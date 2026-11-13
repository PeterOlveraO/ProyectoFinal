using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Terrain.Tests
{
    public sealed class ProjectileCollisionTests
    {
        static MethodInfo SegmentHit
        {
            get
            {
                var type = Type.GetType("Game.Combat.AttackProjectile, Assembly-CSharp");
                Assert.NotNull(type, "El proyectil debe compilar en la build del juego.");
                var method = type.GetMethod("SegmentHit", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(method);
                return method;
            }
        }

        static bool Hits(Vector3 start, Vector3 end, Vector3 center, out float time)
        {
            object[] args = { start, end, center, 0f };
            bool hit = (bool)SegmentHit.Invoke(null, args);
            time = (float)args[3];
            return hit;
        }

        [Test]
        public void CrossingEntireEnemyInOneFrameStillHits()
        {
            Assert.IsTrue(Hits(Vector3.zero, new Vector3(2f, 0f, 0f),
                               new Vector3(1f, 0f, 0f), out float time));
            Assert.That(time, Is.InRange(0f, 0.5f));
        }

        [Test]
        public void SameHorizontalPathOnDifferentFloorMisses()
        {
            Assert.IsFalse(Hits(Vector3.zero, new Vector3(2f, 0f, 0f),
                                new Vector3(1f, 4f, 0f), out _));
        }

        [Test]
        public void EnemyPastProjectileRangeMisses()
        {
            Assert.IsFalse(Hits(Vector3.zero, new Vector3(.2f, 0f, 0f),
                                new Vector3(2f, 0f, 0f), out _));
        }
    }
}
