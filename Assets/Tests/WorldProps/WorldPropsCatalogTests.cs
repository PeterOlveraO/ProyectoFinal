using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.WorldProps.Tests
{
    public class WorldPropsCatalogTests
    {
        static Type Decorator()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("Game.Pokemon.WorldDecorator");
                if (type != null) return type;
            }
            Assert.Fail("WorldDecorator no está cargado");
            return null;
        }

        [TestCase("CatalogBuildings")]
        [TestCase("CatalogExtras")]
        [TestCase("CatalogTrees")]
        [TestCase("CatalogProps")]
        public void CatalogProfilesReferenceRealSpritesAndPositiveFootprints(string method)
        {
            var catalog = (IEnumerable)Decorator().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            int count = 0;
            foreach (var profile in catalog)
            {
                Type type = profile.GetType();
                string path = (string)type.GetField("Path").GetValue(profile);
                Assert.That(Resources.Load<Sprite>(path), Is.Not.Null, path);
                Assert.That((float)type.GetField("Footprint").GetValue(profile), Is.GreaterThan(0), path);
                Assert.That((float)type.GetField("ColliderHeight").GetValue(profile), Is.GreaterThan(0), path);
                count++;
            }
            Assert.That(count, Is.GreaterThan(0));
        }

        [Test]
        public void FragmentedFloorAndEmptyTreeAreNotCatalogued()
        {
            foreach (string method in new[] { "CatalogBuildings", "CatalogExtras", "CatalogTrees", "CatalogProps" })
            {
                var catalog = (IEnumerable)Decorator().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                foreach (var profile in catalog)
                {
                    string path = (string)profile.GetType().GetField("Path").GetValue(profile);
                    Assert.That(path, Is.Not.EqualTo("Decor/Trees/tree_23"));
                    Assert.That(path, Is.Not.EqualTo("Decor/Props/prop_26"));
                    Assert.That(path, Is.Not.EqualTo("Decor/Props/prop_38"));
                    Assert.That(path, Is.Not.EqualTo("Decor/Props/prop_44"));
                }
            }
        }
    }
}
