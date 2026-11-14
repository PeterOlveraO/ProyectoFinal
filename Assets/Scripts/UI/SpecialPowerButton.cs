using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Botón del poder especial (invencibilidad + eliminar enemigos de la zona).
    /// Vive sobre el joystick de ataque, en el HUD, y muestra la recarga con un reloj circular.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpecialPowerButton : MonoBehaviour
    {
        Button _button;
        Image _cooldown;
        TMPro.TMP_Text _label;
        RectTransform _root;
        int _displayedSeconds = int.MinValue;

        void Start()
        {
            Build();
        }

        void Update()
        {
            if (_button == null) return;
            if (_root.gameObject.activeSelf != GameFlow.Playing)
                _root.gameObject.SetActive(GameFlow.Playing);
            if (!GameFlow.Playing) return;

            var player = PlayerSphereController.Instance;
            if (player == null) return;

            float cooldown = player.PowerCooldownRemaining;
            float invincibility = player.InvincibilityRemaining;
            bool ready = cooldown <= 0f;

            _button.interactable = GameFlow.Playing && ready;

            if (invincibility > 0.05f)
            {
                SetSeconds(Mathf.CeilToInt(invincibility));
                _cooldown.color = new Color(0.18f, 0.85f, 0.45f, 0.45f);
            }
            else if (!ready)
            {
                SetSeconds(Mathf.CeilToInt(cooldown));
                _cooldown.color = new Color(0.02f, 0.01f, 0.06f, 0.62f);
            }
            else
            {
                SetSeconds(0);
                _cooldown.color = new Color(0f, 0f, 0f, 0f);
            }

            _cooldown.fillAmount = ready ? 0f : Mathf.Clamp01(cooldown / player.PowerCooldownDuration);
        }

        void SetSeconds(int seconds)
        {
            if (_displayedSeconds == seconds) return;
            _displayedSeconds = seconds;
            _label.text = seconds == 0 ? "PODER" : seconds.ToString();
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        public void Activate()
        {
            if (!GameFlow.Playing) return;
            PlayerSphereController.Instance?.ActivateInvincibility();
        }

        void Build()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[SpecialPowerButton] No hay Canvas padre en el HUD.", this);
                enabled = false;
                return;
            }

            _root = UIFactory.SafeRoot(canvas.transform, "PowerSafeArea");
            var rect = UIFactory.CreateRect("PowerButton", _root);
            UIFactory.Place(rect, new Vector2(1f, 0f), new Vector2(1f, 0f),
                            new Vector2(-48f, 310f), new Vector2(140f, 140f));

            var halo = UIFactory.CreateImage("Halo", rect, ProceduralSprites.Circle,
                                             new Color(1f, 0.86f, 0.3f, 0.28f), false);
            UIFactory.Stretch(halo.rectTransform, -12f);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ProceduralSprites.Circle;
            image.color = UIFactory.Sun;
            UIFactory.AddShadow(image, new Color(0f, 0f, 0f, 0.4f), new Vector2(0f, -6f));
            UIFactory.AddOutline(image, UIFactory.Coral, 4f);

            _button = rect.gameObject.AddComponent<Button>();
            _button.targetGraphic = image;
            _button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            rect.gameObject.AddComponent<UIFocusFeedback>();

            var colors = _button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.55f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            _button.colors = colors;
            _button.onClick.AddListener(Activate);

            var overlay = UIFactory.CreateImage("Cooldown", rect, ProceduralSprites.Circle,
                                                new Color(0.02f, 0.01f, 0.06f, 0.62f), false);
            UIFactory.Stretch(overlay.rectTransform, 6f);
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)Image.Origin360.Top;
            overlay.fillClockwise = true;
            overlay.fillAmount = 0f;
            _cooldown = overlay;

            _label = UIFactory.CreateText("Label", rect, "PODER", 27, UIFactory.Ink);
            UIFactory.Stretch(_label.rectTransform, 12f);
        }
    }
}
