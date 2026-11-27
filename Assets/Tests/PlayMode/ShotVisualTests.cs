using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Combat;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class ShotVisualTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> _objects = new List<GameObject>();
        bool _blocked, _over, _fog;
        Camera _camera;

        [SetUp]
        public void Setup()
        {
            Assert.IsNull(Camera.main, "Run in the empty test scene.");
            _blocked = GameFlow.Blocked;
            _over = GameFlow.Over;
            _fog = RenderSettings.fog;
            GameFlow.Blocked = true;
            GameFlow.Over = false;
            RenderSettings.fog = false;
            var go = new GameObject("VFX test camera");
            _objects.Add(go);
            go.tag = "MainCamera";
            _camera = go.AddComponent<Camera>();
            _camera.transform.rotation = Quaternion.Euler(30, 45, 0);
            _camera.transform.position = -_camera.transform.forward * 35;
            _camera.orthographic = true;
            _camera.orthographicSize = 8; // Same framing as World.
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(.055f, .08f, .12f);
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            foreach (var burst in Object.FindObjectsByType<HitBurst>(FindObjectsInactive.Include))
                Object.Destroy(burst.gameObject);
            foreach (var go in _objects)
                if (go != null) Object.Destroy(go);
            _objects.Clear();
            yield return null;
            GameFlow.Blocked = _blocked;
            GameFlow.Over = _over;
            RenderSettings.fog = _fog;
        }

        AttackProjectile Shot(ShotType type, PokemonElement element, Vector3 position)
        {
            var shot = AttackProjectile.SpawnPattern(position, _camera.transform.right,
                8, 1000, 1, Color.white, type, element);
            shot.enabled = false;
            _objects.Add(shot.gameObject);
            return shot;
        }

        static void Step(AttackProjectile shot, float delta)
        {
            GameFlow.Blocked = false;
            typeof(AttackProjectile).GetMethod("TickProjectile", Private).Invoke(shot, new object[] { delta });
            GameFlow.Blocked = true;
        }

        [Test]
        public void EveryCombinationAnimatesWithSharedFogMaterialAndAtlas()
        {
            Material shared = null;
            Texture atlas = null;
            for (int type = 0; type < ShotDefinitions.Count; type++)
            for (int element = 0; element < 13; element++)
            {
                var shot = Shot((ShotType)type, (PokemonElement)element, Vector3.zero);
                var body = shot.transform.Find("VisualAnchor/ElementVisual").GetComponent<SpriteRenderer>();
                var initial = body.sprite;
                Step(shot, .09f);
                Assert.AreNotSame(initial, body.sprite, $"{type}/{element} must animate.");
                foreach (var renderer in shot.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (shared == null) shared = renderer.sharedMaterial;
                    if (atlas == null) atlas = renderer.sprite.texture;
                    Assert.AreSame(shared, renderer.sharedMaterial, "No per-shot material instances.");
                    Assert.AreSame(atlas, renderer.sprite.texture, "All flight layers belong to one atlas.");
                    Assert.AreEqual(FilterMode.Point, renderer.sprite.texture.filterMode);
                }
                Assert.IsTrue(shared.shader.isSupported);
                Object.DestroyImmediate(shot.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator PauseFreezesFlightAndWorldSpaceTrailsAndClearRemovesEveryLayer()
        {
            var shot = Shot(ShotType.Boomerang, PokemonElement.Grass, Vector3.zero);
            shot.enabled = true;
            for (int i = 0; i < 5; i++) Step(shot, .05f);
            var echo = shot.transform.Find("Afterimage0");
            Assert.Less(Vector3.Dot(echo.position - shot.transform.position, _camera.transform.right), -.1f,
                "Afterimages must stay behind in world space, not ride on the projectile.");
            var body = shot.transform.Find("VisualAnchor/ElementVisual").GetComponent<SpriteRenderer>();
            var position = shot.transform.position;
            var echoPosition = echo.position;
            var sprite = body.sprite;
            var rotation = body.transform.rotation;
            yield return null;
            yield return null;
            Assert.AreEqual(position, shot.transform.position);
            Assert.AreEqual(echoPosition, echo.position);
            Assert.AreSame(sprite, body.sprite);
            Assert.AreEqual(rotation, body.transform.rotation);
            AttackProjectile.ClearAll();
            yield return null;
            Assert.IsTrue(shot == null);
            Assert.IsTrue(echo == null, "No orphan trails after a run reset.");
        }

        [UnityTest]
        public IEnumerator ExplosionPoolReplaysAsMuzzleWithoutLeakingItsLargeRingOrLifetime()
        {
            HitBurst.SpawnImpact(Vector3.zero, PokemonElement.Fire, ShotType.Explosive, Vector3.right);
            var burst = Object.FindAnyObjectByType<HitBurst>();
            GameFlow.Blocked = false;
            yield return new WaitForSeconds(.52f);
            GameFlow.Blocked = true;
            Assert.IsFalse(burst.gameObject.activeSelf);
            HitBurst.SpawnMuzzle(Vector3.one, PokemonElement.Ice, ShotType.Precision, Vector3.right);
            Assert.AreSame(burst, Object.FindAnyObjectByType<HitBurst>());
            Vector3 ringScale = burst.transform.Find("ShockRing").localScale;
            Assert.Less(Mathf.Max(ringScale.x, ringScale.y), 1);
            Assert.AreEqual(Vector3.one, burst.transform.position);
            GameFlow.Blocked = false;
            yield return new WaitForSeconds(.22f);
            Assert.IsFalse(burst.gameObject.activeSelf);
        }

        [UnityTest, Explicit("Local GPU contact sheets; select this test explicitly.")]
        public IEnumerator CaptureAnimationBoards()
        {
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType);
            string directory = Path.GetFullPath("Temp/CombatVfxReview");
            Directory.CreateDirectory(directory);
            var target = new RenderTexture(1280, 720, 24) { antiAliasing = 1 };
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            _camera.targetTexture = target;
            var types = (ShotType[])Enum.GetValues(typeof(ShotType));
            var elements = new[] { PokemonElement.Fire, PokemonElement.Electric, PokemonElement.Grass,
                PokemonElement.Ice, PokemonElement.Steel, PokemonElement.Psychic, PokemonElement.Dragon };
            try
            {
                var board = new List<AttackProjectile>();
                for (int row = 0; row < 7; row++)
                for (int column = 0; column < 4; column++)
                {
                    var shot = Shot(types[row], elements[row], Point(-10 + column * 6, 6 - row * 2));
                    Step(shot, new[] { 0f, .08f, .18f, .30f }[column]);
                    board.Add(shot);
                }
                yield return new WaitForEndOfFrame();
                Save(target, image, directory, "patterns.png");
                foreach (var shot in board) Object.DestroyImmediate(shot.gameObject);
                board.Clear();

                for (int element = 0; element < 13; element++)
                for (int sample = 0; sample < 3; sample++)
                {
                    var shot = Shot(ShotType.Basic, (PokemonElement)element,
                        Point(-11 + element / 7 * 13 + sample * 3.2f, 6 - element % 7 * 2));
                    Step(shot, .07f + sample * .09f);
                    board.Add(shot);
                }
                yield return new WaitForEndOfFrame();
                Save(target, image, directory, "elements.png");
                foreach (var shot in board) Object.DestroyImmediate(shot.gameObject);
                board.Clear();

                for (int row = 0; row < 7; row++)
                for (int column = 0; column < 4; column++)
                {
                    Vector3 position = Point(-10 + column * 6, 6 - row * 2);
                    HitBurst.SpawnImpact(position, elements[row],
                        types[row], _camera.transform.right);
                    HitBurst newest = null;
                    foreach (var burst in Object.FindObjectsByType<HitBurst>())
                        if (burst.transform.position == position) newest = burst;
                    typeof(HitBurst).GetField("_time", Private).SetValue(newest, new[] { .02f, .08f, .16f, .28f }[column]);
                    typeof(HitBurst).GetMethod("Animate", Private).Invoke(newest, null);
                }
                yield return new WaitForEndOfFrame();
                Save(target, image, directory, "impacts.png");
                foreach (var burst in Object.FindObjectsByType<HitBurst>()) Object.DestroyImmediate(burst.gameObject);

                for (int row = 0; row < 7; row++)
                for (int column = 0; column < 4; column++)
                {
                    Vector3 position = Point(-10 + column * 6, 6 - row * 2);
                    HitBurst.SpawnMuzzle(position, elements[row], types[row], _camera.transform.right);
                    foreach (var burst in Object.FindObjectsByType<HitBurst>())
                    {
                        if (burst.transform.position != position) continue;
                        typeof(HitBurst).GetField("_time", Private).SetValue(burst, new[] { 0f, .03f, .07f, .12f }[column]);
                        typeof(HitBurst).GetMethod("Animate", Private).Invoke(burst, null);
                    }
                }
                yield return new WaitForEndOfFrame();
                Save(target, image, directory, "muzzles.png");
                foreach (var burst in Object.FindObjectsByType<HitBurst>()) Object.DestroyImmediate(burst.gameObject);

                for (int i = 0; i < 20; i++)
                {
                    var shot = Shot(types[i % 7], (PokemonElement)(i % 13),
                        Point(-10 + i % 5 * 4.5f, 5 - i / 5 * 3.3f));
                    Step(shot, .2f);
                    board.Add(shot);
                }
                yield return new WaitForEndOfFrame();
                Save(target, image, directory, "crowd.png");
                foreach (var shot in board) Object.DestroyImmediate(shot.gameObject);
                board.Clear();

                for (int row = 0; row < 7; row++)
                    board.Add(Shot(types[row], elements[row], Point(-5, 6 - row * 2)));
                for (int frame = 0; frame < 24; frame++)
                {
                    foreach (var shot in board) Step(shot, 1f / 30);
                    yield return new WaitForEndOfFrame();
                    Save(target, image, directory, "flight-" + frame.ToString("00") + ".png");
                }
                Debug.Log("Combat VFX captures: " + directory);
            }
            finally
            {
                _camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }

        Vector3 Point(float x, float y) => _camera.transform.right * x + _camera.transform.up * y;

        [Test]
        public void TwentyAnimatedShotsHaveNoSteadyStateManagedAllocations()
        {
            var tick = typeof(AttackProjectile).GetMethod("TickProjectile", Private);
            var actions = new Action<float>[20];
            for (int i = 0; i < actions.Length; i++)
            {
                var shot = Shot((ShotType)(i % 7), (PokemonElement)(i % 13), Point(i, i));
                actions[i] = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), shot, tick);
            }
            GameFlow.Blocked = false;
            for (int warm = 0; warm < 10; warm++)
                foreach (var action in actions) action(1f / 60);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 240; frame++)
                foreach (var action in actions) action(1f / 60);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            watch.Stop();
            GameFlow.Blocked = true;
            Assert.AreEqual(0, allocated, "Flight animation and trail samples must not allocate each frame.");
            Debug.Log($"Combat VFX: 20 shots / 240 ticks, {watch.Elapsed.TotalMilliseconds / 240:F3} ms/tick, {allocated} managed bytes.");
        }

        static void Save(RenderTexture target, Texture2D image, string directory, string name)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; }
        }
    }
}
