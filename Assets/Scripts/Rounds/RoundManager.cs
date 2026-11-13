using System;
using Game.Combat;
using Game.Core;
using Game.Economy;
using Game.Enemies;
using Game.Player;
using Game.Terrain;
using Game.Upgrades;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>
    /// Sistema de rondas con economía.
    ///
    /// Cada ronda pide eliminar un número de enemigos que crece con la ronda. Los enemigos
    /// aparecen por oleadas alrededor del jugador (nunca encima) y se vuelven algo más
    /// rápidos y resistentes; cada 4 rondas suben un "nivel de zona" (+20 % de vida).
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
        [SerializeField, Min(1)] int baseEnemies = 6;

        [Tooltip("Enemigos que se suman en cada ronda.")]
        [SerializeField, Min(0)] int enemiesPerRound = 2;

        [Tooltip("Tope de enemigos por ronda para que las rondas no se hagan eternas.")]
        [SerializeField, Min(1)] int maxEnemiesPerRound = 48;

        [Tooltip("Enemigos vivos a la vez en la ronda 1.")]
        [SerializeField, Min(1)] int baseMaxAlive = 4;

        [Tooltip("Cada cuántas rondas se suma un enemigo vivo más.")]
        [SerializeField, Min(1)] int maxAliveGrowthEvery = 2;

        [Tooltip("Tope de enemigos vivos simultáneos.")]
        [SerializeField, Min(1)] int maxAliveCap = 14;

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
        [Tooltip("Rondas por cada nivel de zona (+20 % de vida del enemigo).")]
        [SerializeField, Min(1)] int roundsPerTier = 4;

        [Tooltip("Nivel de zona máximo (tope de vida).")]
        [SerializeField, Min(1)] int maxTier = 8;

        [SerializeField, Min(0.1f)] float baseEnemySpeed = 2.2f;
        [SerializeField, Min(0f)] float speedPerRound = 0.09f;
        [SerializeField, Min(0.1f)] float maxEnemySpeed = 4.6f;

        [Header("Recompensas")]
        [SerializeField, Min(0)] int pointsPerKill = 10;
        [SerializeField, Min(0)] int pointsPerRound = 50;
        [SerializeField, Min(0)] int pointsPerRoundGrowth = 10;
        [SerializeField, Min(0)] int coinsPerKill = 1;
        [SerializeField, Min(0)] int coinsPerRound = 15;
        [SerializeField, Min(0)] int coinsPerRoundGrowth = 5;

        [Header("Progresión ampliada")]
        [SerializeField, Min(0)] int extraEnemiesPerRound = 2;
        [SerializeField, Min(1)] int expandedEnemyCap = 120;
        [SerializeField, Min(1)] int bossEveryRounds = 5;
        [SerializeField, Min(1f)] float powerupDuration = 15f;

        RoundPhase _phase = RoundPhase.Idle;
        float _prepRemaining;
        float _clearRemaining;
        float _spawnTimer;
        int _spawnedThisRound;
        int _defeatedThisRound;
        readonly int[] _evLevels = new int[4];
        bool _rewardsPaid;
        float _doublePointsRemaining;
        float _shieldRemaining;
        bool _skipRequested;

        public RoundPhase Phase => _phase;
        public int Round { get; private set; }
        public int EnemiesThisRound { get; private set; }
        public int EnemiesDefeatedThisRound => _defeatedThisRound;
        public int AliveCount { get; private set; }
        public float PreparationRemaining => Mathf.Max(0f, _prepRemaining);
        public float DoublePointsRemaining => _doublePointsRemaining;
        public float ShieldRemaining => _shieldRemaining;
        public bool DoublePointsActive => _doublePointsRemaining > 0f;
        public bool ShieldActive => _shieldRemaining > 0f;
        public bool IsBossRound => Round > 0 && Round % Mathf.Max(1, bossEveryRounds) == 0;

        /// <summary>Saldo de puntos para comprar EVs y bebidas durante esta partida.</summary>
        public int Points { get; private set; }
        public int TotalPointsEarned { get; private set; }
        public int CoinsEarned { get; private set; }
        public int BonusDrops { get; private set; }
        public int BonusPointsPerDrop => pointsPerKill * (DoublePointsActive ? 2 : 1);
        public int EnemiesDefeated { get; private set; }
        public int RoundsPassed { get; private set; }

        public int LastRoundBonusPoints { get; private set; }
        public int LastRoundBonusCoins { get; private set; }

        /// <summary>Se dispara cuando cambian puntos, monedas o el estado de la ronda.</summary>
        public event Action Changed;

        public bool IsRunActive => _phase != RoundPhase.Idle && _phase != RoundPhase.GameOver;
        public int EVLevel(int stat) => stat >= 0 && stat < _evLevels.Length ? _evLevels[stat] : 0;
        // R1 da ~128 puntos; una rama completa no debe poder comprarse al inicio.
        public int EVCost(int stat) => 180 + EVLevel(stat) * 90;
        public bool CanBuyEV(int stat) => IsRunActive && stat >= 0 && stat < _evLevels.Length &&
                                          _evLevels[stat] < 10 && Points >= EVCost(stat);

        public bool BuyEV(int stat)
        {
            if (!CanBuyEV(stat) || !SpendPoints(EVCost(stat))) return false;
            _evLevels[stat]++;
            if (stat == 0) PlayerSphereController.Instance?.Heal(10);
            Changed?.Invoke();
            return true;
        }

        void Awake()
        {
            Instance = this;
            if (GetComponent<DrinkStations>() == null) gameObject.AddComponent<DrinkStations>();

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

            if (_phase != RoundPhase.GameOver)
            {
                bool hadDouble = DoublePointsActive;
                bool hadShield = ShieldActive;
                _doublePointsRemaining = Mathf.Max(0f, _doublePointsRemaining - Time.deltaTime);
                _shieldRemaining = Mathf.Max(0f, _shieldRemaining - Time.deltaTime);
                if (hadDouble != DoublePointsActive || hadShield != ShieldActive) Changed?.Invoke();
                if (_skipRequested && _phase == RoundPhase.Fighting)
                {
                    _skipRequested = false;
                    ClearEnemies();
                    ClearPowerups();
                    StartRound(Round + 1);
                    return;
                }
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
            if (IsRunActive) FinishRun();
            ClearEnemies();
            ClearPowerups();
            _doublePointsRemaining = _shieldRemaining = 0f;
            _skipRequested = false;
            GetComponent<DrinkStations>()?.ResetRun();
            PlayerSphereController.Instance?.GetComponent<ShotUpgradeStations>()?.ResetRun();

            Points = 0;
            TotalPointsEarned = 0;
            CoinsEarned = 0;
            BonusDrops = 0;
            Array.Clear(_evLevels, 0, _evLevels.Length);
            _rewardsPaid = false;
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
            if (player != null) player.Revive();

            StartRound(1);
        }

        /// <summary>Detiene la run (al volver al menú principal) y limpia los enemigos.</summary>
        public void StopRun()
        {
            if (IsRunActive) FinishRun();
            ClearEnemies();
            ClearPowerups();
            _doublePointsRemaining = _shieldRemaining = 0f;
            _skipRequested = false;
            _phase = RoundPhase.Idle;
            GameFlow.Over = false;
            AliveCount = 0;
            PlayerEconomy.Flush();
            Changed?.Invoke();
        }

        public void AbandonRun()
        {
            if (!IsRunActive) return;
            FinishRun();
            ClearEnemies();
            ClearPowerups();
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
            if (DoublePointsActive) amount *= 2;
            Points += amount;
            TotalPointsEarned += amount;
        }

        void FinishRun()
        {
            if (!IsRunActive || _rewardsPaid) return;
            _phase = RoundPhase.GameOver;
            ClearPowerups();
            _doublePointsRemaining = _shieldRemaining = 0f;
            _skipRequested = false;
            _rewardsPaid = true;
            GameFlow.Over = true;
            GameFlow.Blocked = true;
            PlayerEconomy.AddCoins(CoinsEarned);
            PlayerEconomy.RegisterLoss();
            PlayerEconomy.Flush();
            Changed?.Invoke();
        }

        void StartRound(int round)
        {
            Round = Mathf.Max(1, round);
            EnemiesThisRound = (int)Math.Min(Math.Max(maxEnemiesPerRound, expandedEnemyCap),
                (long)baseEnemies + ((long)enemiesPerRound + extraEnemiesPerRound) * (Round - 1));

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
            RoundsPassed++;

            LastRoundBonusPoints = (pointsPerRound + pointsPerRoundGrowth * (Round - 1)) * (DoublePointsActive ? 2 : 1);
            LastRoundBonusCoins = coinsPerRound + coinsPerRoundGrowth * (Round - 1);

            Points += LastRoundBonusPoints;
            TotalPointsEarned += LastRoundBonusPoints;
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
            if (_phase != RoundPhase.Fighting || enemy == null || enemy.transform.parent != transform || GameFlow.Over) return;
            _defeatedThisRound++;

            EnemiesDefeated++;
            AddPoints(pointsPerKill);
            CoinsEarned += coinsPerKill;

            if (UnityEngine.Random.value < 0.30f)
            {
                BonusDrops++;
                AddPoints(pointsPerKill);
                HitBurst.Spawn(enemy.transform.position + Vector3.up, new Color(.4f, .8f, 1f), 8, .8f);
            }

            if (UnityEngine.Random.value < 0.20f)
                RoundPowerup.Spawn(this, enemy.transform.position, (RoundPowerup.Kind)UnityEngine.Random.Range(0, 3));

            PlayerEconomy.RegisterEnemyDefeated();

            Changed?.Invoke();
        }

        void SpawnEnemy(PlayerSphereController player)
        {
            int tier = TierForRound(Round);
            Vector3 position = PickSpawnPosition(player.transform.position);

            bool boss = Round % Mathf.Max(1, bossEveryRounds) == 0 &&
                        _spawnedThisRound == EnemiesThisRound / 2;
            int bossIndex = boss ? ((Round / Mathf.Max(1, bossEveryRounds) - 1) % Game.Pokemon.PokemonSpriteLibrary.BossCount) : -1;
            var go = EnemyTriangleFactory.Create(tier, bossIndex);
            go.transform.SetParent(transform, true);
            go.transform.position = position;

            var enemy = go.GetComponent<EnemyTriangle>();
            if (enemy != null) enemy.Initialize(tier, CurrentEnemySpeed(), boss);

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
                if (enemy != null && enemy.transform.parent == transform && !enemy.IsDead) alive++;
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
                if (enemy != null && enemy.transform.parent == transform) Destroy(enemy.gameObject);
        }

        void ClearPowerups()
        {
            foreach (var pickup in GetComponentsInChildren<RoundPowerup>())
                Destroy(pickup.gameObject);
        }

        internal bool CollectPowerup(RoundPowerup.Kind kind)
        {
            if (_phase != RoundPhase.Fighting || GameFlow.Blocked || GameFlow.Over) return false;
            switch (kind)
            {
                case RoundPowerup.Kind.DoublePoints: _doublePointsRemaining = powerupDuration; break;
                case RoundPowerup.Kind.Shield: _shieldRemaining = powerupDuration; break;
                case RoundPowerup.Kind.SkipRound: _skipRequested = true; break;
            }
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------ Dificultad

        int MaxAliveForRound()
        {
            int bonus = (Round - 1) / Mathf.Max(1, maxAliveGrowthEvery);
            return Mathf.Clamp(baseMaxAlive + bonus, 1, maxAliveCap);
        }

        int TierForRound(int round)
        {
            return Mathf.Clamp(1 + (round - 1) / Mathf.Max(1, roundsPerTier), 1, maxTier);
        }

        float CurrentEnemySpeed()
        {
            return Mathf.Min(maxEnemySpeed, baseEnemySpeed + speedPerRound * (Round - 1));
        }

        float CurrentSpawnInterval()
        {
            return Mathf.Max(minSpawnInterval, spawnInterval - 0.03f * (Round - 1));
        }
    }
}
