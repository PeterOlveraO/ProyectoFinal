#if UNITY_EDITOR
using Game.Enemies;
using Game.Player;
using Game.Rounds;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Deja la escena World lista para el menú y el combate:
    ///   · quita la malla 3D del jugador (solo queda su sprite Pokémon),
    ///   · añade el joystick derecho de ataque (naranja) junto al izquierdo (azul),
    ///   · añade PlayerAttack al jugador,
    ///   · añade GameMenuController y el botón del poder especial al HUD.
    ///
    /// Es idempotente: se puede ejecutar varias veces.
    /// </summary>
    public static class GameplaySetup
    {
        const string ScenePath = "Assets/Scenes/World.unity";

        [MenuItem("Tools/Game/Setup Menus And Attack")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[GameplaySetup] No se encontró 'Player' en la escena.");
                return;
            }

            RemovePlayerMesh(player);

            var hud = GameObject.Find("HUD");
            if (hud == null)
            {
                Debug.LogError("[GameplaySetup] No se encontró 'HUD' en la escena.");
                return;
            }

            var moveJoystick = ConfigureMoveJoystick();
            var attackJoystick = ConfigureAttackJoystick(hud.transform);

            var sphereController = player.GetComponent<PlayerSphereController>();
            if (sphereController != null) sphereController.SetJoystick(moveJoystick);

            var attack = GetOrAdd<PlayerAttack>(player);
            attack.SetJoystick(attackJoystick);

            GetOrAdd<GameMenuController>(hud);
            GetOrAdd<SpecialPowerButton>(hud);
            GetOrAdd<RunHud>(hud);

            ConfigureRounds();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[GameplaySetup] Escena actualizada: jugador con vida, joystick de ataque, " +
                      "menú principal/pausa, poder especial, HUD de ronda y sistema de rondas.");
        }

        /// <summary>
        /// Sustituye los spawners infinitos por zona por el gestor de rondas: ahora los
        /// enemigos aparecen alrededor del jugador y cada ronda pide eliminar un cupo.
        /// </summary>
        static void ConfigureRounds()
        {
            var world = GameObject.Find("World");
            if (world == null)
            {
                Debug.LogError("[GameplaySetup] No se encontró 'World' en la escena.");
                return;
            }

            var oldBootstrap = world.GetComponent<ZoneSpawnBootstrap>();
            if (oldBootstrap != null) Object.DestroyImmediate(oldBootstrap, true);

            GetOrAdd<RoundManager>(world);
        }

        static void RemovePlayerMesh(GameObject player)
        {
            var filter = player.GetComponent<MeshFilter>();
            if (filter != null) Object.DestroyImmediate(filter, true);

            var renderer = player.GetComponent<MeshRenderer>();
            if (renderer != null) Object.DestroyImmediate(renderer, true);

            // Cualquier renderer de malla que hubiera quedado en los hijos.
            foreach (var childRenderer in player.GetComponentsInChildren<MeshRenderer>(true))
                Object.DestroyImmediate(childRenderer.gameObject, true);
        }

        static VirtualJoystick ConfigureMoveJoystick()
        {
            var go = GameObject.Find("Joystick");
            if (go == null)
            {
                var hud = GameObject.Find("HUD");
                go = new GameObject("Joystick", typeof(RectTransform));
                go.transform.SetParent(hud.transform, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(0.5f, 0.6f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            var joystick = GetOrAdd<VirtualJoystick>(go);
            joystick.SetShowInEditor(true);
            joystick.SetPinned(true);
            joystick.SetRadius(110f);
            joystick.SetColors(new Color(0.45f, 0.72f, 1f, 0.24f),
                               new Color(1f, 1f, 1f, 0.62f));
            EditorUtility.SetDirty(joystick);
            return joystick;
        }

        static VirtualJoystick ConfigureAttackJoystick(Transform hud)
        {
            var go = GameObject.Find("AttackJoystick");
            if (go == null)
            {
                go = new GameObject("AttackJoystick", typeof(RectTransform));
                go.transform.SetParent(hud, false);
            }

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(1f, 0.6f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var joystick = GetOrAdd<VirtualJoystick>(go);
            joystick.SetShowInEditor(true);
            joystick.SetPinned(true);
            joystick.SetRadius(120f);
            joystick.SetColors(new Color(1f, 0.45f, 0.25f, 0.26f),
                               new Color(1f, 0.62f, 0.25f, 0.7f));
            EditorUtility.SetDirty(joystick);
            return joystick;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }
    }
}
#endif
