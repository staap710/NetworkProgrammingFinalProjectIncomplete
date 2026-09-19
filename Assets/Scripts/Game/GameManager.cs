using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using CoinRush.Networking;

namespace CoinRush.Game
{
    /// <summary>
    /// Central game-state manager (DontDestroyOnLoad).
    ///
    /// Responsibilities:
    ///   - Parses every incoming JSON message and routes it to the right subsystem.
    ///   - HOST: drives the countdown timer, arbitrates coin/powerup races,
    ///           broadcasts SCORE_UPDATE and GAME_OVER, triggers scene loads.
    ///   - CLIENT: mirrors authoritative state received from the host.
    ///   - Stores PendingStartMessage so GameScene objects can read it on Start().
    ///   - Stores LastGameResult so ResultScene can display the outcome.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // Singleton
        public static GameManager Instance { get; private set; }

        // Inspector tweaks
        [Header("Game Settings")]
        public int gameDurationSeconds = 90;
        public int coinCount = 20;

        // Coin spawn bounds (used by host to generate positions)
        [Header("Coin Spawn Area")]
        public float coinAreaMinX = -8f;
        public float coinAreaMaxX =  8f;
        public float coinAreaMinY = -1f;
        public float coinAreaMaxY =  4f;

        // Runtime read-only state
        public int   LocalPlayerId   { get; private set; }
        public int   P1Score         { get; private set; }
        public int   P2Score         { get; private set; }
        public float TimeRemaining   { get; private set; }
        public bool  GameRunning     { get; private set; }

        // Shared data written before GameScene loads
        public StartMessage  PendingStartMessage { get; private set; }
        public GameOverMessage LastGameResult    { get; private set; }

        // Events raised on the main thread
        public event Action<int, int>          OnScoreUpdated;   // (p1, p2)
        public event Action<float>             OnTimerUpdated;   // seconds remaining
        public event Action<GameOverMessage>   OnGameOver;
        public event Action<InputMessage>      OnRemoteInput;
        public event Action<CoinAckMessage>    OnCoinAck;
        public event Action<PowerupAckMessage> OnPowerupAck;
        public event Action<PowerupSpawnMessage> OnPowerupSpawn;

        // Host-side arbitration arrays
        private int[] _coinClaimedBy;
        private int[] _powerupClaimedBy;

        // Host-side score broadcast timer
        private float _scoreBroadcastTimer;
        private const float SCORE_BROADCAST_INTERVAL = 1f;

