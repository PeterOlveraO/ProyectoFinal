using Game.Combat;
using Game.Core;
using Game.Economy;
using Game.Player;
using Game.Rendering;
using Game.Rounds;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// HUD de la partida: vida del jugador, ronda actual, enemigos restantes, puntos
    /// (economía interna) y monedas (economía global). También muestra el cartel de
    /// ronda y la pantalla de derrota con el resumen de la run.
    ///
    /// Se construye por código sobre el Canvas del HUD y no necesita imágenes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunHud : MonoBehaviour
    {
        RectTransform _statusPanel;
        Image _healthFill;
        TMPro.TMP_Text _healthLabel;
        TMPro.TMP_Text _roundLabel;
        TMPro.TMP_Text _enemiesLabel;
        TMPro.TMP_Text _pointsLabel;
        TMPro.TMP_Text _coinsLabel;
        TMPro.TMP_Text _bonusLabel;
        Button _drinkButton;
        TMPro.TMP_Text _drinkLabel;
        int _nearbyDrink = -1;
        TMPro.TMP_Text _equippedShotLabel;
        Button _shotButton;
        TMPro.TMP_Text _shotLabel;
        int _nearbyShot = -1;

        CanvasGroup _banner;
        TMPro.TMP_Text _bannerTitle;
        TMPro.TMP_Text _bannerSubtitle;
        float _bannerTimer;
        RoundManager.RoundPhase _bannerPhase = (RoundManager.RoundPhase)(-1);
        int _bannerRound = -1;

        Image _damageFlash;
        float _flashAmount;
        int _lastHealth = -1;
        int _lastMaxHealth = -1;
        int _lastPreparation = -1;
        RoundManager _round;
        RectTransform _root, _overlay;
        bool _numbersDirty = true;

        GameObject _gameOverRoot;
        TMPro.TMP_Text _gameOverStats;
        MenuPage _gameOverPage;

        void Start()
        {
            Build();
            _round = RoundManager.Instance;
            if (_round != null) _round.Changed += MarkNumbersDirty;
            PlayerEconomy.Changed += MarkNumbersDirty;
        }

        void MarkNumbersDirty() => _numbersDirty = true;

        void OnDestroy()
        {
            if (_round != null) _round.Changed -= MarkNumbersDirty;
            PlayerEconomy.Changed -= MarkNumbersDirty;
            if (_root != null) Destroy(_root.gameObject);
            if (_overlay != null) Destroy(_overlay.gameObject);
        }

        void Update()
        {
            var round = RoundManager.Instance;
            var player = PlayerSphereController.Instance;
            var menu = GameMenuController.Instance;

            bool playing = menu == null || menu.IsPlaying;
            bool gameOver = round != null && round.Phase == RoundManager.RoundPhase.GameOver;

            bool showHud = playing && !gameOver && (menu == null || !menu.IsPaused);
            if (_root != null && _root.gameObject.activeSelf != showHud)
                _root.gameObject.SetActive(showHud);

            UpdateHealth(player);
            if (_numbersDirty)
            {
                UpdateNumbers(round, player);
                UpdateShotLoadout(round);
                _numbersDirty = false;
            }
            UpdateBanner(round, showHud);
            UpdateFlash();
            UpdateGameOver(round, gameOver);
            UpdateDrink(round, player, playing && !gameOver && (menu == null || !menu.IsPaused));
            UpdateShotOffer(round, player, showHud && GameFlow.Playing);
        }

        // ------------------------------------------------------------- Construcción

        void Build()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[RunHud] No hay Canvas padre.", this);
                enabled = false;
                return;
            }

            var root = UIFactory.SafeRoot(canvas.transform, "RunHud");
            _root = root;

            // Flash rojo al recibir daño (detrás del resto del HUD).
            _damageFlash = UIFactory.CreateImage("DamageFlash", root, null,
                                                new Color(1f, 0.1f, 0.12f, 0f), false);
            UIFactory.Stretch(_damageFlash.rectTransform);

            BuildStatusPanel(root);
            BuildBanner(root);
            BuildDrinkButton(root);
            BuildShotShop(root);

            // La pantalla de derrota vive en su propio Canvas por encima de todo el HUD.
            _overlay = UIFactory.CreateCanvas("UI_GameOver", 120);
            BuildGameOver(_overlay);
        }

        void BuildStatusPanel(Transform parent)
        {
            _statusPanel = UIFactory.CreateRect("RunStatus", parent);
            UIFactory.Place(_statusPanel, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(24f, -24f), new Vector2(470f, 220f));

            var background = UIFactory.CreateImage("Background", _statusPanel,
                                                   ProceduralSprites.RoundedRect,
                                                   UIFactory.DeepViolet, false, true);
            UIFactory.Stretch(background.rectTransform);

            var healthTitle = UIFactory.CreateText("HealthTitle", _statusPanel, "VIDA", 26,
                                                   UIFactory.Sun, TextAnchor.MiddleLeft);
            UIFactory.Place(healthTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(22f, -14f), new Vector2(200f, 30f));

            _healthLabel = UIFactory.CreateText("HealthValue", _statusPanel, "100 / 100", 24,
                                                UIFactory.Cream, TextAnchor.MiddleRight);
            UIFactory.Place(_healthLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(248f, -14f), new Vector2(200f, 30f));

            var barBackground = UIFactory.CreateImage("HealthBarBackground", _statusPanel,
                                                      ProceduralSprites.RoundedRect,
                                                      new Color(0.10f, 0.05f, 0.20f, 0.95f), false, true);
            UIFactory.Place(barBackground.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(22f, -46f), new Vector2(426f, 30f));

            _healthFill = UIFactory.CreateImage("HealthBarFill", barBackground.rectTransform,
                                                ProceduralSprites.RoundedRect, UIFactory.Lime, false, true);
            UIFactory.Stretch(_healthFill.rectTransform, 4f);
            _healthFill.type = Image.Type.Filled;
            _healthFill.fillMethod = Image.FillMethod.Horizontal;
            _healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _healthFill.fillAmount = 1f;

            _roundLabel = UIFactory.CreateText("Round", _statusPanel, "RONDA 1", 28,
                                               UIFactory.Sun, TextAnchor.MiddleLeft);
            UIFactory.Place(_roundLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(22f, -86f), new Vector2(260f, 34f));

            _enemiesLabel = UIFactory.CreateText("Enemies", _statusPanel, "0 / 6", 24,
                                                 UIFactory.Cream, TextAnchor.MiddleRight);
            UIFactory.Place(_enemiesLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(248f, -86f), new Vector2(200f, 30f));

            _pointsLabel = UIFactory.CreateText("Points", _statusPanel, "PUNTOS 0", 24,
                                                UIFactory.Lime, TextAnchor.MiddleLeft);
            UIFactory.Place(_pointsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(22f, -130f), new Vector2(260f, 30f));

            _coinsLabel = UIFactory.CreateText("Coins", _statusPanel, "MONEDAS 0", 24,
                                               UIFactory.Sun, TextAnchor.MiddleRight);
            UIFactory.Place(_coinsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(248f, -130f), new Vector2(200f, 30f));

            _bonusLabel = UIFactory.CreateText("RunBonus", _statusPanel, "BONUS DE PUNTOS x0", 22,
                                               UIFactory.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(_bonusLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(22f, -172f), new Vector2(430f, 30f));
        }

        void BuildDrinkButton(Transform parent)
        {
            _drinkButton = UIFactory.CreateButton(parent, "BuyDrink", "", UIFactory.Sky,
                                                   new Vector2(650f, 100f), BuyDrink, 28);
            UIFactory.Place((RectTransform)_drinkButton.transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                            new Vector2(0f, 35f), new Vector2(650f, 100f));
            _drinkLabel = _drinkButton.GetComponentInChildren<TMPro.TMP_Text>();
            _drinkButton.gameObject.SetActive(false);
        }

        void UpdateDrink(RoundManager round, PlayerSphereController player, bool playing)
        {
            var stations = round != null ? round.Drinks : null;
            _nearbyDrink = playing && player != null && stations != null && round.Phase != RoundManager.RoundPhase.Idle
                ? stations.NearbyOffer(player.transform.position) : -1;
            if (_drinkButton == null) return;
            _drinkButton.gameObject.SetActive(_nearbyDrink >= 0);
            if (_nearbyDrink < 0) return;
            if (!stations.IsBuilt(_nearbyDrink))
            {
                _drinkLabel.text = "CONSTRUIR MÁQUINA " + DrinkStations.Names[_nearbyDrink] +
                    " · " + DrinkStations.BuildPrices[_nearbyDrink] + " PUNTOS";
                _drinkButton.interactable = round.Points >= DrinkStations.BuildPrices[_nearbyDrink];
                return;
            }
            _drinkLabel.text = stations.Has((DrinkType)_nearbyDrink) ?
                "BEBIDA " + DrinkStations.Names[_nearbyDrink] + " · ADQUIRIDA" :
                "BEBIDA " + DrinkStations.Names[_nearbyDrink] + " · " + DrinkStations.Prices[_nearbyDrink] + " PUNTOS";
            _drinkButton.interactable = !stations.Has((DrinkType)_nearbyDrink) &&
                                         round.Points >= DrinkStations.Prices[_nearbyDrink];
        }

        void BuyDrink()
        {
            var stations = RoundManager.Instance?.Drinks;
            if (stations == null) return;
            if (stations.IsBuilt(_nearbyDrink)) stations.Buy(_nearbyDrink);
            else stations.Build(_nearbyDrink);
        }

        void BuildShotShop(Transform parent)
        {
            // Top-center loadout, away from the top-left status and bottom-corner sticks.
            var panel = UIFactory.CreateRect("ShotLoadout", parent);
            UIFactory.Place(panel, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0f, -24f), new Vector2(650f, 54f));
            var background = UIFactory.CreateImage("Background", panel, ProceduralSprites.RoundedRect,
                UIFactory.DeepViolet, false, true);
            UIFactory.Stretch(background.rectTransform);
            _equippedShotLabel = UIFactory.CreateText("EquippedShot", panel, "DISPARO · BÁSICO", 26, UIFactory.Sun);
            UIFactory.Place(_equippedShotLabel.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0f, -8f), new Vector2(630f, 38f));

            // Separate vertical slot above the drink offer (35..135), never over it.
            _shotButton = UIFactory.CreateButton(parent, "BuyShot", "", UIFactory.CardViolet,
                new Vector2(650f, 110f), BuyShot, 28);
            UIFactory.Place((RectTransform)_shotButton.transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(0f, 155f), new Vector2(650f, 110f));
            _shotLabel = _shotButton.GetComponentInChildren<TMPro.TMP_Text>();
            _shotButton.gameObject.SetActive(false);
        }

        void UpdateShotLoadout(RoundManager round)
        {
            if (_equippedShotLabel == null) return;
            var equipped = round != null ? round.Shots.Equipped : ShotType.Basic;
            _equippedShotLabel.text = "DISPARO EQUIPADO · " + ShotDefinitions.Name(equipped);
        }

        void UpdateShotOffer(RoundManager round, PlayerSphereController player, bool playing)
        {
            if (_shotButton == null) return;
            var stations = round != null ? round.ShotStations : null;
            _nearbyShot = playing && round != null && round.IsRunActive && player != null && player.IsAlive &&
                stations != null ? stations.NearbyOffer(player.transform.position) : -1;
            _shotButton.gameObject.SetActive(_nearbyShot >= 0);
            if (_nearbyShot < 0) return;
            var type = ShotStations.OfferType(_nearbyShot);
            if (!stations.IsBuilt(_nearbyShot))
            {
                _shotLabel.text = "CONSTRUIR ARSENAL " + ShotDefinitions.Name(type) + "\n" +
                    ShotStations.BuildPrice(_nearbyShot) + " PUNTOS";
                _shotButton.interactable = round.Points >= ShotStations.BuildPrice(_nearbyShot);
                return;
            }
            _shotLabel.text = "DISPARO " + ShotDefinitions.Name(type) + "\n" +
                "COMPRAR Y REEMPLAZAR · " + ShotDefinitions.Price(type) + " PUNTOS";
            _shotButton.interactable = round.Points >= ShotDefinitions.Price(type);
        }

        void BuyShot()
        {
            var round = RoundManager.Instance;
            if (round == null || round.ShotStations == null) return;
            if (round.ShotStations.IsBuilt(_nearbyShot)) round.BuyShot(_nearbyShot);
            else round.ShotStations.Build(_nearbyShot);
        }

        void BuildBanner(Transform parent)
        {
            var rect = UIFactory.CreateRect("RoundBanner", parent);
            UIFactory.Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -260f), new Vector2(1000f, 170f));

            _banner = rect.gameObject.AddComponent<CanvasGroup>();
            _banner.alpha = 0f;
            _banner.blocksRaycasts = false;
            _banner.interactable = false;

            _bannerTitle = UIFactory.CreateText("Title", rect, "RONDA 1", 78, UIFactory.Sun);
            UIFactory.Place(_bannerTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, 0f), new Vector2(1100f, 110f));
            UIFactory.AddOutline(_bannerTitle, UIFactory.Ink, 6f);

            _bannerSubtitle = UIFactory.CreateText("Subtitle", rect, "", 32, UIFactory.Cream);
            UIFactory.Place(_bannerSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -112f), new Vector2(1100f, 56f));
            UIFactory.AddOutline(_bannerSubtitle, new Color(0f, 0f, 0f, 0.55f), 2f);
        }

        void BuildGameOver(Transform parent)
        {
            _gameOverPage = new MenuPage(parent, "GameOver", "Cada partida te hace más fuerte", "Fin de la partida · Este es tu progreso");
            _gameOverRoot = _gameOverPage.Root.gameObject;
            var card = UIFactory.Surface("Summary", _gameOverPage.Body);
            UIFactory.Region(card, .15f, 0, .85f, 1);
            UIFactory.Stack(card, 40, 24);
            UIFactory.Label(card, "Title", "PARTIDA TERMINADA", 46, UIFactory.Sun, 80);
            _gameOverStats = UIFactory.Label(card, "Stats", "", 32, UIFactory.Cream, 360, TextAnchor.UpperLeft);
            _gameOverPage.Action("RetryButton", "VOLVER A INTENTAR", UIFactory.Sun, Retry);
            _gameOverPage.Action("MenuButton", "Volver al menú", UIFactory.CardViolet, BackToMenu);
        }

        // ---------------------------------------------------------------- Estado

        void UpdateHealth(PlayerSphereController player)
        {
            if (_healthFill == null || player == null) return;
            if (_lastHealth == player.Health && _lastMaxHealth == player.MaxHealth) return;

            float ratio = player.Health01;
            _healthFill.fillAmount = ratio;
            _healthFill.color = Color.Lerp(UIFactory.Coral, UIFactory.Lime, ratio);
            _healthLabel.text = player.Health + " / " + player.MaxHealth;

            if (_lastHealth >= 0 && player.Health < _lastHealth) _flashAmount = 0.5f;
            _lastHealth = player.Health;
            _lastMaxHealth = player.MaxHealth;
        }

        void UpdateNumbers(RoundManager round, PlayerSphereController player)
        {
            if (round == null || round.Phase == RoundManager.RoundPhase.Idle)
            {
                if (_roundLabel != null) _roundLabel.text = "RONDA 1";
                if (_enemiesLabel != null) _enemiesLabel.text = "0 / 0";
                if (_pointsLabel != null) _pointsLabel.text = "PUNTOS 0";
                if (_coinsLabel != null) _coinsLabel.text = "MONEDAS " + PlayerEconomy.Coins;
                if (_bonusLabel != null) _bonusLabel.text = "BONUS DE PUNTOS x0";
                return;
            }

            _roundLabel.text = "RONDA " + round.Round;
            _enemiesLabel.text = round.EnemiesDefeatedThisRound + " / " + round.EnemiesThisRound;
            _pointsLabel.text = "PUNTOS " + round.Points;
            _coinsLabel.text = "PREMIO +" + round.CoinsEarned;
            _bonusLabel.text = "BONUS x" + round.BonusDrops + "  ·  CARTERA " + PlayerEconomy.Coins;
        }

        void UpdateBanner(RoundManager round, bool playing)
        {
            if (_banner == null) return;

            if (!playing || round == null || round.Phase == RoundManager.RoundPhase.Idle)
            {
                _banner.alpha = 0f;
                return;
            }

            var phase = round.Phase;

            if (phase != _bannerPhase || round.Round != _bannerRound)
            {
                _bannerPhase = phase;
                _bannerRound = round.Round;

                switch (phase)
                {
                    case RoundManager.RoundPhase.Preparing:
                        SetBanner("RONDA " + round.Round,
                                  "Prepárate… " + Mathf.CeilToInt(round.PreparationRemaining));
                        _bannerTimer = 999f;
                        break;

                    case RoundManager.RoundPhase.Fighting:
                        SetBanner("RONDA " + round.Round,
                                  "Elimina a " + round.EnemiesThisRound + " enemigos");
                        _bannerTimer = 2.2f;
                        break;

                    case RoundManager.RoundPhase.Clearing:
                        SetBanner("¡RONDA SUPERADA!",
                                  "+" + round.LastRoundBonusPoints + " puntos  ·  +" +
                                  round.LastRoundBonusCoins + " monedas");
                        _bannerTimer = 999f;
                        break;

                    default:
                        _bannerTimer = 0f;
                        break;
                }
            }

            if (phase == RoundManager.RoundPhase.Preparing)
            {
                int seconds = Mathf.CeilToInt(round.PreparationRemaining);
                if (seconds != _lastPreparation) _bannerSubtitle.text = "Prepárate… " + seconds;
                _lastPreparation = seconds;
            }

            if (phase == RoundManager.RoundPhase.Fighting || phase == RoundManager.RoundPhase.Clearing)
                _bannerTimer -= Time.unscaledDeltaTime;

            _banner.alpha = Mathf.Clamp01(_bannerTimer);
        }

        void SetBanner(string title, string subtitle)
        {
            _bannerTitle.text = title;
            _bannerSubtitle.text = subtitle;
        }

        void UpdateFlash()
        {
            if (_damageFlash == null) return;

            _flashAmount = Mathf.Max(0f, _flashAmount - 2.5f * Time.unscaledDeltaTime);
            _damageFlash.color = new Color(1f, 0.1f, 0.12f, _flashAmount * 0.45f);
        }

        void UpdateGameOver(RoundManager round, bool gameOver)
        {
            if (_gameOverRoot == null) return;

            if (gameOver && !_gameOverRoot.activeSelf)
            {
                _gameOverStats.text =
                    "Rondas superadas: " + round.RoundsPassed +
                    "\nEnemigos derrotados: " + round.EnemiesDefeated +
                    "\nPuntos ganados: " + round.TotalPointsEarned +
                    "\nPuntos restantes: " + round.Points +
                    "\nMonedas ganadas: " + round.CoinsEarned +
                    "\nBonus de puntos: " + round.BonusDrops +
                    "\n\n" + (round.RewardSaveFailed ? "Saldo no disponible" : "Monedas totales: " + PlayerEconomy.Coins);

                _gameOverPage.Status.text = round.RewardSaveFailed
                    ? "No se pudieron guardar las monedas. Revisa el almacenamiento antes de volver a jugar."
                    : "Recompensa guardada · Tu colección te espera";
                _gameOverPage.Status.color = round.RewardSaveFailed ? UIFactory.Coral : UIFactory.Muted;
                _gameOverPage.Wallet.text = round.RewardSaveFailed
                    ? "SALDO NO DISPONIBLE" : $"{PlayerEconomy.Coins:N0} MONEDAS";
                _gameOverRoot.SetActive(true);
                _gameOverPage.Focus();
                _banner.alpha = 0f;
            }
            else if (!gameOver && _gameOverRoot.activeSelf)
            {
                _gameOverRoot.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- Acciones

        void Retry()
        {
            _lastHealth = -1;
            _bannerPhase = (RoundManager.RoundPhase)(-1);
            _lastPreparation = -1;
            GameMenuController.Instance?.StartGame();
        }

        void BackToMenu()
        {
            _gameOverRoot.SetActive(false);
            GameMenuController.Instance?.ShowMainMenu();
        }
    }
}
