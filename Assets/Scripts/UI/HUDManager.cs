using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CoinRush.Networking;
using CoinRush.Game;

namespace CoinRush.UI
{
    /// <summary>
    /// In-game heads-up display. All text fields use TextMeshPro (TMP_Text).
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("Scores")]
        public TMP_Text p1ScoreText;
        public TMP_Text p2ScoreText;
        public TMP_Text p1Label;
        public TMP_Text p2Label;

        [Header("Timer")]
        public TMP_Text timerText;

        [Header("Notifications")]
        public GameObject stunOverlay;      // semi-transparent red panel
        public GameObject speedOverlay;     // semi-transparent cyan panel
        public TMP_Text   notificationText; // brief popup message

        void Start()
        {
            if (stunOverlay  != null) stunOverlay.SetActive(false);
            if (speedOverlay != null) speedOverlay.SetActive(false);
            if (notificationText != null) notificationText.gameObject.SetActive(false);

            if (GameManager.Instance == null) return;

            int localId = GameManager.Instance.LocalPlayerId;
            if (p1Label != null) p1Label.text = (localId == 1) ? "YOU  (P1)" : "P1";
            if (p2Label != null) p2Label.text = (localId == 2) ? "YOU  (P2)" : "P2";

            UpdateScores(0, 0);
            UpdateTimer(GameManager.Instance.gameDurationSeconds);

            GameManager.Instance.OnScoreUpdated += UpdateScores;
            GameManager.Instance.OnTimerUpdated  += UpdateTimer;
            GameManager.Instance.OnPowerupAck    += OnPowerupAck;
        }

        void OnDestroy()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnScoreUpdated -= UpdateScores;
            GameManager.Instance.OnTimerUpdated  -= UpdateTimer;
            GameManager.Instance.OnPowerupAck    -= OnPowerupAck;
        }

        // Callbacks

        private void UpdateScores(int p1, int p2)
        {
            if (p1ScoreText != null) p1ScoreText.text = p1.ToString();
            if (p2ScoreText != null) p2ScoreText.text = p2.ToString();
        }

        private void UpdateTimer(float remaining)
        {
            if (timerText == null) return;
            int total = Mathf.CeilToInt(Mathf.Max(remaining, 0f));
            timerText.text  = $"{total / 60:00}:{total % 60:00}";
            timerText.color = (total <= 10) ? Color.red : Color.white;
        }

        private void OnPowerupAck(PowerupAckMessage ack)
        {
            int localId = GameManager.Instance?.LocalPlayerId ?? 1;

            if (ack.powerupType == "SPEED" && ack.collectedBy == localId)
            {
                ShowNotification("SPEED BOOST!", Color.cyan, 3f);
                if (speedOverlay != null)
                {
                    speedOverlay.SetActive(true);
                    Invoke(nameof(HideSpeedOverlay), 5f);
                }
            }
            else if (ack.powerupType == "SPEED" && ack.collectedBy != localId)
            {
                ShowNotification("Opponent got SPEED BOOST!", Color.yellow, 2f);
            }
            else if (ack.powerupType == "STUN" && ack.collectedBy != localId)
            {
                ShowNotification("STUNNED!", Color.red, 2f);
                if (stunOverlay != null)
                {
                    stunOverlay.SetActive(true);
                    Invoke(nameof(HideStunOverlay), 2f);
                }
            }
            else if (ack.powerupType == "STUN" && ack.collectedBy == localId)
            {
                ShowNotification("Stunned your opponent!", Color.green, 2f);
            }
        }

        private void ShowNotification(string msg, Color color, float duration)
        {
            if (notificationText == null) return;
            notificationText.text  = msg;
            notificationText.color = color;
            notificationText.gameObject.SetActive(true);
            CancelInvoke(nameof(HideNotification));
            Invoke(nameof(HideNotification), duration);
        }

        private void HideNotification()  => notificationText?.gameObject.SetActive(false);
        private void HideStunOverlay()   => stunOverlay?.SetActive(false);
        private void HideSpeedOverlay()  => speedOverlay?.SetActive(false);
    }
}