        // Lifecycle

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.OnMessageReceived += HandleMessage;
        }

        void OnDisable()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.OnMessageReceived -= HandleMessage;
        }

        void Update()
        {
            if (!GameRunning) return;

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining < 0f) TimeRemaining = 0f;
            OnTimerUpdated?.Invoke(TimeRemaining);

            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                _scoreBroadcastTimer += Time.deltaTime;
                if (_scoreBroadcastTimer >= SCORE_BROADCAST_INTERVAL)
                {
                    _scoreBroadcastTimer = 0f;
                    HostBroadcastScore();
                }

                if (TimeRemaining <= 0f)
                {
                    GameRunning = false;
                    HostSendGameOver();
                }
            }
        }

        // Public helpers

        public void SetLocalPlayerId(int id) => LocalPlayerId = id;

        /// <summary>
        /// Called by LobbyManager on the HOST after the client's HELLO arrives.
        /// Generates coin positions, sends START, then loads GameScene.
        /// </summary>
        public void HostStartGame()
        {
            float[] xs = new float[coinCount];
            float[] ys = new float[coinCount];
            for (int i = 0; i < coinCount; i++)
            {
                xs[i] = UnityEngine.Random.Range(coinAreaMinX, coinAreaMaxX);
                ys[i] = UnityEngine.Random.Range(coinAreaMinY, coinAreaMaxY);
            }

            var msg = new StartMessage
            {
                timerSeconds    = gameDurationSeconds,
                coinCount       = coinCount,
                coinPositionsX  = xs,
                coinPositionsY  = ys,
                map             = 1
            };

            // Send to client first, then start locally
            NetworkManager.Instance.Send(msg);
            InitGameState(msg);
        }

        // ── Coin arbitration (public so Coin.cs can call on host) ─────────────

        public void InitCoinArbitration(int count)
            => _coinClaimedBy = new int[count];

        /// <summary>
        /// Called when the LOCAL player (host) touches a coin.
        /// Routes directly through arbitration without going over the wire.
        /// </summary>
        public void LocalCoinCollected(int coinId, int playerId)
            => ArbiterHandleCoin(new CoinCollectedMessage { coinId = coinId, playerId = playerId });

        // ── Powerup arbitration (public so Powerup.cs can call on host) ───────

        public void InitPowerupArbitration(int count)
            => _powerupClaimedBy = new int[count];

        /// <summary>
        /// Called when the LOCAL player (host) touches a power-up.
        /// Routes directly through arbitration without going over the wire.
        /// </summary>
        public void LocalPowerupCollected(int powerupId, int playerId)
            => ArbiterHandlePowerup(new PowerupCollectedMessage { powerupId = powerupId, playerId = playerId });

        /// <summary>
        /// Called by PowerupSpawner after resolving the power-up type.
        /// Sends POWERUP_ACK to client and fires the local event.
        /// </summary>
        public void PublishPowerupAck(PowerupAckMessage ack)
        {
            NetworkManager.Instance.Send(ack);
            OnPowerupAck?.Invoke(ack);
        }

        public void AddScore(int playerId, int amount = 1)
        {
            if (playerId == 1) P1Score += amount;
            else               P2Score += amount;
            OnScoreUpdated?.Invoke(P1Score, P2Score);
        }

        /// <summary>Reset state for a new game session (called before returning to Lobby).</summary>
        public void ResetGame()
        {
            P1Score            = 0;
            P2Score            = 0;
            TimeRemaining      = 0f;
            GameRunning        = false;
            PendingStartMessage = null;
            LastGameResult     = null;
            _coinClaimedBy     = null;
            _powerupClaimedBy  = null;
        }

        // Private helpers

        private void InitGameState(StartMessage msg)
        {
            PendingStartMessage = msg;
            TimeRemaining       = msg.timerSeconds;
            P1Score             = 0;
            P2Score             = 0;
            GameRunning         = true;
            _scoreBroadcastTimer = 0f;
            InitCoinArbitration(msg.coinCount);
            SceneManager.LoadScene("GameScene");
        }

        private void HostBroadcastScore()
        {
            var msg = new ScoreUpdateMessage { p1Score = P1Score, p2Score = P2Score };
            NetworkManager.Instance.Send(msg);
            OnScoreUpdated?.Invoke(P1Score, P2Score);
        }

        private void HostSendGameOver()
        {
            int winner = P1Score > P2Score ? 1 : (P2Score > P1Score ? 2 : 0);
            var msg = new GameOverMessage { winner = winner, p1Score = P1Score, p2Score = P2Score };
            NetworkManager.Instance.Send(msg);
            FinaliseGameOver(msg);
        }

        private void FinaliseGameOver(GameOverMessage msg)
        {
            LastGameResult = msg;
            GameRunning    = false;
            OnGameOver?.Invoke(msg);
            StartCoroutine(LoadResultsSceneDelayed(2f));
        }

        private IEnumerator LoadResultsSceneDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            SceneManager.LoadScene("ResultScene");
        }

        // Coin arbitration logic

        private void ArbiterHandleCoin(CoinCollectedMessage req)
        {
            if (_coinClaimedBy == null || req.coinId >= _coinClaimedBy.Length) return;

            // If not yet claimed, award it
            if (_coinClaimedBy[req.coinId] == 0)
            {
                _coinClaimedBy[req.coinId] = req.playerId;
                AddScore(req.playerId);
            }

            // Always ACK so both machines remove the coin
            var ack = new CoinAckMessage
            {
                coinId      = req.coinId,
                collectedBy = _coinClaimedBy[req.coinId]
            };
            if (NetworkManager.Instance.IsConnected)
                NetworkManager.Instance.Send(ack);
            OnCoinAck?.Invoke(ack);
        }

        // Powerup arbitration logic

        private void ArbiterHandlePowerup(PowerupCollectedMessage req)
        {
            if (_powerupClaimedBy == null || req.powerupId >= _powerupClaimedBy.Length) return;
            if (_powerupClaimedBy[req.powerupId] != 0) return; // already taken

            _powerupClaimedBy[req.powerupId] = req.playerId;

            // Ask PowerupSpawner to finalise (it knows the type)
            var spawner = FindFirstObjectByType<PowerupSpawner>();
            spawner?.HostConfirmPowerup(req.powerupId, req.playerId);
        }

        // Main message router

        private void HandleMessage(string json)
        {
            var baseMsg = JsonUtility.FromJson<BaseMessage>(json);
            if (baseMsg == null) return;

            switch (baseMsg.type)
            {
                // ── Lobby handshake ───────────────────────────────────────────
                case "HELLO":
                    // Handled by LobbyManager; ignore here
                    break;

                // ── Game start ────────────────────────────────────────────────
                case "START":
                    if (NetworkManager.Instance != null && !NetworkManager.Instance.IsHost)
                    {
                        var msg = JsonUtility.FromJson<StartMessage>(json);
                        InitGameState(msg);
                    }
                    break;

                // ── Player position sync ──────────────────────────────────────
                case "INPUT":
                    OnRemoteInput?.Invoke(JsonUtility.FromJson<InputMessage>(json));
                    break;

                // ── Coin flow ─────────────────────────────────────────────────
                case "COIN_COLLECTED":
                    if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
                        ArbiterHandleCoin(JsonUtility.FromJson<CoinCollectedMessage>(json));
                    break;

                case "COIN_ACK":
                    OnCoinAck?.Invoke(JsonUtility.FromJson<CoinAckMessage>(json));
                    break;

                // ── Score / game-over ─────────────────────────────────────────
                case "SCORE_UPDATE":
                    if (NetworkManager.Instance != null && !NetworkManager.Instance.IsHost)
                    {
                        var msg = JsonUtility.FromJson<ScoreUpdateMessage>(json);
                        P1Score = msg.p1Score;
                        P2Score = msg.p2Score;
                        OnScoreUpdated?.Invoke(P1Score, P2Score);
                    }
                    break;

                case "GAME_OVER":
                    FinaliseGameOver(JsonUtility.FromJson<GameOverMessage>(json));
                    break;

                // ── Powerup flow ──────────────────────────────────────────────
                case "POWERUP_COLLECTED":
                    if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
                        ArbiterHandlePowerup(JsonUtility.FromJson<PowerupCollectedMessage>(json));
                    break;

                case "POWERUP_ACK":
                    OnPowerupAck?.Invoke(JsonUtility.FromJson<PowerupAckMessage>(json));
                    break;

                case "POWERUP_SPAWN":
                    OnPowerupSpawn?.Invoke(JsonUtility.FromJson<PowerupSpawnMessage>(json));
                    break;

                // ── Internal markers ──────────────────────────────────────────
                case "__DISCONNECTED__":
                    Debug.Log("[GameManager] Remote player disconnected.");
                    GameRunning = false;
                    break;
            }
        }
    }
}
