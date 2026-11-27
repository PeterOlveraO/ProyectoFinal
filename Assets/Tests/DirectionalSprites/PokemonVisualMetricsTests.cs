using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.DirectionalSprites.Tests
{
    public class PokemonVisualMetricsTests
    {
        [Serializable]
        public class PixelBounds
        {
            public string resource;
            public int width, height, left, bottom, visibleWidth, visibleHeight;
        }

        [Serializable]
        public class Family
        {
            public string name;
            public int maxVisibleWidth, maxVisibleHeight;
            public PixelBounds source;
            public PixelBounds[] frames;
        }

        [Serializable]
        public class Metrics
        {
            public int version;
            public Family[] families;
        }

        [Serializable]
        public class PhysicalSize
        {
            public string name;
            public float heightMeters;
        }

        [Serializable]
        public class PhysicalSizes
        {
            public PhysicalSize[] families;
        }

        Type visuals, layoutType, animationType;
        Metrics metrics;

        [SetUp]
        public void SetUp()
        {
            visuals = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Game.Pokemon.PokemonVisuals")).First(t => t != null);
            layoutType = visuals.Assembly.GetType("Game.Pokemon.PokemonBillboardLayout");
            animationType = visuals.GetNestedType("DirectionalAnimation");
            var asset = Resources.Load<TextAsset>("PokemonVisualMetrics");
            Assert.IsNotNull(asset);
            metrics = JsonUtility.FromJson<Metrics>(asset.text);
            Assert.AreEqual(1, metrics.version);
            Assert.AreEqual(190, metrics.families.Length);
        }

        SpriteRenderer Create(Transform actor, Sprite source, float height, float width, float ground)
        {
            var method = visuals.GetMethod("CreateBillboard", new[] {
                typeof(Transform), typeof(string), typeof(Sprite), typeof(float),
                typeof(bool), typeof(float), typeof(float)
            });
            return (SpriteRenderer)method.Invoke(null, new object[] {
                actor, "metrics test", source, height, false, width, ground
            });
        }

        object Animation(SpriteRenderer renderer, Sprite source) =>
            Activator.CreateInstance(animationType, renderer, source);

        void Step(object animation, Camera camera, Vector3 movement, float dt) =>
            animationType.GetMethod("Update").Invoke(animation, new object[] { movement, camera, dt });

        static void AssertGeometry(SpriteRenderer renderer, PixelBounds pixels, float height, float width,
                                   float ground, string context)
        {
            var sprite = renderer.sprite;
            Assert.AreEqual(pixels.width, sprite.rect.width, context);
            Assert.AreEqual(pixels.height, sprite.rect.height, context);
            Assert.IsFalse(sprite.texture.isReadable, context);
            Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, context);
            Vector3 scale = renderer.transform.localScale;
            Assert.That(scale.x, Is.EqualTo(scale.y).Within(0.00001f), context);
            Assert.That(pixels.visibleHeight * scale.y / sprite.pixelsPerUnit,
                        Is.LessThanOrEqualTo(height + 0.00001f), context);
            Assert.That(pixels.visibleWidth * scale.x / sprite.pixelsPerUnit,
                        Is.LessThanOrEqualTo(width + 0.00001f), context);
            float foot = renderer.transform.localPosition.y +
                         (pixels.bottom - sprite.pivot.y) * scale.y / sprite.pixelsPerUnit;
            Assert.That(foot, Is.EqualTo(ground).Within(0.00001f), context);
        }

        [TestCase(1.35f, 2.25f, 0f)]
        [TestCase(1.9f, 3.2f, 0f)]
        [TestCase(1.65f, 2.75f, -0.5f)]
        public void EverySourceAndSixFramesFitTheirProfileAndStayGrounded(float height, float width, float ground)
        {
            var actor = new GameObject("metrics actor");
            var cameraObject = new GameObject("metrics camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var first = Resources.Load<Sprite>(metrics.families[0].source.resource);
                var renderer = Create(actor.transform, first, height, width, ground);
                var layout = renderer.GetComponent(layoutType);
                Assert.IsNotNull(layout);
                int ordinary = 0;
                foreach (var family in metrics.families)
                {
                    if (family.source.resource.StartsWith("PokemonSprites/")) ordinary++;
                    var source = Resources.Load<Sprite>(family.source.resource);
                    Assert.IsNotNull(source, family.name);
                    layoutType.GetMethod("Configure").Invoke(layout,
                        new object[] { renderer, source, height, width, ground });
                    var animation = Animation(renderer, source);
                    Assert.AreSame(source, renderer.sprite, "Initial sprite identity: " + family.name);
                    AssertGeometry(renderer, family.source, height, width, ground, family.name + "/source");
                    float sourceScale = Mathf.Min(height * source.pixelsPerUnit / family.source.visibleHeight,
                                                  width * source.pixelsPerUnit / family.source.visibleWidth);
                    Assert.That(renderer.transform.localScale.x, Is.EqualTo(sourceScale).Within(0.00001f));
                    renderer.transform.localPosition += new Vector3(.31f, 0f, -.27f);
                    var horizontal = renderer.transform.localPosition;
                    var directions = new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };
                    for (int view = 0; view < directions.Length; view++)
                    for (int frame = 0; frame < 2; frame++)
                    {
                        Step(animation, camera, directions[view], frame == 0 ? 0f : .17f);
                        int index = (view == 3 ? 4 : view * 2) + frame;
                        Assert.AreSame(Resources.Load<Sprite>(family.frames[index].resource), renderer.sprite);
                        Assert.AreEqual(view == 3, renderer.flipX, family.name);
                        AssertGeometry(renderer, family.frames[index], height, width, ground,
                                       family.name + "/" + index);
                        float expected = Mathf.Min(height * renderer.sprite.pixelsPerUnit / family.maxVisibleHeight,
                                                   width * renderer.sprite.pixelsPerUnit / family.maxVisibleWidth);
                        Assert.That(renderer.transform.localScale.x, Is.EqualTo(expected).Within(.00001f),
                                    "Scale must stay fixed across views and phases: " + family.name);
                        Assert.AreEqual(horizontal.x, renderer.transform.localPosition.x);
                        Assert.AreEqual(horizontal.z, renderer.transform.localPosition.z);
                    }
                    Assert.AreEqual(Vector3.one, actor.transform.localScale);
                }
                Assert.AreEqual(178, ordinary);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [TestCase("poke_052")]
        [TestCase("species_mew")]
        public void MissingAnimationUsesTheSourceAlphaBounds(string sourceName)
        {
            var actor = new GameObject("fallback actor");
            var cameraObject = new GameObject("fallback camera");
            Sprite[] cached = null, saved = null;
            try
            {
                var family = metrics.families.Single(f => f.name == sourceName);
                var source = Resources.Load<Sprite>(family.source.resource);
                var renderer = Create(actor.transform, source, 1.35f, 2.25f, 0f);
                var animation = Animation(renderer, source);
                cached = (Sprite[])animationType.GetField("frames", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(animation);
                saved = (Sprite[])cached.Clone();
                Array.Clear(cached, 0, cached.Length);
                Step(animation, cameraObject.AddComponent<Camera>(), Vector3.right, .2f);
                Assert.AreSame(source, renderer.sprite);
                Assert.IsTrue(renderer.flipX);
                AssertGeometry(renderer, family.source, 1.35f, 2.25f, 0f, sourceName);
                Assert.That(renderer.transform.localScale.x,
                    Is.EqualTo(Mathf.Min(1.35f * source.pixelsPerUnit / family.source.visibleHeight,
                                         2.25f * source.pixelsPerUnit / family.source.visibleWidth)).Within(.00001f));
            }
            finally
            {
                if (saved != null) Array.Copy(saved, cached, saved.Length);
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void CroppedAndPaddedAliasesHaveIdenticalAnimationScale()
        {
            var actor = new GameObject("alias actor");
            try
            {
                var cropped = Resources.Load<Sprite>("PokemonSprites/poke_052");
                var padded = Resources.Load<Sprite>("PokemonSprites/species_mew");
                var first = Create(actor.transform, cropped, 1.35f, 2.25f, 0f);
                var second = Create(actor.transform, padded, 1.35f, 2.25f, 0f);
                Step(Animation(first, cropped), null, Vector3.zero, 0f);
                Step(Animation(second, padded), null, Vector3.zero, 0f);
                Assert.AreEqual(first.transform.localScale, second.transform.localScale);
                Assert.AreEqual(first.transform.localPosition.y, second.transform.localPosition.y);
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }

        [Test]
        public void SpeciesScaleRestoresAfterAnExplicitVisualOverride()
        {
            var actor = new GameObject("boss profile actor");
            try
            {
                var source = Resources.Load<Sprite>("PokemonPortraits/species_rayquaza");
                var enemyType = visuals.Assembly.GetType("Game.Pokemon.PokemonEnemyVisual");
                var visual = enemyType.GetMethod("Attach").Invoke(null, new object[] { actor, source });
                var renderer = actor.GetComponentsInChildren<SpriteRenderer>().Single(r => r.sprite == source);
                var family = metrics.families.Single(f => f.name == source.name);
                AssertGeometry(renderer, family.source, 7f, 20f, 0f, "Rayquaza species height");
                float normal = renderer.transform.localScale.x;
                enemyType.GetMethod("SetVisualProfile").Invoke(visual, new object[] { 8f, 20f });
                Assert.Greater(renderer.transform.localScale.x, normal);
                Assert.AreSame(source, renderer.sprite);
                Assert.AreEqual(Vector3.one, actor.transform.localScale);
                AssertGeometry(renderer, family.source, 8f, 20f, 0f, "explicit override");
                Step(Animation(renderer, source), null, Vector3.zero, 0f);
                AssertGeometry(renderer, family.frames[0], 8f, 20f, 0f, "animated override");
                enemyType.GetMethod("ApplySpeciesScale").Invoke(visual, null);
                AssertGeometry(renderer, family.frames[0], 7f, 20f, 0f, "species restored on animated frame");
                float expected = 7f * renderer.sprite.pixelsPerUnit / family.maxVisibleHeight;
                Assert.That(renderer.transform.localScale.x, Is.EqualTo(expected).Within(.00001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }

        [Test]
        public void EverySpeciesReachesItsPokedexHeightAndKeepsAllSixFramesGrounded()
        {
            var sizesAsset = Resources.Load<TextAsset>("PokemonPhysicalSizes");
            Assert.IsNotNull(sizesAsset);
            var sizes = JsonUtility.FromJson<PhysicalSizes>(sizesAsset.text).families;
            CollectionAssert.AreEquivalent(metrics.families.Select(f => f.name), sizes.Select(f => f.name));
            var sizeType = visuals.Assembly.GetType("Game.Pokemon.PokemonPhysicalSize");
            var enemyType = visuals.Assembly.GetType("Game.Pokemon.PokemonEnemyVisual");
            var cameraObject = new GameObject("physical size camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                foreach (var family in metrics.families)
                {
                    var actor = new GameObject("size " + family.name);
                    try
                    {
                        var source = Resources.Load<Sprite>(family.source.resource);
                        float height = sizes.Single(s => s.name == family.name).heightMeters;
                        float width = (float)sizeType.GetMethod("WidthFor").Invoke(null, new object[] { source, height });
                        enemyType.GetMethod("Attach").Invoke(null, new object[] { actor, source });
                        var renderer = actor.GetComponentsInChildren<SpriteRenderer>().Single(r => r.sprite == source);
                        Assert.That(family.source.visibleHeight * renderer.transform.localScale.y / source.pixelsPerUnit,
                                    Is.EqualTo(height).Within(.00001f), family.name);
                        var animation = Animation(renderer, source);
                        var directions = new[] { Vector3.back, Vector3.forward, Vector3.left };
                        for (int view = 0; view < 3; view++)
                        for (int phase = 0; phase < 2; phase++)
                        {
                            Step(animation, camera, directions[view], phase == 0 ? 0f : .17f);
                            AssertGeometry(renderer, family.frames[view * 2 + phase], height, width, 0f, family.name);
                            Assert.That(family.maxVisibleHeight * renderer.transform.localScale.y / source.pixelsPerUnit,
                                        Is.EqualTo(height).Within(.00001f), family.name);
                        }
                        var shadow = actor.transform.Find("Ground Contact");
                        Assert.That(shadow.localScale.x, Is.EqualTo(height * .65f).Within(.00001f), family.name);
                        Assert.AreEqual(Vector3.one, actor.transform.localScale);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
        }

        [TestCase("species_ditto", .3f)]
        [TestCase("species_charizard", 1.7f)]
        [TestCase("species_gyarados", 6.5f)]
        [TestCase("species_rayquaza", 7f)]
        public void NormalAndBossInitializationKeepSpeciesHeightAndHealthBarAboveIt(string name, float height)
        {
            var family = metrics.families.Single(f => f.name == name);
            var source = Resources.Load<Sprite>(family.source.resource);
            var factory = visuals.Assembly.GetType("Game.Enemies.EnemyTriangleFactory");
            var actor = (GameObject)factory.GetMethod("Create", new[] { typeof(int), typeof(Sprite) })
                .Invoke(null, new object[] { 1, source });
            try
            {
                var enemyType = visuals.Assembly.GetType("Game.Enemies.EnemyTriangle");
                var enemy = actor.GetComponent(enemyType);
                // EditMode AddComponent does not run this gameplay component's Awake.
                if (actor.transform.Find("HealthBar") == null)
                    enemyType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
                var reset = enemyType.GetMethod("ResetCombatState", BindingFlags.Instance | BindingFlags.NonPublic);
                var renderer = actor.GetComponentsInChildren<SpriteRenderer>().Single(r => r.sprite == source);
                foreach (bool boss in new[] { true, false, true })
                {
                    reset.Invoke(enemy, new object[] { boss, 1f });
                    Assert.That(family.source.visibleHeight * renderer.transform.localScale.y / source.pixelsPerUnit,
                                Is.EqualTo(height).Within(.00001f), name);
                    Assert.That(actor.transform.Find("HealthBar").localPosition.y,
                                Is.EqualTo(height + .27f).Within(.00001f), name);
                    Assert.AreSame(source, renderer.sprite);
                    Assert.AreEqual(Vector3.one, actor.transform.localScale);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }

        [Test]
        public void UnknownSourceUsesNonReadableFullRectFallback()
        {
            var actor = new GameObject("unknown source");
            var texture = new Texture2D(4, 2);
            texture.filterMode = FilterMode.Point;
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 2), Vector2.one * .5f, 32);
            sprite.name = "unknown-source-metrics-test";
            try
            {
                var renderer = Create(actor.transform, sprite, 1.35f, 2.25f, -.5f);
                var animation = Animation(renderer, sprite);
                Step(animation, null, Vector3.zero, 0f);
                Assert.AreSame(sprite, renderer.sprite);
                AssertGeometry(renderer, new PixelBounds {
                    width = 4, height = 2, visibleWidth = 4, visibleHeight = 2
                }, 1.35f, 2.25f, -.5f, sprite.name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
