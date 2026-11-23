using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Player;
using Game.Pokemon;
using Game.Pokemon.Progression;
using Game.Rounds;
using Game.Terrain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class DesktopCombatTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> _owned = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();
        static readonly FieldInfo CollectionService = typeof(PokemonCollection).GetField("service",
            BindingFlags.Static | BindingFlags.NonPublic);
        object _previousService;
        bool _replacedService;
        Keyboard _keyboard, _previousKeyboard;
        Mouse _mouse, _previousMouse;
        PlayerAttack _attack;
        bool _blocked, _over;

        GameObject Create(string name)
        {
            var go = new GameObject(name);
            _owned.Add(go);
            return go;
        }
        static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Private).Invoke(target, args);
        EnemyTriangle Enemy(Vector3 position)
        {
            var go = Create("AimEnemy");
            go.transform.position = position;
            var enemy = go.AddComponent<EnemyTriangle>();
            enemy.Initialize(1, 2);
            return enemy;
        }
        bool Aim(out Vector3 direction)
        {
            object[] args = { Vector3.zero };
            bool result = (bool)Call(_attack, "TryReadAim", args);
            direction = (Vector3)args[0];
            return result;
        }
        void Space(bool pressed)
        {
            InputSystem.QueueStateEvent(_keyboard, pressed ? new KeyboardState(Key.Space) : new KeyboardState());
            InputSystem.Update();
            _keyboard.MakeCurrent();
        }
        void Tick(float seconds)
        {
            bool aiming = Aim(out var direction);
            Call(_attack, "TickAttack", seconds, aiming, direction);
        }
        static int Projectiles() => Object.FindObjectsByType<AttackProjectile>(FindObjectsInactive.Include).Length;

        [SetUp]
        public void Setup()
        {
            Assert.IsNull(RoundManager.Instance);
            Assert.IsNull(PlayerSphereController.Instance);
            _blocked = GameFlow.Blocked; _over = GameFlow.Over;
            GameFlow.Blocked = GameFlow.Over = false;
            _previousKeyboard = Keyboard.current; _previousMouse = Mouse.current;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _attack = Create("DesktopPlayer").AddComponent<PlayerAttack>();
            _attack.enabled = false;
            _attack.ResetRun();
        }
        [TearDown]
        public void Cleanup()
        {
            foreach (var projectile in Object.FindObjectsByType<AttackProjectile>(FindObjectsInactive.Include))
                Object.DestroyImmediate(projectile.gameObject);
            foreach (var burst in Object.FindObjectsByType<HitBurst>(FindObjectsInactive.Include))
                Object.DestroyImmediate(burst.gameObject);
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            foreach (var asset in _assets)
                if (asset != null) Object.DestroyImmediate(asset);
            _assets.Clear();
            if (_replacedService) CollectionService.SetValue(null, _previousService);
            _replacedService = false;
            InputSystem.RemoveDevice(_keyboard); InputSystem.RemoveDevice(_mouse);
            if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
            if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
            GameFlow.Blocked = _blocked; GameFlow.Over = _over;
        }

        [Test]
        public void HeldSpaceChoosesNearestLiveTargetAndReleaseStopsAiming()
        {
            Enemy(Vector3.forward * 8);
            var near = Enemy(Vector3.right * 3);
            Space(true);
            Assert.IsTrue(Aim(out var axis));
            Assert.Less(Vector3.Distance(axis, Vector3.right), .0001f);
            near.enabled = false;
            Assert.IsTrue(Aim(out axis));
            Assert.Less(Vector3.Distance(axis, Vector3.forward), .0001f);
            Space(false);
            Assert.IsFalse(Aim(out _));
        }

        [Test]
        public void AutoAimRejectsOccludedTooHighAndOutOfRangeTargets()
        {
            var hidden = Enemy(Vector3.right * 3);
            var wall = Create("AimWall");
            wall.transform.position = new Vector3(1.5f, 1, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(.4f, 3, 2);
            Enemy(new Vector3(0, 5, 2));
            Enemy(Vector3.back * 25);
            var clear = Enemy(Vector3.forward * 7);
            Physics.SyncTransforms();
            Space(true);
            Assert.IsTrue(Aim(out var axis));
            Assert.Less(Vector3.Distance(axis, Vector3.forward), .0001f);
            clear.enabled = false;
            Assert.IsFalse(Aim(out _));
            hidden.enabled = false;
            Assert.IsFalse(Aim(out _));
        }

        sealed class MemoryStore : IProgressionStore
        {
            string _data;
            public string Read() => _data;
            public void Write(string expected, string next)
            {
                Assert.AreEqual(expected, _data);
                _data = next;
            }
        }

        WorldTerrain ContactCombatWorld()
        {
            Assert.IsNull(WorldTerrain.Instance);
            var settings = ScriptableObject.CreateInstance<TerrainSettings>();
            _assets.Add(settings);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _assets.Add(material);
            settings.terrainMaterial = material;
            settings.chunksX = settings.chunksZ = 4;
            settings.chunkSize = 16;
            settings.flatRadius = 1000;
            settings.flatLevel = 1;
            settings.lakeCount = 0;
            settings.enableWorldZones = true;
            settings.loadRadius = 0;
            var world = Create("DesktopContactWorld");
            world.SetActive(false);
            var terrain = world.AddComponent<WorldTerrain>();
            typeof(WorldTerrain).GetField("settings", Private).SetValue(terrain, settings);
            world.SetActive(true);
            terrain.enabled = false;

            var capsule = _attack.gameObject.AddComponent<CharacterController>();
            capsule.height = 1;
            capsule.radius = .5f;
            capsule.center = Vector3.zero;
            capsule.skinWidth = .02f;
            capsule.stepOffset = .55f;
            var player = _attack.gameObject.AddComponent<PlayerSphereController>();
            player.enabled = false;
            terrain.Player = player.transform;

            var catalog = JsonUtility.FromJson<SpeciesCatalog>(Resources.Load<TextAsset>("PokemonProgression/catalog").text);
            var config = JsonUtility.FromJson<GachaConfig>(Resources.Load<TextAsset>("PokemonProgression/gacha").text);
            config.starterF = 1;
            config.starterA = config.starterS = 0;
            var progression = new ProgressionService(new MemoryStore(), catalog, config, random: new System.Random(123));
            Assert.IsTrue(progression.ChooseStarter("jolteon").success);
            _previousService = CollectionService.GetValue(null);
            _replacedService = true;
            CollectionService.SetValue(null, progression);
            var round = Create("DesktopContactRound").AddComponent<RoundManager>();
            round.enabled = false;
            round.Drinks.enabled = false;
            round.ShotStations.enabled = false;
            round.StartRun();
            Assert.IsTrue(round.IsRunActive, round.LastRunStartError);
            Assert.AreEqual(Rarity.F, round.RunPokemon.rarity);
            Assert.AreEqual("jolteon", round.RunPokemon.speciesId);
            Assert.AreEqual(2, round.CombatStats.damage, "Use the real catalog, nature and combat conversion.");
            Assert.AreEqual(round.CombatStats.damage, _attack.Damage);
            Assert.AreEqual(terrain.SpawnPosition.y + .52f, player.transform.position.y, .001f);
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(terrain.SpawnPosition + Vector3.right * 2 + Vector3.up * 2,
                Vector3.down, out var groundHit, 3, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
            Assert.IsInstanceOf<MeshCollider>(groundHit.collider, "Exercise the generated ground collider.");
            Assert.AreEqual(terrain.SpawnPosition.y, groundHit.point.y, .001f);
            return terrain;
        }

        [TestCase(.8f)]
        [TestCase(0f)]
        public void HeldSpaceKillsContactEnemyInSevenRealHitsOnGeneratedTerrain(float distance)
        {
            var terrain = ContactCombatWorld();
            var enemy = Enemy(terrain.SpawnPosition + Vector3.right * distance);
            enemy.InitializeRound(1, RoundDifficulty.Stats(1), false);
            Assert.AreEqual(14, enemy.Health);
            Physics.SyncTransforms();
            Space(true);
            Assert.IsTrue(Aim(out var axis), "Contact/overlap must remain targetable.");
            Assert.Less(Vector3.Distance(axis, distance == 0 ? Vector3.forward : Vector3.right), .0001f);

            int hits = 0;
            const float step = 1f / 60f;
            // Same attack/projectile tick paths, with deterministic time and no unrelated spawning.
            for (int frame = 0; frame < 360 && !enemy.IsDead; frame++)
            {
                int previous = enemy.Health;
                Tick(step);
                foreach (var projectile in Object.FindObjectsByType<AttackProjectile>())
                    Call(projectile, "TickProjectile", step);
                if (enemy.Health == previous) continue;
                hits++;
                Assert.AreEqual(2, previous - enemy.Health, "Each impact must apply the unmodified run damage.");
                Assert.AreEqual(14 - hits * 2, enemy.Health);
                Assert.AreEqual(hits == 7, enemy.IsDead);
            }
            Assert.AreEqual(7, hits);
            Assert.IsTrue(enemy.IsDead);
            Space(false);
            Assert.IsFalse(Aim(out _));
        }

        [Test]
        public void CoincidentAimStillChecksHeightTerritoryAndLineOfSight()
        {
            var terrain = ContactCombatWorld();
            var enemy = Enemy(terrain.SpawnPosition);
            Vector3 origin = _attack.transform.position + Vector3.up * .62f;
            Assert.IsTrue(AutoAimTargeting.TryDirection(origin, 18, out var axis));
            Assert.AreEqual(Vector3.forward, axis);

            enemy.transform.position += Vector3.up * 5;
            Assert.IsFalse(AutoAimTargeting.TryDirection(origin, 18, out _), "Overlap cannot bypass height.");
            enemy.transform.position = terrain.SpawnPosition;
            var blocker = Create("CoincidentBodyOccluder").AddComponent<BoxCollider>();
            blocker.size = new Vector3(.2f, .1f, .2f);
            blocker.transform.position = new Vector3(origin.x, origin.y - .25f, origin.z);
            Physics.SyncTransforms();
            Assert.IsFalse(AutoAimTargeting.TryDirection(origin, 18, out _), "The vertical body ray remains occluded.");
            blocker.enabled = false;
            Physics.SyncTransforms();
            Assert.IsTrue(AutoAimTargeting.TryDirection(origin, 18, out _));

            Rect locked = terrain.Traversal.Layout.Bounds(3);
            var lockedFeet = new Vector3(locked.center.x, terrain.SpawnPosition.y, locked.center.y);
            enemy.transform.position = lockedFeet;
            Assert.IsFalse(AutoAimTargeting.TryDirection(lockedFeet + Vector3.up * 1.14f, 18, out _),
                "Even a zero-length XZ segment cannot acquire inside a locked territory.");
        }

        [Test]
        public void SpaceWithoutAnEnemyDoesNotEmitProjectiles()
        {
            Space(true);
            Tick(5);
            Assert.AreEqual(0, Projectiles());
        }

        [Test]
        public void ContinuousFireUsesFasterCadenceAndStopsOnReleasePauseOrFocusLoss()
        {
            Enemy(Vector3.forward * 7);
            Space(true);
            Tick(0);
            Assert.AreEqual(1, Projectiles());
            Tick(.65f);
            Assert.AreEqual(1, Projectiles());
            Tick(.02f);
            Assert.AreEqual(2, Projectiles(), "1.5 shots per second instead of 1.");
            Space(false); Tick(2);
            Assert.AreEqual(2, Projectiles());
            Space(true); GameFlow.Blocked = true; Tick(2);
            Assert.AreEqual(2, Projectiles());
            GameFlow.Blocked = false;
            Call(_attack, "OnApplicationFocus", false); Tick(2);
            Assert.AreEqual(2, Projectiles());
            Call(_attack, "OnApplicationFocus", true); Tick(0);
            Assert.AreEqual(3, Projectiles());
        }

        [Test]
        public void SelectedTextInputDoesNotStartAutoFire()
        {
            Enemy(Vector3.forward * 3);
            var events = Create("DesktopEvents").AddComponent<EventSystem>();
            var input = new GameObject("Amount", typeof(RectTransform), typeof(TMPro.TMP_InputField));
            _owned.Add(input);
            events.SetSelectedGameObject(input);
            Space(true);
            Assert.IsFalse(Aim(out _));
            events.SetSelectedGameObject(null);
            Assert.IsTrue(Aim(out _));
        }

        [Test]
        public void MovementMultiplierComposesWithHasteAndDoesNotAccumulateOnEnemyReuse()
        {
            var player = Create("PlayerSpeed").AddComponent<PlayerSphereController>();
            player.enabled = false;
            Assert.AreEqual(7.5f, player.MovementSpeed, .001f);
            player.RunEffects.Configure(() => true, _ => { }, () => false, () => false);
            Assert.IsTrue(player.RunEffects.Apply(TemporaryEffect.Haste));
            Assert.AreEqual(9f, player.MovementSpeed, .001f);
            var enemy = Enemy(Vector3.forward * 3);
            Assert.AreEqual(3f, enemy.Speed, .001f);
            enemy.Initialize(1, 2);
            Assert.AreEqual(3f, enemy.Speed, .001f);
            enemy.InitializeRound(1, new EnemyRoundStats { speed = 4, health = 100, contactDamage = 10 }, true);
            Assert.AreEqual(6f, enemy.Speed, .001f);
        }
    }
}
