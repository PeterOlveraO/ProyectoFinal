using Game.Economy;
using Game.Player;
using Game.Rendering;
using Game.Rounds;
using Game.Upgrades;
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
        Text _healthLabel;
        Text _roundLabel;
        Text _enemiesLabel;
        Text _pointsLabel;
        Text _coinsLabel;
        Text _bonusLabel;
        Text _powerupLabel;
        Button _drinkButton;
        Text _drinkLabel;
        int _nearbyDrink = -1;
        Button _upgradeButton;
        Text _upgradeLabel;
        int _nearbyUpgrade = -1;

        CanvasGroup _banner;
        Text _bannerTitle;
        Text _bannerSubtitle;
        float _bannerTimer;
        RoundManager.RoundPhase _bannerPhase = (RoundManager.RoundPhase)(-1);
        int _bannerRound = -1;

        Image _damageFlash;
        float _flashAmount;
        int _lastHealth = -1;

        GameObject _gameOverRoot;
        Text _gameOverStats;

        void Start()
        {
            Build();
        }

        void Update()
        {
            var round = RoundManager.Instance;
            var player = PlayerSphereController.Instance;
            var menu = GameMenuController.Instance;

            bool playing = menu == null || menu.IsPlaying;
            bool gameOver = round != null && round.Phase == RoundManager.RoundPhase.GameOver;

            if (_statusPanel != null)
                _statusPanel.gameObject.SetActive(true);

            UpdateHealth(player);
            UpdateNumbers(round, player);
            UpdateBanner(round, playing);
            UpdateFlash();
            UpdateGameOver(round, gameOver);
            UpdateDrink(round, player, playing && !gameOver && (menu == null || !menu.IsPaused));
            UpdateUpgrade(round, player, playing && !gameOver && (menu == null || !menu.IsPaused));
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

            var root = UIFactory.CreateRect("RunHud", canvas.transform);
            UIFactory.Stretch(root);

            // Flash rojo al recibir daño (detrás del resto del HUD).
            _damageFlash = UIFactory.CreateImage("DamageFlash", root, null,
                                                new Color(1f, 0.1f, 0.12f, 0f), false);
            UIFactory.Stretch(_damageFlash.rectTransform);

            BuildStatusPanel(root);
            BuildBanner(root);
            BuildDrinkButton(root);
            BuildUpgradeButton(root);

            // La pantalla de derrota vive en su propio Canvas por encima de todo el HUD.
            var overlay = UIFactory.CreateCanvas("UI_GameOver", 120);
            BuildGameOver((RectTransform)overlay.transform);
        }

        void BuildStatusPanel(Transform parent)
        {
            _statusPanel = UIFactory.CreateRect("RunStatus", parent);
            UIFactory.Place(_statusPanel, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(24f, -24f), new Vector2(470f, 262f));

            var background = UIFactory.CreateImage("Background", _statusPanel,
                                                   ProceduralSprites.RoundedRect,
                                                   new Color(0.06f, 0.03f, 0.12f, 0.74f), false, true);
            UIFactory.Stretch(background.rectTransform);
            UIFactory.AddOutline(background, new Color(1f, 1f, 1f, 0.12f), 2f);

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

            _powerupLabel = UIFactory.CreateText("PowerupStatus", _statusPanel, "", 21,
                                                 UIFactory.Sky, TextAnchor.MiddleLeft);
            UIFactory.Place(_powerupLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(22f, -208f), new Vector2(430f, 30f));
        }

        void BuildDrinkButton(Transform parent)
        {
            _drinkButton = UIFactory.CreateButton(parent, "BuyDrink", "", UIFactory.Sky,
                                                   new Vector2(650f, 100f), BuyDrink, 28);
            UIFactory.Place((RectTransform)_drinkButton.transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                            new Vector2(0f, 35f), new Vector2(650f, 100f));
            _drinkLabel = _drinkButton.GetComponentInChildren<Text>();
            _drinkButton.gameObject.SetActive(false);
        }

        void UpdateDrink(RoundManager round, PlayerSphereController player, bool playing)
        {
            var stations = round != null ? round.GetComponent<DrinkStations>() : null;
            _nearbyDrink = playing && player != null && stations != null && round.Phase != RoundManager.RoundPhase.Idle
                ? stations.NearbyOffer(player.transform.position) : -1;
            if (_drinkButton == null) return;
            _drinkButton.gameObject.SetActive(_nearbyDrink >= 0);
            if (_nearbyDrink < 0) return;
            _drinkLabel.text = stations.Has((DrinkType)_nearbyDrink) ?
                "BEBIDA " + DrinkStations.Names[_nearbyDrink] + " · ADQUIRIDA" :
                "BEBIDA " + DrinkStations.Names[_nearbyDrink] + " · " + DrinkStations.Prices[_nearbyDrink] + " PUNTOS";
            _drinkButton.interactable = !stations.Has((DrinkType)_nearbyDrink) &&
                                         round.Points >= DrinkStations.Prices[_nearbyDrink];
        }

        void BuyDrink()
        {
            RoundManager.Instance?.GetComponent<DrinkStations>()?.Buy(_nearbyDrink);
        }

        void BuildUpgradeButton(Transform parent)
        {
            _upgradeButton = UIFactory.CreateButton(parent, "BuyShotUpgrade", "", UIFactory.Sun,
                                                     new Vector2(650f, 100f), BuyUpgrade, 28);
            UIFactory.Place((RectTransform)_upgradeButton.transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                            new Vector2(0f, 145f), new Vector2(650f, 100f));
            _upgradeLabel = _upgradeButton.GetComponentInChildren<Text>();
            _upgradeButton.gameObject.SetActive(false);
        }

        void UpdateUpgrade(RoundManager round, PlayerSphereController player, bool playing)
        {
            var stations = player != null ? player.GetComponent<ShotUpgradeStations>() : null;
            _nearbyUpgrade = playing && player != null && stations != null && round != null && round.IsRunActive
                ? stations.NearbyOffer(player.transform.position) : -1;
            if (_upgradeButton == null) return;
            _upgradeButton.gameObject.SetActive(_nearbyUpgrade >= 0);
            if (_nearbyUpgrade < 0) return;
            _upgradeLabel.text = stations.Has(_nearbyUpgrade)
                ? "DISPARO " + ShotUpgradeStations.Names[_nearbyUpgrade] + " · ADQUIRIDO"
                : "DISPARO " + ShotUpgradeStations.Names[_nearbyUpgrade] + " · " +
                  ShotUpgradeStations.Prices[_nearbyUpgrade] + " PUNTOS";
            _upgradeButton.interactable = !stations.Has(_nearbyUpgrade) &&
                                          round.Points >= ShotUpgradeStations.Prices[_nearbyUpgrade];
        }

        void BuyUpgrade()
        {
            PlayerSphereController.Instance?.GetComponent<ShotUpgradeStations>()?.Buy(_nearbyUpgrade);
        }

        void BuildBanner(Transform parent)
        {
            var rect = UIFactory.CreateRect("RoundBanner", parent);
            UIFactory.Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -130f), new Vector2(1100f, 190f));

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
            var root = UIFactory.CreateRect("GameOver", parent);
            UIFactory.Stretch(root);
            _gameOverRoot = root.gameObject;

            var dim = UIFactory.CreateImage("Dim", root, null, new Color(0.03f, 0.01f, 0.07f, 0.86f), true);
            UIFactory.Stretch(dim.rectTransform);

            var card = UIFactory.CreateCard(root, new Vector2(980f, 660f),
                                            UIFactory.DeepViolet, UIFactory.Coral);
            UIFactory.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(980f, 660f));

            var title = UIFactory.CreateText("Title", card, "DERROTADO", 96, UIFactory.Coral);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 228f), new Vector2(900f, 130f));
            UIFactory.AddOutline(title, UIFactory.Ink, 6f);

            _gameOverStats = UIFactory.CreateText("Stats", card, "", 34, UIFactory.Cream);
            UIFactory.Place(_gameOverStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 42f), new Vector2(860f, 270f));

            var retry = UIFactory.CreateButton(card, "RetryButton", "REINTENTAR", UIFactory.Lime,
                                               new Vector2(400f, 104f), Retry, 40);
            UIFactory.Place((RectTransform)retry.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(-215f, -218f), new Vector2(400f, 104f));

            var menu = UIFactory.CreateButton(card, "MenuButton", "MENÚ", UIFactory.Sky,
                                              new Vector2(400f, 104f), BackToMenu, 42);
            UIFactory.Place((RectTransform)menu.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(215f, -218f), new Vector2(400f, 104f));

            _gameOverRoot.SetActive(false);
        }

        // ---------------------------------------------------------------- Estado

        void UpdateHealth(PlayerSphereController player)
        {
            if (_healthFill == null || player == null) return;

            float ratio = player.Health01;
            _healthFill.fillAmount = ratio;
            _healthFill.color = Color.Lerp(UIFactory.Coral, UIFactory.Lime, ratio);
            _healthLabel.text = player.Health + " / " + player.MaxHealth;

            if (_lastHealth >= 0 && player.Health < _lastHealth) _flashAmount = 0.5f;
            _lastHealth = player.Health;
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
                if (_powerupLabel != null) _powerupLabel.text = "";
                return;
            }

            _roundLabel.text = "RONDA " + round.Round;
            _enemiesLabel.text = round.EnemiesDefeatedThisRound + " / " + round.EnemiesThisRound;
            _pointsLabel.text = "PUNTOS " + round.Points;
            _coinsLabel.text = "PREMIO +" + round.CoinsEarned;
            _bonusLabel.text = "BONUS x" + round.BonusDrops + "  ·  CARTERA " + PlayerEconomy.Coins;
            _powerupLabel.text = (round.DoublePointsRemaining > 0f
                    ? "PUNTOS x2 " + Mathf.CeilToInt(round.DoublePointsRemaining) + "s   " : "") +
                (round.ShieldRemaining > 0f
                    ? "ESCUDO " + Mathf.CeilToInt(round.ShieldRemaining) + "s" : "");
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
                        SetBanner(round.IsBossRound ? "¡RONDA DE JEFE!" : "RONDA " + round.Round,
                                  "Elimina a " + round.EnemiesThisRound + " enemigos" +
                                  (round.IsBossRound ? " · jefe legendario" : ""));
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
                _bannerSubtitle.text = "Prepárate… " + Mathf.CeilToInt(round.PreparationRemaining);

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
                    "\n\nMonedas totales: " + PlayerEconomy.Coins;

                _gameOverRoot.SetActive(true);
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
            _gameOverRoot.SetActive(false);
            _lastHealth = -1;
            RoundManager.Instance?.StartRun();
        }

        void BackToMenu()
        {
            _gameOverRoot.SetActive(false);
            GameMenuController.Instance?.ShowMainMenu();
        }
    }
}
