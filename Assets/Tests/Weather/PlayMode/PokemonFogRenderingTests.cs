using System.Collections;
using System.Collections.Generic;
using Game.Pokemon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Game.Weather.Tests
{
    /// <summary>
    /// Real-camera GPU regressions, not a CPU copy of the shader. URP Lit is the terrain
    /// reference; SpriteRenderer draws exercise native tint, flips, texture and sorting.
    /// Null graphics is a failure: Linux CI must use its llvmpipe renderer.
    /// </summary>
    public sealed class PokemonFogRenderingTests
    {
        const int Layer = 30, Width = 128, Height = 64;
        readonly List<Object> _owned = new List<Object>();
        readonly WeatherEnvironment _environment = new WeatherEnvironment();
        Camera _camera;
        Light _previousSun;
        RenderTexture _target;
        Texture2D _readback;
        bool _captured;
        static readonly Color Ink = new Color(.12f, .18f, .26f, 1f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "These regressions require real GPU readback (llvmpipe is supported), never a skipped Null renderer.");
            Assert.That(WeatherController.Instance, Is.Null, "Run in the empty PlayMode test scene.");
            Assert.That(Camera.main, Is.Null);
            _previousSun = RenderSettings.sun;
            _environment.Capture();
            _captured = true;
            _target = Own(new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear) { antiAliasing = 1, filterMode = FilterMode.Point });
            Assert.That(_target.Create(), Is.True);
            _readback = Own(new Texture2D(Width, Height, TextureFormat.RGBA32, false, true));
            _camera = Create("Pokemon fog probe camera").AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 2f;
            _camera.aspect = 2f;
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 160f;
            _camera.cullingMask = 1 << Layer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.targetTexture = _target;
            var data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.volumeLayerMask = 0;
            data.antialiasing = AntialiasingMode.None;
            var sun = Create("Pokemon fog probe sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Color.white;
            sun.intensity = 1f;
            sun.shadows = LightShadows.None;
            sun.cullingMask = 1 << Layer;
            RenderSettings.sun = sun;
            RenderSettings.fog = false;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.69f, .78f, .80f);
            RenderSettings.fogDensity = .022f;
            yield return Frame();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_camera != null)
            {
                _camera.enabled = false;
                _camera.targetTexture = null;
            }
            if (_target != null) _target.Release();
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            if (_captured)
            {
                _environment.Restore();
                RenderSettings.sun = _previousSun;
                _captured = false;
            }
            // Allow deferred PlayMode destruction to settle before the next fixture.
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpriteFogMatchesLitTerrainAtRealDepthInOrthographicAndPerspectiveCameras()
        {
            var sprite = ProbeSprite(SolidTexture(Ink));
            var pokemon = Billboard(sprite);
            var terrain = Quad(LitMaterial(), "Lit terrain fog reference");
            foreach (bool orthographic in new[] { true, false })
            {
                _camera.orthographic = orthographic;
                // Measure the actual fog endpoint using URP Lit, avoiding assumptions about
                // platform color conversion. Exp2 at 140 units leaves < .0001 transmittance.
                Place(pokemon.transform, .25f, 140f);
                Place(terrain.transform, .75f, 140f);
                RenderSettings.fog = true;
                yield return Frame();
                ReadPixels();
                Color fog = Sample(.75f, .5f);
                Assert.That(fog.r, Is.GreaterThan(.2f), "The Lit reference must actually render fog.");
                float previousTransmission = 1f;
                foreach (float depth in new[] { 12f, 40f, 75f })
                {
                    Place(pokemon.transform, .25f, depth);
                    Place(terrain.transform, .75f, depth);
                    RenderSettings.fog = false;
                    yield return Frame();
                    ReadPixels();
                    Color clearSprite = Sample(.25f, .5f), clearTerrain = Sample(.75f, .5f);
                    AssertRgb(clearSprite, Ink, .025f, "Sunny unlit texels must remain unchanged.");
                    Assert.That(Mathf.Abs(clearTerrain.r - fog.r), Is.GreaterThan(.15f));
                    RenderSettings.fog = true;
                    yield return Frame();
                    ReadPixels();
                    Color mistSprite = Sample(.25f, .5f), mistTerrain = Sample(.75f, .5f);
                    float spriteTransmission = (mistSprite.r - fog.r) / (clearSprite.r - fog.r);
                    float terrainTransmission = (mistTerrain.r - fog.r) / (clearTerrain.r - fog.r);
                    float distance = depth - _camera.nearClipPlane;
                    float expected = Mathf.Exp(-.022f * .022f * distance * distance);
                    string context = $"projection={orthographic}, depth={depth}, GPU={SystemInfo.graphicsDeviceType}";
                    Assert.That(terrainTransmission, Is.EqualTo(expected).Within(.035f), context + ": Lit control");
                    Assert.That(spriteTransmission, Is.EqualTo(terrainTransmission).Within(.025f), context);
                    Assert.That(spriteTransmission, Is.LessThan(previousTransmission - .015f), context);
                    previousTransmission = spriteTransmission;
                }
            }
        }

        [UnityTest]
        public IEnumerator SunnyMatchesLegacySpriteIncludingTintFlipsPointSamplingAndAlpha()
        {
            var texture = Texture();
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                texture.SetPixel(x, y, new Color(.15f + .2f * x, .1f + .2f * y, .3f,
                    x == 0 ? 0f : y == 0 ? .5f : 1f));
            texture.Apply();
            var sprite = ProbeSprite(texture);
            var pokemon = Billboard(sprite);
            var legacy = Billboard(sprite);
            var shader = Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            legacy.sharedMaterial = Own(new Material(shader));
            Place(pokemon.transform, .25f, 20f);
            Place(legacy.transform, .75f, 20f);
            // Fixed size: each 4x4 texel spans 8x8 target pixels, avoiding edge probes.
            pokemon.transform.localScale = legacy.transform.localScale = Vector3.one;
            pokemon.color = legacy.color = new Color(.65f, .35f, .9f, .55f);
            foreach (int flips in new[] { 0, 1, 2, 3 })
            {
                pokemon.flipX = legacy.flipX = (flips & 1) != 0;
                pokemon.flipY = legacy.flipY = (flips & 2) != 0;
                yield return Frame();
                ReadPixels();
                float brightest = 0f;
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    Color expected = _readback.GetPixel(80 + 4 + x * 8, 16 + 4 + y * 8);
                    Color actual = _readback.GetPixel(16 + 4 + x * 8, 16 + 4 + y * 8);
                    AssertRgb(actual, expected, .012f, $"Sunny native SpriteRenderer: flip={flips}, texel={x},{y}");
                    brightest = Mathf.Max(brightest, actual.maxColorComponent);
                }
                Assert.That(brightest, Is.GreaterThan(.05f), "Both invisible draws must not pass the comparison.");
            }
        }

        [UnityTest]
        public IEnumerator FogPreservesTransparentAndPartialAlphaOverTheRealBlackTerritoryShader()
        {
            _camera.backgroundColor = Color.magenta; // A missing black overlay must fail.
            var zoneSource = Resources.Load<Material>("WorldZoneFog");
            Assert.That(zoneSource, Is.Not.Null);
            var zone = Own(new Material(zoneSource));
            zone.SetInt("_ClosedCount", 1);
            var rects = new Vector4[9];
            rects[0] = new Vector4(-1000f, -1000f, 1000f, 1000f);
            zone.SetVectorArray("_ClosedRects", rects);
            zone.SetFloat("_RevealAlpha", 0f);
            var blackGround = Quad(zone, "Real black territory overlay");
            blackGround.transform.position = new Vector3(0f, 0f, 90f);
            blackGround.transform.localScale = new Vector3(4f, 2f, 1f);
            var texture = Texture();
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                texture.SetPixel(x, y, new Color(Ink.r, Ink.g, Ink.b, x < 2 ? 0f : y < 2 ? .5f : 1f));
            texture.Apply();
            var pokemon = Billboard(ProbeSprite(texture));
            Place(pokemon.transform, .5f, 45f);
            pokemon.transform.localScale = Vector3.one;
            pokemon.color = new Color(1f, 1f, 1f, .75f);
            RenderSettings.fog = true;
            yield return Frame();
            ReadPixels();
            Color transparent = _readback.GetPixel(56, 40);
            Color partial = _readback.GetPixel(72, 24);
            Color opaque = _readback.GetPixel(72, 40);
            AssertRgb(Sample(.1f, .5f), Color.black, 1f / 255f, "Territory black must not receive atmospheric fog.");
            AssertRgb(transparent, Color.black, 1f / 255f, "Transparent sprite texels must not paint a fog rectangle.");
            Assert.That(opaque.r, Is.GreaterThan(.1f), "The sprite must render before testing its transparent texels.");
            AssertRgb(partial, opaque * .5f, .012f, "Fog must be blended before premultiplying texture and renderer alpha.");
        }

        [UnityTest]
        public IEnumerator SpriteSortingForcedVisibilityAndOpaqueTerrainDepthStillWorkWithFog()
        {
            var sprite = ProbeSprite(SolidTexture(Color.white));
            var first = Billboard(sprite);
            var second = Billboard(sprite);
            first.color = new Color(.8f, .1f, .1f);
            second.color = new Color(.1f, .1f, .8f);
            Place(first.transform, .5f, 30f);
            Place(second.transform, .5f, 30f);
            RenderSettings.fog = true;
            second.forceRenderingOff = true;
            yield return Frame();
            ReadPixels();
            Color firstOnly = Sample(.5f, .5f);
            first.forceRenderingOff = true;
            second.forceRenderingOff = false;
            yield return Frame();
            ReadPixels();
            Color secondOnly = Sample(.5f, .5f);
            Assert.That(Mathf.Abs(firstOnly.r - secondOnly.r), Is.GreaterThan(.1f));
            first.forceRenderingOff = false;
            second.sortingOrder = 5;
            yield return Frame();
            ReadPixels();
            AssertRgb(Sample(.5f, .5f), secondOnly, .012f, "Higher sprite sorting order must win.");
            first.sortingOrder = 10;
            yield return Frame();
            ReadPixels();
            AssertRgb(Sample(.5f, .5f), firstOnly, .012f, "Sorting must still work with a shared material.");

            var ground = Quad(LitMaterial(), "Opaque terrain in front of Pokemon");
            Place(ground.transform, .5f, 10f);
            first.forceRenderingOff = second.forceRenderingOff = true;
            yield return Frame();
            ReadPixels();
            Color terrainOnly = Sample(.5f, .5f);
            first.forceRenderingOff = second.forceRenderingOff = false;
            yield return Frame();
            ReadPixels();
            AssertRgb(Sample(.5f, .5f), terrainOnly, .012f, "Sprite ZTest must respect opaque terrain depth.");
        }

        static IEnumerator Frame()
        {
            // No WaitForEndOfFrame: it can hang the batch test runner without a Game View.
            yield return null;
            yield return null;
        }

        T Own<T>(T obj) where T : Object { _owned.Add(obj); return obj; }
        GameObject Create(string name)
        {
            var go = Own(new GameObject(name));
            go.layer = Layer;
            return go;
        }

        Texture2D Texture() => Own(new Texture2D(4, 4, TextureFormat.RGBA32, false, true)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });

        Texture2D SolidTexture(Color color)
        {
            var texture = Texture();
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++) texture.SetPixel(x, y, color);
            texture.Apply();
            return texture;
        }

        Sprite ProbeSprite(Texture2D texture)
        {
            var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f,
                2f, 0, SpriteMeshType.FullRect));
            sprite.name = "pokemon-fog-gpu-probe";
            return sprite;
        }

        SpriteRenderer Billboard(Sprite sprite)
        {
            var actor = Create("Pokemon material GPU probe");
            var renderer = PokemonVisuals.CreateBillboard(actor.transform, "Probe sprite", sprite, 2f, false, 2f, 0f);
            renderer.gameObject.layer = Layer;
            // Exclude the independent contact-shadow artwork from these shader probes.
            actor.transform.Find("Ground Contact").gameObject.SetActive(false);
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Game/Pokemon/WorldSpriteFog"));
            Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            return renderer;
        }

        Material LitMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "Use the project's actual terrain shader as the control.");
            var material = Own(new Material(shader));
            // Isolate fog from illumination. This still executes LitForwardPass's real fog.
            material.SetColor("_BaseColor", Color.black);
            material.SetColor("_EmissionColor", Ink);
            material.EnableKeyword("_EMISSION");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_Cull", 0f);
            return material;
        }

        MeshRenderer Quad(Material material, string name)
        {
            var go = Create(name);
            var mesh = Own(new Mesh());
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
                new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1),
                new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        void Place(Transform target, float viewportX, float depth)
        {
            target.position = _camera.ViewportToWorldPoint(new Vector3(viewportX, .5f, depth));
            float viewHeight = _camera.orthographic ? _camera.orthographicSize * 2f :
                2f * depth * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * .5f);
            target.localScale = new Vector3(viewHeight * .2f, viewHeight * .2f, 1f);
        }

        void ReadPixels()
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target;
                _readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                _readback.Apply(false, false);
            }
            finally { RenderTexture.active = previous; }
        }

        Color Sample(float x, float y) => _readback.GetPixel((int)(x * Width), (int)(y * Height));
        static void AssertRgb(Color actual, Color expected, float tolerance, string context)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), context + " R");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), context + " G");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), context + " B");
        }
    }
}
