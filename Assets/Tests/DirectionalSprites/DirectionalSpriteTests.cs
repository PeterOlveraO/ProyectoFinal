using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.DirectionalSprites.Tests
{
    public class DirectionalSpriteTests
    {
        Type visuals;
        Type facing;
        MethodInfo direction;

        [SetUp]
        public void SetUp()
        {
            // Works both before and after the runtime assembly is moved into an asmdef.
            visuals = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Game.Pokemon.PokemonVisuals"))
                .First(t => t != null);
            facing = visuals.GetNestedType("Facing");
            direction = visuals.GetMethod("Direction");
        }

        string Resolve(Vector3 movement, string previous)
        {
            return direction.Invoke(null, new object[] {
                movement, Vector3.right, Vector3.forward, Enum.Parse(facing, previous)
            }).ToString();
        }

        [TestCase(1f, 0f, "Right")]
        [TestCase(-1f, 0f, "Left")]
        [TestCase(0f, 1f, "Back")]
        [TestCase(0f, -1f, "Front")]
        [TestCase(0.2f, 1f, "Back")]
        [TestCase(-1f, -0.2f, "Left")]
        public void MovementSelectsCameraRelativeView(float x, float z, string expected)
        {
            Assert.AreEqual(expected, Resolve(new Vector3(x, 0f, z), "Front"));
        }

        [Test]
        public void IdleKeepsLastView()
        {
            Assert.AreEqual("Back", Resolve(Vector3.zero, "Back"));
            Assert.AreEqual("Left", Resolve(Vector3.up, "Left"));
        }

        [Test]
        public void MissingFramesKeepTheAssignedPokemon()
        {
            var actor = new GameObject("sprite test");
            var cameraObject = new GameObject("camera test");
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            sprite.name = "missing-directional-test";
            try
            {
                var renderer = actor.AddComponent<SpriteRenderer>();
                var camera = cameraObject.AddComponent<Camera>();
                var type = visuals.GetNestedType("DirectionalAnimation");
                var animation = Activator.CreateInstance(type, renderer, sprite);
                var update = type.GetMethod("Update");
                update.Invoke(animation, new object[] { Vector3.forward, camera, 0.2f });
                Assert.AreSame(sprite, renderer.sprite);
                update.Invoke(animation, new object[] { Vector3.left, camera, 0.2f });
                Assert.AreSame(sprite, renderer.sprite);
                Assert.IsTrue(renderer.flipX);
                update.Invoke(animation, new object[] { Vector3.zero, camera, 0.2f });
                Assert.IsTrue(renderer.flipX);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
