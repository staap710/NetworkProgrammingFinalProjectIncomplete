using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using CoinRush.Networking;
using CoinRush.Game;

namespace CoinRush.UI
{
    public class ResultsManager : MonoBehaviour
    {
        [Header("Result Text")]
        public TMP_Text winnerText;
        public TMP_Text subtitleText;
        public TMP_Text p1ScoreText;
        public TMP_Text p2ScoreText;

        [Header("Buttons")]
        public Button playAgainButton;
        public Button quitButton;

        void Start()
        {
            if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgain);
            if (quitButton      != null) quitButton.onClick.AddListener(OnQuit);

            if (GameManager.Instance?.LastGameResult != null)
                ShowResults(GameManager.Instance.LastGameResult);
        }

        private void ShowResults(GameOverMessage result)
        {
            int localId = GameManager.Instance?.LocalPlayerId ?? 1;

            if (winnerText != null)
            {
                if (result.winner == 0)        winnerText.text = "TIE GAME!";
                else if (result.winner == localId) winnerText.text = "YOU WIN!";
                else                           winnerText.text = "YOU LOSE!";
            }

            if (subtitleText != null)
            {
                if (result.winner == 0)            subtitleText.text = "Evenly matched!";
                else if (result.winner == localId) subtitleText.text = "Coin collecting champion!";
                else                               subtitleText.text = "Better luck next time...";
            }

            if (p1ScoreText != null) p1ScoreText.text = $"Player 1:  {result.p1Score} coins";
            if (p2ScoreText != null) p2ScoreText.text = $"Player 2:  {result.p2Score} coins";
        }

        private void OnPlayAgain()
        {
            NetworkManager.Instance?.Disconnect();
            GameManager.Instance?.ResetGame();
            SceneManager.LoadScene("LobbyScene");
        }

        private void OnQuit()
        {
            NetworkManager.Instance?.Disconnect();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
