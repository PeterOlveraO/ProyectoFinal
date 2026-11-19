using System;
using Game.Combat;
using Game.Core;
using Game.Economy;
using Game.Enemies;
using Game.Player;
using Game.Player.ProgressionCombat;
using Game.Pokemon;
using Game.Pokemon.Progression;
using Game.Terrain;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>
    /// Sistema de rondas con economía.
    ///
    /// Oleadas normales con dificultad acotada por ronda; cada quinta ronda contiene
    /// un único jefe mítico/legendario del catálogo, sin refuerzos.
    ///
    /// Economía:
    ///   · Puntos (interna de la partida): +10 por enemigo y +50 por ronda superada
    ///     (más 10 extra por cada ronda adicional). Se reinicia en cada run.
    ///   · Monedas: +1 por enemigo y +15 por ronda superada (más 5 extra por ronda).
    ///     Se acumulan aquí y se depositan una sola vez al terminar la partida.
    ///
    /// Si el jugador llega a 0 de vida la run termina y se muestra la pantalla de derrota.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoundManager : MonoBehaviour
    {
        public enum RoundPhase { Idle, Preparing, Fighting, Clearing, GameOver }

        public static RoundManager Instance { get; private set; }

        [Header("Rondas")]
        [Tooltip("Enemigos que hay que eliminar en la ronda 1.")]
        [SerializeField, Min(1)] int baseEnemies = 12;

        [Tooltip("Enemigos que se suman en cada ronda.")]
        [SerializeField, Min(0)] int enemiesPerRound = 4;

        [Tooltip("Tope de enemigos por ronda para que las rondas no se hagan eternas.")]
        [SerializeField, Min(1)] int maxEnemiesPerRound = 96;

        [Tooltip("Enemigos vivos a la vez en la ronda 1.")]
        [SerializeField, Min(1)] int baseMaxAlive = 8;

        [Tooltip("Cada cuántas rondas aumenta el límite de enemigos vivos.")]
        [SerializeField, Min(1)] int maxAliveGrowthEvery = 2;

        [Tooltip("Cuántos enemigos vivos se suman en cada incremento.")]
        [SerializeField, Min(0)] int maxAliveGrowth = 2;

        [Tooltip("Tope de enemigos vivos simultáneos.")]
        [SerializeField, Min(1)] int maxAliveCap = 28;

        [Tooltip("Segundos de preparación antes de cada ronda.")]
        [SerializeField, Min(0.5f)] float preparationDuration = 4f;

        [Tooltip("Segundos que se muestra el cartel de ronda superada.")]
        [SerializeField, Min(0.2f)] float clearingDuration = 2.5f;

        [Header("Aparición")]
        [Tooltip("Distancia mínima al jugador a la que aparecen los enemigos.")]
        [SerializeField, Min(2f)] float spawnRadiusMin = 10f;

        [Tooltip("Distancia máxima al jugador a la que aparecen los enemigos.")]
        [SerializeField, Min(3f)] float spawnRadiusMax = 17f;

        [Tooltip("Desnivel máximo entre el jugador y el punto de aparición.")]
        [SerializeField, Min(0.5f)] float maxSpawnHeightDelta = 1.9f;

        [Tooltip("Segundos entre apariciones al empezar la ronda 1.")]
        [SerializeField, Min(0.1f)] float spawnInterval = 1f;

        [Tooltip("Intervalo mínimo de aparición en rondas altas.")]
        [SerializeField, Min(0.05f)] float minSpawnInterval = 0.35f;

        [Header("Dificultad")]
        [Tooltip("Rondas por cada nivel de zona (metadatos; stats en RoundDifficulty).")]
        [SerializeField, Min(1)] int roundsPerTier = 4;

        [Tooltip("Nivel de zona máximo; no modifica los stats de RoundDifficulty.")]
        [SerializeField, Min(1)] int maxTier = 8;

        // RoundDifficulty owns speed/health/damage, including scenes serialized before #29.

        [Header("Recompensas")]
        [SerializeField, Min(0)] int pointsPerKill = 10;
        [SerializeField, Min(0)] int pointsPerRound = 50;
        [SerializeField, Min(0)] int pointsPerRoundGrowth = 10;
        [SerializeField, Min(0)] int coinsPerKill = 1;
        [SerializeField, Min(0)] int coinsPerRound = 15;
        [SerializeField, Min(0)] int coinsPerRoundGrowth = 5;

        RoundPhase _phase = RoundPhase.Idle;
        float _prepRemaining;
        float _clearRemaining;
        float _spawnTimer;
        int _spawnedThisRound;
        int _defeatedThisRound;
        readonly int[] _evLevels = new int[4];
        bool _rewardsPaid;
        DrinkStations _drinks;
        ShotStations _shotStations;
        readonly ShotLoadout _shots = new ShotLoadout();
        CombatBalance _balance;
        PokemonInstance _runPokemon;
        SpeciesDefinition _runSpecies;
        StatBlock _runStats;
        SpeciesDefinition _bossSpecies;
        EnemyTriangle _bossEnemy;
        CombatStatBlock _combatStats;
        readonly System.Collections.Generic.HashSet<EnemyTriangle> _creditedEnemies = new System.Collections.Generic.HashSet<EnemyTriangle>();

        public DrinkStations Drinks => _drinks;
        public ShotStations ShotStations => _shotStations;
        public ShotLoadout Shots => _shots;

        public bool EquipShot(ShotType type) => false; // No inventory/free switching.

        public bool BuyShot(int offer)
        {
            if (!CanUseShotShop() || _shotStations == null ||
                _shotStations.NearbyOffer(PlayerSphereController.Instance.transform.position) != offer ||
                offer < 0 || offer >= Game.Rounds.ShotStations.OfferCount || !_shotStations.IsBuilt(offer)) return false;
            if (!_shots.TryBuyAndEquip(Game.Rounds.ShotStations.OfferType(offer), SpendPoints)) return false;
            PlayerSphereController.Instance.GetComponent<PlayerAttack>()?.CancelPendingShots();
            Changed?.Invoke();
            return true;
        }

        bool CanUseShotShop() => GameFlow.Playing && IsRunActive &&
            PlayerSphereController.Instance != null && PlayerSphereController.Instance.IsAlive;
        public bool HasRunPokemon => _runPokemon != null;
        public PokemonInstance RunPokemon => _runPokemon?.Copy();
        public SpeciesDefinition RunSpecies => _runSpecies == null ? null : new SpeciesDefinition
        {
            id = _runSpecies.id, name = _runSpecies.name, category = _runSpecies.category,
            spriteName = _runSpecies.spriteName, baseStats = _runSpecies.baseStats
        };
        public StatBlock RunStats => _runStats;
        public CombatStatBlock CombatStats => _combatStats;
        public float MaxDamageReduction => _balance?.maxTotalDamageReduction ?? .65f;
        public string LastRunStartError { get; private set; }
        public bool RewardSaveFailed { get; private set; }

        public RoundPhase Phase => _phase;
        public int Round { get; private set; }
        public bool IsBossRound => IsRunActive && RoundDifficulty.IsBoss(Round);
        public string BossName => IsBossRound ? _bossSpecies?.name : null;
        public int EnemiesThisRound { get; private set; }
        public int EnemiesDefeatedThisRound => _defeatedThisRound;
        public int AliveCount { get; private set; }
        public float PreparationRemaining => Mathf.Max(0f, _prepRemaining);

        /// <summary>Saldo de puntos para comprar EVs, bebidas y disparos durante esta partida.</summary>
        public int Points { get; private set; }
        public int TotalPointsEarned { get; private set; }
        public int CoinsEarned { get; private set; }
        public int BonusDrops { get; private set; }
        public int BonusPointsPerDrop => pointsPerKill;
        public int EnemiesDefeated { get; private set; }
        public int RoundsPassed { get; private set; }

        public int LastRoundBonusPoints { get; private set; }
        public int LastRoundBonusCoins { get; private set; }

        /// <summary>Se dispara cuando cambian puntos, monedas o el estado de la ronda.</summary>
        public event Action Changed;

        public bool IsRunActive => _phase != RoundPhase.Idle && _phase != RoundPhase.GameOver;
        public int EVLevel(int stat) => stat >= 0 && stat < _evLevels.Length ? _evLevels[stat] : 0;
        public int EVCost(int stat) => RunEconomy.EVPrice(EVLevel(stat));
        public bool CanBuyEV(int stat) => IsRunActive && stat >= 0 && stat < _evLevels.Length &&
                                          _evLevels[stat] < 10 && Points >= EVCost(stat);

        public bool BuyEV(int stat)
        {
            if (!CanBuyEV(stat)) return false;
            var player = PlayerSphereController.Instance;
            int previousMax = player != null ? player.MaxHealth : 0;
            Points -= EVCost(stat);
            _evLevels[stat]++;
            RebuildRunStats();
            // Preserve the old vitality-upgrade heal, but only for the actual gained capacity.
            if (stat == 0 && player != null) player.Heal(Mathf.Max(0, player.MaxHealth - previousMax));
            Changed?.Invoke();
            return true;
        }

        void Awake()
        {
            Instance = this;
            if (GetComponent<DrinkStations>() == null) gameObject.AddComponent<DrinkStations>();
            _drinks = GetComponent<DrinkStations>();
            if (GetComponent<ShotStations>() == null) gameObject.AddComponent<ShotStations>();
            _shotStations = GetComponent<ShotStations>();

            // Los valores del inspector tienen prioridad; solo se corrigen si quedaron raros.
            spawnRadiusMax = Mathf.Max(spawnRadiusMax, spawnRadiusMin + 1f);
            maxAliveCap = Mathf.Max(1, maxAliveCap);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            PlayerEconomy.Flush();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) PlayerEconomy.Flush();
        }

        void OnApplicationQuit() => PlayerEconomy.Flush();

        void Update()
        {
            if (GameFlow.Blocked) return;

            var player = PlayerSphereController.Instance;

            if (_phase == RoundPhase.Idle) return;

            if (_phase != RoundPhase.GameOver && player != null && !player.IsAlive)
            {
                GameOver();
                return;
            }

            AliveCount = CountAlive();

            switch (_phase)
            {
                case RoundPhase.Preparing:
                    _prepRemaining -= Time.deltaTime;
                    if (_prepRemaining <= 0f) BeginFighting();
                    break;

                case RoundPhase.Fighting:
                    UpdateFighting(player);
                    break;

                case RoundPhase.Clearing:
                    _clearRemaining -= Time.deltaTime;
                    if (_clearRemaining <= 0f) StartRound(Round + 1);
                    break;
            }
        }

        // ---------------------------------------------------------------- Flujo

        /// <summary>Empieza una run nueva desde la ronda 1 y revive al jugador.</summary>
        public void StartRun()
        {
            // Repeated start clicks must not end/pay the run that the first click just started.
            if (IsRunActive) return;
            if (!CaptureRunPokemon())
            {
                GameFlow.Blocked = true;
                Changed?.Invoke();
                return;
            }
            ClearEnemies();
            _drinks?.ResetRun();
            _shotStations?.ResetRun();
            _shots.ResetRun();
            ClearPlayerShots();

            Points = 0;
            TotalPointsEarned = 0;
            CoinsEarned = 0;
            BonusDrops = 0;
            Array.Clear(_evLevels, 0, _evLevels.Length);
            _creditedEnemies.Clear();
            RebuildRunStats();
            _rewardsPaid = false;
            RewardSaveFailed = false;
            EnemiesDefeated = 0;
            RoundsPassed = 0;
            LastRoundBonusPoints = 0;
            LastRoundBonusCoins = 0;

            _defeatedThisRound = 0;
            _spawnedThisRound = 0;
            AliveCount = 0;

            GameFlow.Over = false;
            GameFlow.Blocked = false;

            var player = PlayerSphereController.Instance;
            if (player != null)
            {
                player.Revive();
                player.GetComponent<PlayerAttack>()?.ResetRun();
            }

            StartRound(1);
        }

        /// <summary>Detiene la run (al volver al menú principal) y limpia los enemigos.</summary>
        public void StopRun()
        {
            if (IsRunActive) FinishRun();
            ClearEnemies();
            _phase = RoundPhase.Idle;
            GameFlow.Over = false;
            GameFlow.Blocked = true;
            AliveCount = 0;
            ResetTemporaryStats();
            PlayerEconomy.Flush();
            Changed?.Invoke();
        }

        public void AbandonRun()
        {
            if (!IsRunActive) return;
            FinishRun();
            ClearEnemies();
        }

        public void NotifyPlayerDefeated()
        {
            if (IsRunActive) FinishRun();
        }

        bool CaptureRunPokemon()
        {
            LastRunStartError = null;
            var progression = PokemonCollection.Progression;
            var instance = progression.Equipped;
            if (!progression.IsReady || progression.NeedsStarter || instance == null)
            {
                LastRunStartError = "EquippedPokemonRequired";
                return false;
            }
            SpeciesDefinition species = null;
            foreach (var entry in progression.Catalog)
                if (entry.id == instance.speciesId) { species = entry; break; }
            if (species == null)
            {
                LastRunStartError = "SpeciesNotFound";
                return false;
            }
            try
            {
                var asset = Resources.Load<TextAsset>("PokemonProgression/combat");
                if (asset == null) throw new InvalidOperationException("Missing PokemonProgression/combat configuration.");
                var balance = JsonUtility.FromJson<CombatBalance>(asset.text);
                if (balance == null) throw new InvalidOperationException("Invalid combat configuration.");
                balance.Validate();
                _balance = balance;
            }
            catch (Exception ex)
            {
                LastRunStartError = "InvalidCombatBalance";
                Debug.LogException(ex);
                return false;
            }
            _runPokemon = instance.Copy();
            _runSpecies = new SpeciesDefinition
            {
                id = species.id, name = species.name, category = species.category,
                spriteName = species.spriteName, baseStats = species.baseStats
            };
            return true;
        }

        void RebuildRunStats()
        {
            if (_runPokemon == null || _runSpecies == null || _balance == null) return;
            var ev = PokemonCombatStats.RunEV(_balance, _evLevels[0], _evLevels[1], _evLevels[2], _evLevels[3]);
            _runStats = PokemonStats.Calculate(_runSpecies.baseStats, _runPokemon.ivs, ev, _runPokemon.nature);
            _combatStats = PokemonCombatStats.Convert(_runStats, _balance);
            PlayerSphereController.Instance?.RecalculateStats();
        }

        void ResetTemporaryStats()
        {
            Array.Clear(_evLevels, 0, _evLevels.Length);
            _drinks?.ResetRun();
            _shotStations?.ResetRun();
            _shots.ResetRun();
            ClearPlayerShots();
            RebuildRunStats();
        }

        static void ClearPlayerShots()
        {
            PlayerSphereController.Instance?.GetComponent<PlayerAttack>()?.CancelPendingShots();
            AttackProjectile.ClearAll();
        }

        public bool SpendPoints(int amount)
        {
            if (!IsRunActive || amount < 0 || Points < amount) return false;
            Points -= amount;
            Changed?.Invoke();
            return true;
        }

        void AddPoints(int amount)
        {
            Points += amount;
            TotalPointsEarned += amount;
        }

        void FinishRun()
        {
            if (!IsRunActive || _rewardsPaid) return;
            _phase = RoundPhase.GameOver;
            GameFlow.Over = true;
            GameFlow.Blocked = true;
            ResetTemporaryStats();
            _rewardsPaid = PlayerEconomy.AddCoins(CoinsEarned);
            RewardSaveFailed = !_rewardsPaid;
            PlayerEconomy.RegisterLoss();
            PlayerEconomy.Flush();
            Changed?.Invoke();
        }

        void StartRound(int round)
        {
            Round = Mathf.Max(1, round);
            _bossSpecies = RoundDifficulty.IsBoss(Round)
                ? RoundDifficulty.BossSpecies(Round, JsonUtility.FromJson<SpeciesCatalog>(
                    Resources.Load<TextAsset>("PokemonProgression/catalog").text).species) : null;
            _bossEnemy = null;
            EnemiesThisRound = RoundDifficulty.IsBoss(Round) ? 1 : NormalEnemyCount();
            _creditedEnemies.Clear();

            _defeatedThisRound = 0;
            _spawnedThisRound = 0;
            AliveCount = 0;
            _spawnTimer = 0f;
            _prepRemaining = Mathf.Max(1.2f, preparationDuration - (Round - 1) * 0.12f);
            _phase = RoundPhase.Preparing;

            Changed?.Invoke();
        }

        void BeginFighting()
        {
            _phase = RoundPhase.Fighting;
            _spawnTimer = spawnInterval;
            Changed?.Invoke();
        }

        void UpdateFighting(PlayerSphereController player)
        {
            if (player == null) return;

            int maxAlive = MaxAliveForRound();

            if (_spawnedThisRound < EnemiesThisRound && AliveCount < maxAlive)
            {
                _spawnTimer += Time.deltaTime;
                if (_spawnTimer >= CurrentSpawnInterval())
                {
                    _spawnTimer = 0f;
                    SpawnEnemy(player);
                }
            }

            if (_defeatedThisRound >= EnemiesThisRound && AliveCount <= 0) CompleteRound();
        }

        void CompleteRound()
        {
            if (_phase != RoundPhase.Fighting) return;
            RoundsPassed++;

            LastRoundBonusPoints = pointsPerRound + pointsPerRoundGrowth * (Round - 1);
            LastRoundBonusCoins = coinsPerRound + coinsPerRoundGrowth * (Round - 1);

            AddPoints(LastRoundBonusPoints);
            CoinsEarned += LastRoundBonusCoins;

            PlayerEconomy.RegisterRoundWon();

            _phase = RoundPhase.Clearing;
            _clearRemaining = clearingDuration;

            Changed?.Invoke();
        }

        void GameOver()
        {
            FinishRun();
        }

        // --------------------------------------------------------------- Enemigos

        /// <summary>Lo llama un enemigo justo antes de destruirse. Suma puntos y monedas.</summary>
        public void NotifyEnemyKilled(EnemyTriangle enemy)
        {
            if (_phase != RoundPhase.Fighting || enemy == null || GameFlow.Over) return;
            if (IsBossRound && enemy != _bossEnemy) return;
            if (!_creditedEnemies.Add(enemy)) return;
            _defeatedThisRound++;

            EnemiesDefeated++;
            // Compensate skipped normal kills without inflating defeat/achievement counters.
            int rewardKills = IsBossRound ? NormalEnemyCount() : 1;
            AddPoints(pointsPerKill * rewardKills);
            CoinsEarned += coinsPerKill * rewardKills;

            if (UnityEngine.Random.value < 0.30f)
            {
                BonusDrops++;
                AddPoints(pointsPerKill * rewardKills);
                HitBurst.Spawn(enemy.transform.position + Vector3.up, new Color(.4f, .8f, 1f), 8, .8f);
            }

            PlayerEconomy.RegisterEnemyDefeated();

            Changed?.Invoke();
        }

        void SpawnEnemy(PlayerSphereController player)
        {
            int tier = TierForRound(Round);
            Vector3 position = PickSpawnPosition(player.transform.position);

            var go = IsBossRound
                ? EnemyTriangleFactory.Create(tier,
                    Resources.Load<Sprite>("PokemonPortraits/" + _bossSpecies.spriteName) ??
                    Resources.Load<Sprite>("PokemonSprites/" + _bossSpecies.spriteName))
                : EnemyTriangleFactory.Create(tier);
            go.transform.SetParent(transform, true);
            go.transform.position = position;

            var enemy = go.GetComponent<EnemyTriangle>();
            if (enemy != null)
            {
                enemy.InitializeRound(tier, RoundDifficulty.Stats(Round, IsBossRound), IsBossRound);
                if (IsBossRound) _bossEnemy = enemy;
            }

            _spawnedThisRound++;
            AliveCount++;

            HitBurst.Spawn(position + Vector3.up * 0.55f,
                           new Color(1f, 0.55f, 0.25f, 1f), 7, 0.7f);
        }

        Vector3 PickSpawnPosition(Vector3 origin)
        {
            var terrain = WorldTerrain.Instance;
            if (terrain == null || terrain.Map == null) return origin;

            Vector3 fallback = origin;

            for (int attempt = 0; attempt < 48; attempt++)
            {
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                float radius = UnityEngine.Random.Range(spawnRadiusMin, spawnRadiusMax);
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Vector3 candidate = origin + offset;

                if (!TryGround(terrain, candidate, out Vector3 ground)) continue;

                // Guarda un destino válido por si ningún punto respeta el desnivel.
                fallback = ground;

                if (Mathf.Abs(ground.y - origin.y) <= maxSpawnHeightDelta) return ground;
            }

            return fallback;
        }

        static bool TryGround(WorldTerrain terrain, Vector3 worldPosition, out Vector3 ground)
        {
            ground = worldPosition;

            var map = terrain.Map;
            Vector3 local = worldPosition - terrain.transform.position;
            int x = Mathf.FloorToInt(local.x / terrain.BlockSize);
            int z = Mathf.FloorToInt(local.z / terrain.BlockSize);

            if (!map.Contains(x, z)) return false;
            if (terrain.IsLakeAtWorld(worldPosition)) return false;

            ground = terrain.BlockTopCenter(x, z);
            return true;
        }

        int CountAlive()
        {
            var all = EnemyTriangle.All;
            int alive = 0;

            for (int i = 0; i < all.Count; i++)
            {
                var enemy = all[i];
                if (enemy != null && !enemy.IsDead) alive++;
            }

            return alive;
        }

        void ClearEnemies()
        {
            var all = EnemyTriangle.All;
            if (all.Count == 0) return;

            var snapshot = new EnemyTriangle[all.Count];
            for (int i = 0; i < all.Count; i++) snapshot[i] = all[i];

            foreach (var enemy in snapshot)
                if (enemy != null) Destroy(enemy.gameObject);
        }

        // ------------------------------------------------------------ Dificultad

        int MaxAliveForRound()
        {
            if (IsBossRound) return 1;
            double bonus = ((Round - 1) / Mathf.Max(1, maxAliveGrowthEvery)) * (double)maxAliveGrowth;
            return (int)Math.Max(1d, Math.Min(maxAliveCap, baseMaxAlive + bonus));
        }

        int TierForRound(int round)
        {
            return Mathf.Clamp(1 + (round - 1) / Mathf.Max(1, roundsPerTier), 1, maxTier);
        }

        int NormalEnemyCount()
        {
            return (int)Math.Max(1d, Math.Min(maxEnemiesPerRound,
                (double)baseEnemies + (double)enemiesPerRound * (Round - 1)));
        }

        float CurrentSpawnInterval()
        {
            return Mathf.Max(minSpawnInterval, spawnInterval - 0.03f * (Round - 1));
        }
    }
}
