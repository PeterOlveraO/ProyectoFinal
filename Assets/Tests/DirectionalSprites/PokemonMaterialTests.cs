using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.DirectionalSprites.Tests
{
    public sealed class PokemonMaterialTests
    {
        static SpriteRenderer Create(Transform parent, Sprite sprite, float height, float width)
        {
            var visuals = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Game.Pokemon.PokemonVisuals")).First(t => t != null);
            var create = visuals.GetMethod("CreateBillboard", new[] {
                typeof(Transform), typeof(string), typeof(Sprite), typeof(float),
                typeof(bool), typeof(float), typeof(float)
            });
            return (SpriteRenderer)create.Invoke(null,
                new object[] { parent, "material probe", sprite, height, false, width, 0f });
        }

        [Test]
        public void PlayerEnemyAndBossProfilesShareFogMaterialWithoutChangingContactShadows()
        {
            var actor = new GameObject("material profiles");
            try
            {
                var sprite = Resources.Load<Sprite>("PokemonSprites/species_charizard");
                Assert.That(sprite, Is.Not.Null);
                var player = Create(actor.transform, sprite, 1.65f, 2.75f);
                var enemy = Create(actor.transform, sprite, 1.35f, 2.25f);
                var boss = Create(actor.transform, sprite, 1.9f, 3.2f);
                var shared = player.sharedMaterial;
                Assert.That(shared, Is.SameAs(Resources.Load<Material>("PokemonWorldSprite")));
                Assert.That(UnityEditor.AssetDatabase.GetAssetPath(shared),
                    Is.EqualTo("Assets/Resources/PokemonWorldSprite.mat"));
                Assert.That(shared.shader, Is.SameAs(Resources.Load<Shader>("PokemonWorldSprite")));
                Assert.That(shared.shader.name, Is.EqualTo("Game/Pokemon/WorldSpriteFog"));
                Assert.That(enemy.sharedMaterial, Is.SameAs(shared));
                Assert.That(boss.sharedMaterial, Is.SameAs(shared));
                Assert.That(shared.renderQueue, Is.EqualTo(3000));
                Assert.That(shared.FindPass("ShadowCaster"), Is.EqualTo(-1));
                var shadow = actor.GetComponentsInChildren<SpriteRenderer>()
                    .Single(r => r.gameObject.name == "Ground Contact");
                Assert.That(shadow.sharedMaterial, Is.Not.SameAs(shared));
                Assert.That(shadow.sortingOrder, Is.EqualTo(-2));
                enemy.color = Color.red;
                enemy.flipX = true;
                Assert.That(player.color, Is.EqualTo(Color.white));
                Assert.That(boss.color, Is.EqualTo(Color.white));
                Assert.That(enemy.sharedMaterial, Is.SameAs(shared), "Tint/flip must not instantiate a material.");
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            }
            finally { Object.DestroyImmediate(actor); }
        }

        [Test]
        public void ReconfiguringDestroyingAndRecreatingBillboardsReuseTheResourcesMaterialAsset()
        {
            var first = new GameObject("first material borrower");
            var second = new GameObject("second material borrower");
            GameObject recreated = null;
            try
            {
                var shared = Resources.Load<Material>("PokemonWorldSprite");
                Assert.That(shared, Is.Not.Null, "Create the shared material asset before running these tests.");
                Assert.That(UnityEditor.AssetDatabase.GetAssetPath(shared),
                    Is.EqualTo("Assets/Resources/PokemonWorldSprite.mat"));
                var shader = shared.shader;
                var texture = shared.GetTexture("_MainTex");
                Color tint = shared.GetColor("_Color");
                int queue = shared.renderQueue;
                var sprite = Resources.Load<Sprite>("PokemonSprites/species_charizard");
                var a = Create(first.transform, sprite, 1.35f, 2.25f);
                var b = Create(second.transform, sprite, 1.65f, 2.75f);
                Assert.That(a.sharedMaterial, Is.SameAs(shared), "Use the asset itself, never a runtime clone.");
                Assert.That(b.sharedMaterial, Is.SameAs(shared));
                var layout = a.GetComponents<MonoBehaviour>()
                    .Single(c => c.GetType().Name == "PokemonBillboardLayout");
                for (int i = 0; i < 3; i++)
                {
                    layout.GetType().GetMethod("Configure", BindingFlags.Instance | BindingFlags.Public)
                        .Invoke(layout, new object[] { a, sprite, 1.35f, 2.25f, 0f });
                    Assert.That(a.sharedMaterial, Is.SameAs(shared));
                }
                a.color = Color.red;
                a.flipX = true;
                first.SetActive(false);
                Assert.That(shared != null, Is.True);
                first.SetActive(true);
                Assert.That(a.sharedMaterial, Is.SameAs(shared));
                Object.DestroyImmediate(first);
                Assert.That(shared != null, Is.True);
                Assert.That(b.sharedMaterial, Is.SameAs(shared));
                Object.DestroyImmediate(second);
                Assert.That(shared != null, Is.True, "Destroying every borrower must leave the Resources asset alive.");
                Assert.That(Resources.Load<Material>("PokemonWorldSprite"), Is.SameAs(shared));

                recreated = new GameObject("recreated material borrower");
                var next = Create(recreated.transform, sprite, 1.9f, 3.2f);
                Assert.That(next.sharedMaterial, Is.SameAs(shared));
                Assert.That(next.color, Is.EqualTo(Color.white));
                Object.DestroyImmediate(recreated);
                Assert.That(shared != null, Is.True);
                Assert.That(shared.shader, Is.SameAs(shader));
                Assert.That(shared.GetTexture("_MainTex"), Is.SameAs(texture));
                Assert.That(shared.GetColor("_Color"), Is.EqualTo(tint));
                Assert.That(shared.renderQueue, Is.EqualTo(queue), "Billboard lifecycle must not mutate the asset.");
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
                if (recreated != null) Object.DestroyImmediate(recreated);
            }
        }
    }
}
