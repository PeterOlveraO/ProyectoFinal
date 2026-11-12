using System;
using Game.Combat;
using Game.Core;
using Game.Economy;
using Game.Enemies;
using Game.Player;
using Game.Terrain;
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
    ///   · Monedas (global, persistente): +1 por enemigo y +15 por ronda superada
    ///     (más 5 extra por ronda). Se guardan en <see cref="PlayerEconomy"/>.
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

        RoundPhase _phase = RoundPhase.Idle;
        float _prepRemaining;
        float _clearRemaining;
        float _spawnTimer;
        int _spawnedThisRound;
        int _defeatedThisRound;

        public RoundPhase Phase => _phase;
        public int Round { get; private set; }
        public int EnemiesThisRound { get; private set; }
        public int EnemiesDefeatedThisRound => _defeatedThisRound;
        public int AliveCount { get; private set; }
        public float PreparationRemaining => Mathf.Max(0f, _prepRemaining);

        public int Points { get; private set; }
        public int CoinsEarned { get; private set; }
        /// <summary>Dinero gastable solo durante esta partida; no afecta la cartera global.</summary>
        public int RunCash { get; private set; }
        public int SkillPointsDropped { get; private set; }
        public int EnemiesDefeated { get; private set; }
        public int RoundsPassed { get; private set; }

        public int LastRoundBonusPoints { get; private set; }
        public int LastRoundBonusCoins { get; private set; }

        /// <summary>Se dispara cuando cambian puntos, monedas o el estado de la ronda.</summary>
        public event Action Changed;

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
            ClearEnemies();
            GetComponent<DrinkStations>()?.ResetRun();

            Points = 0;
            CoinsEarned = 0;
            RunCash = 0;
            SkillPointsDropped = 0;
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
            ClearEnemies();
            _phase = RoundPhase.Idle;
            GameFlow.Over = false;
            AliveCount = 0;
            PlayerEconomy.Flush();
            Changed?.Invoke();
        }

        public void AbandonRun()
        {
            if (_phase == RoundPhase.Idle || _phase == RoundPhase.GameOver) return;
            _phase = RoundPhase.GameOver;
            GameFlow.Over = true;
            GameFlow.Blocked = true;
            ClearEnemies();
            PlayerEconomy.RegisterLoss();
            PlayerEconomy.Flush();
            Changed?.Invoke();
        }

        public bool SpendRunCash(int amount)
        {
            if (_phase == RoundPhase.Idle || _phase == RoundPhase.GameOver || amount < 0 || RunCash < amount) return false;
            RunCash -= amount;
            Changed?.Invoke();
            return true;
        }

        void StartRound(int round)
        {
            Round = Mathf.Max(1, round);
            EnemiesThisRound = Mathf.Min(maxEnemiesPerRound,
                                          baseEnemies + enemiesPerRound * (Round - 1));

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

            LastRoundBonusPoints = pointsPerRound + pointsPerRoundGrowth * (Round - 1);
            LastRoundBonusCoins = coinsPerRound + coinsPerRoundGrowth * (Round - 1);

            Points += LastRoundBonusPoints;
            CoinsEarned += LastRoundBonusCoins;
            RunCash += LastRoundBonusCoins;

            PlayerEconomy.AddCoins(LastRoundBonusCoins);
            PlayerEconomy.RegisterRoundWon();
            PlayerEconomy.Flush();

            _phase = RoundPhase.Clearing;
            _clearRemaining = clearingDuration;

            Changed?.Invoke();
        }

        void GameOver()
        {
            _phase = RoundPhase.GameOver;
            GameFlow.Over = true;
            GameFlow.Blocked = true;
            PlayerEconomy.RegisterLoss();
            PlayerEconomy.Flush();
            Changed?.Invoke();
        }

        // --------------------------------------------------------------- Enemigos

        /// <summary>Lo llama un enemigo justo antes de destruirse. Suma puntos y monedas.</summary>
        public void NotifyEnemyKilled(EnemyTriangle enemy)
        {
            if (_phase != RoundPhase.Fighting || enemy == null || GameFlow.Over) return;
            _defeatedThisRound++;

            EnemiesDefeated++;
            Points += pointsPerKill;
            CoinsEarned += coinsPerKill;
            RunCash += coinsPerKill;

            if (UnityEngine.Random.value < 0.30f)
            {
                SkillPointsDropped++;
                PlayerEconomy.AddSkillPoint();
                HitBurst.Spawn(enemy.transform.position + Vector3.up, new Color(.4f, .8f, 1f), 8, .8f);
            }

            PlayerEconomy.AddCoins(coinsPerKill);
            PlayerEconomy.RegisterEnemyDefeated();

            Changed?.Invoke();
        }

        void SpawnEnemy(PlayerSphereController player)
        {
            int tier = TierForRound(Round);
            Vector3 position = PickSpawnPosition(player.transform.position);

            var go = EnemyTriangleFactory.Create(tier);
            go.transform.SetParent(transform, true);
            go.transform.position = position;

            var enemy = go.GetComponent<EnemyTriangle>();
            if (enemy != null) enemy.Initialize(tier, CurrentEnemySpeed());

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
