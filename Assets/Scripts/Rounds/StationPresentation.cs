using Game.Player;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>Show prices near the player, keeping the combat field free of floating text.</summary>
    public sealed class StationPresentation : MonoBehaviour
    {
        GameObject _sign;
        float _nextCheck;

        public void Initialize(GameObject sign)
        {
            _sign = sign;
            _sign.SetActive(false);
        }

        void Update()
        {
            if (Time.unscaledTime < _nextCheck || _sign == null) return;
            _nextCheck = Time.unscaledTime + .15f;
            var player = PlayerSphereController.Instance;
            var rounds = RoundManager.Instance;
            bool show = player != null && rounds != null && rounds.IsRunActive &&
                (player.transform.position - transform.position).sqrMagnitude < 5.2f * 5.2f;
            if (_sign.activeSelf != show) _sign.SetActive(show);
        }
    }
}
