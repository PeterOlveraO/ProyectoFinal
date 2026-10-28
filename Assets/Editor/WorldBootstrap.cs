#if UNITY_EDITOR
using Game.Player;
using Game.Terrain;
using Game.World;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Bootstrap idempotente de la escena isométrica. Se puede ejecutar varias veces.
    ///
    /// Deja la escena así:
    ///   World > Terrain      (WorldTerrain: mundo finito de bloques, carga por radio cuadrado)
    ///         > Player       (esfera temporal con CharacterController; adapta el antiguo Trainer)
    ///         > HUD          (Canvas con el joystick táctil; solo visible en móvil)
    ///         > EventSystem  (Input System UI module)
    ///         > Iso VCam     (Cinemachine: sigue al Player con rotación fija 30, 45, 0)
    ///         > CameraRig    (cámara ortográfica con CinemachineBrain)
    ///         > Sun          (luz direccional)
    /// </summary>
    public static class WorldBootstrap
    {
        const string ScenePath = "Assets/Scenes/World.unity";
        const string SettingsFolder = "Assets/Settings";
        const string TerrainSettingsPath = SettingsFolder + "/TerrainSettings.asset";
        const string MaterialsFolder = "Assets/Materials";
        const string PlayerMatPath = MaterialsFolder + "/M_Trainer.mat";

        public static readonly Vector3 IsoRotation = new Vector3(30f, 45f, 0f);

        const float CameraDistance = 40f;
        const float OrthographicSize = 8f;

        [MenuItem("Tools/World/Bootstrap Isometric World")]
        public static void Bootstrap()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

            EnsureFolder("Assets", "Settings");
            EnsureFolder("Assets", "Materials");

            var settings = LoadOrCreateTerrainSettings();
            var world = FindOrCreate("World", null);

            // --- Terreno anterior fuera ------------------------------------
            var legacy = GameObject.Find("ChunkStreaming");
            if (legacy != null) Object.DestroyImmediate(legacy);

            // --- Terreno ---------------------------------------------------
            var terrainGo = FindOrCreate("Terrain", world.transform);
            terrainGo.transform.localPosition = Vector3.zero;
            var terrain = GetOrAdd<WorldTerrain>(terrainGo);
            SetObject(terrain, "settings", settings);

            // --- Jugador (adapta el Trainer existente si lo hay) -----------
            var player = GameObject.Find("Player");
            if (player == null) player = GameObject.Find("Trainer");
            if (player == null) player = new GameObject("Player");
            player.name = "Player";
            player.transform.SetParent(world.transform, false);
            BuildPlayer(player, settings);
            SetObject(terrain, "player", player.transform);

            // --- Controles táctiles ----------------------------------------
            var joystick = BuildMobileControls(world.transform);
            SetObject(player.GetComponent<PlayerSphereController>(), "joystick", joystick);

            // --- Cámara (adapta la existente) ------------------------------
            var rig = FindOrCreate("CameraRig", world.transform);
            rig.transform.position = Vector3.zero;
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(rig);

            var camGo = Camera.main != null ? Camera.main.gameObject : FindOrCreate("Main Camera", rig.transform);
            camGo.tag = "MainCamera";
            var cam = GetOrAdd<Camera>(camGo);
            ConfigureIsometricCamera(cam);
            camGo.transform.position = player.transform.position - cam.transform.forward * CameraDistance;

            SetupVirtualCamera(world.transform, cam, player.transform);

            // --- Luz -------------------------------------------------------
            var sun = FindOrCreate("Sun", world.transform);
            var light = GetOrAdd<Light>(sun);
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.intensity = 1.15f;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[WorldBootstrap] Escena lista: terreno " + settings.chunksX + "x" + settings.chunksZ +
                      " chunks de " + settings.chunkSize + " bloques, jugador esfera, cámara " + IsoRotation);
        }

        static TerrainSettings LoadOrCreateTerrainSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TerrainSettings>(TerrainSettingsPath);
            if (settings != null) return settings;

            settings = ScriptableObject.CreateInstance<TerrainSettings>();
            AssetDatabase.CreateAsset(settings, TerrainSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        static Material LoadOrCreatePlayerMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PlayerMatPath);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            material = new Material(shader) { name = "M_Trainer", color = new Color(0.95f, 0.55f, 0.15f, 1f) };
            AssetDatabase.CreateAsset(material, PlayerMatPath);
            return material;
        }

        /// <summary>Esfera de 1 unidad de diámetro con CharacterController (radio 0.5, altura 1).</summary>
        static void BuildPlayer(GameObject player, TerrainSettings settings)
        {
            // Restos del marcador anterior (cápsula "Body", TrainerRoamTest ya borrado).
            for (int i = player.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(player.transform.GetChild(i).gameObject);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player);

            player.transform.localScale = Vector3.one;
            player.transform.rotation = Quaternion.identity;

            // Centro aproximado del mundo; en Play el controlador lo coloca sobre el bloque central.
            float half = settings.chunkSize * settings.blockSize * 0.5f;
            player.transform.position = new Vector3(settings.chunksX * half, settings.flatLevel * settings.stepHeight + 0.55f,
                                                    settings.chunksZ * half);

            var filter = GetOrAdd<MeshFilter>(player);
            if (filter.sharedMesh == null || filter.sharedMesh.name != "Sphere")
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                filter.sharedMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(primitive);
            }

            var renderer = GetOrAdd<MeshRenderer>(player);
            renderer.sharedMaterial = LoadOrCreatePlayerMaterial();

            var controller = GetOrAdd<CharacterController>(player);
            controller.radius = 0.5f;
            controller.height = 1f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.55f;
            controller.skinWidth = 0.02f;
            controller.slopeLimit = 45f;

            GetOrAdd<PlayerSphereController>(player);
        }

        /// <summary>
        /// HUD con el joystick flotante en la mitad inferior izquierda de la pantalla.
        /// El joystick se oculta solo fuera de móvil (ver VirtualJoystick.showInEditor).
        /// </summary>
        static VirtualJoystick BuildMobileControls(Transform world)
        {
            var hud = FindOrCreate("HUD", world);
            var canvas = GetOrAdd<Canvas>(hud);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = GetOrAdd<CanvasScaler>(hud);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAdd<GraphicRaycaster>(hud);

            var zone = GameObject.Find("Joystick");
            if (zone == null)
            {
                zone = new GameObject("Joystick", typeof(RectTransform));
                zone.transform.SetParent(hud.transform, false);
            }

            var rect = (RectTransform)zone.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0.5f, 0.6f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var joystick = GetOrAdd<VirtualJoystick>(zone);

            // EventSystem con el módulo del Input System (el proyecto usa solo el Input System nuevo).
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            var esGo = eventSystem != null ? eventSystem.gameObject : FindOrCreate("EventSystem", world);
            GetOrAdd<EventSystem>(esGo);
            var legacyModule = esGo.GetComponent<StandaloneInputModule>();
            if (legacyModule != null) Object.DestroyImmediate(legacyModule);
            GetOrAdd<InputSystemUIInputModule>(esGo);

            return joystick;
        }

        static void ConfigureIsometricCamera(Camera cam)
        {
            cam.orthographic = true;
            cam.orthographicSize = OrthographicSize;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.transform.rotation = Quaternion.Euler(IsoRotation);
        }

        /// <summary>Cámara virtual de Cinemachine: sigue al jugador con la rotación bloqueada.</summary>
        static void SetupVirtualCamera(Transform world, Camera cam, Transform followTarget)
        {
            var vcamGo = FindOrCreate("Iso VCam", world);
            vcamGo.transform.rotation = Quaternion.Euler(IsoRotation);

            GetOrAdd<CinemachineBrain>(cam.gameObject);
            var vcam = GetOrAdd<CinemachineCamera>(vcamGo);

            vcam.Follow = followTarget;
            vcam.LookAt = null;   // sin aim: la rotación queda fija en (30, 45, 0)

            var lens = vcam.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = cam.orthographicSize;
            lens.NearClipPlane = cam.nearClipPlane;
            lens.FarClipPlane = cam.farClipPlane;
            vcam.Lens = lens;

            var follow = GetOrAdd<CinemachineFollow>(vcamGo);
            // Offset en espacio de MUNDO: la cámara conserva el ángulo isométrico.
            follow.FollowOffset = -vcamGo.transform.forward * CameraDistance;
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.25f, 0.25f, 0.25f);

            var zoom = GetOrAdd<IsoCameraZoom>(vcamGo);
            SetFloat(zoom, "minSize", 4f);
            SetFloat(zoom, "maxSize", 20f);

            vcamGo.transform.position = followTarget.position + follow.FollowOffset;

            EditorUtility.SetDirty(vcam);
            EditorUtility.SetDirty(follow);
        }

        static GameObject FindOrCreate(string name, Transform parent)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static void SetObject(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning("[WorldBootstrap] Campo no encontrado: " + field + " en " + target.name);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
