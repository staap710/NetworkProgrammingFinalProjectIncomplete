using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CoinRush.Networking;
using CoinRush.Game;

namespace CoinRush.UI
{
    /// <summary>
    /// Manages the LobbyScene UI: Host / Join buttons, IP display, and the
    /// connect input fields. Drives NetworkManager and GameManager to kick
    /// off the game when both players are ready.
    ///
    /// All text fields use TextMeshPro (TMP_Text / TMP_InputField).
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        [Header("Main Buttons")]
        public Button hostButton;
        public Button joinButton;

        [Header("Host Panel (initially hidden)")]
        public GameObject hostPanel;
        public TMP_Text       hostIPText;
        public TMP_InputField portInputField;
        public TMP_Text       hostStatusText;

        [Header("Join Panel (initially hidden)")]
        public GameObject     joinPanel;
        public TMP_InputField ipInputField;
        public TMP_InputField joinPortInputField;
        public Button         connectButton;
        public TMP_Text       joinStatusText;

        [Header("Shared (optional)")]
        public TMP_Text errorText;

        private bool _connecting;

        void Start()
        {
            if (hostPanel != null) hostPanel.SetActive(false);
            if (joinPanel  != null) joinPanel.SetActive(false);
            if (errorText  != null) errorText.text = "";

            hostButton.onClick.AddListener(OnHostClicked);
            joinButton.onClick.AddListener(OnJoinClicked);
            if (connectButton != null) connectButton.onClick.AddListener(OnConnectClicked);

            if (NetworkManager.Instance != null)
                NetworkManager.Instance.OnMessageReceived += OnNetworkMessage;
        }

        void OnDestroy()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.OnMessageReceived -= OnNetworkMessage;
        }

        // Host flow

        private void OnHostClicked()
        {
            hostButton.interactable = false;
            joinButton.interactable = false;

            int port = ParsePort(portInputField, NetworkManager.DEFAULT_PORT);

            Debug.Log($"[LobbyManager] OnHostClicked — hostPanel={hostPanel}, hostIPText={hostIPText}, NetworkManager.Instance={NetworkManager.Instance}");

            if (hostPanel != null)
            {
                hostPanel.SetActive(true);
                Debug.Log("[LobbyManager] hostPanel activated");

                if (hostIPText != null)
                {
                    string ip = NetworkManager.Instance != null
                        ? NetworkManager.Instance.GetLocalIP()
                        : "NetworkManager NULL!";
                    Debug.Log($"[LobbyManager] Setting hostIPText to IP={ip}, port={port}");
                    hostIPText.text = $"Your IP:  {ip}\n" +
                                      $"Port:      {port}\n\n" +
                                      $"Give this to your opponent!";
                }
                else
                {
                    Debug.LogWarning("[LobbyManager] hostIPText is NULL at runtime!");
                }

                if (hostStatusText != null)
                    hostStatusText.text = "Waiting for opponent to connect...";
            }
            else
            {
                Debug.LogWarning("[LobbyManager] hostPanel is NULL at runtime!");
            }

            GameManager.Instance.SetLocalPlayerId(1);
            NetworkManager.Instance.StartHost(port);
        }

        // Join flow

        private void OnJoinClicked()
        {
            hostButton.interactable = false;
            joinButton.interactable = false;

            if (joinPanel != null)
            {
                joinPanel.SetActive(true);
                if (joinStatusText != null)
                    joinStatusText.text = "Enter the host IP and port, then click Connect.";
            }

            GameManager.Instance.SetLocalPlayerId(2);
        }

        private void OnConnectClicked()
        {
            if (_connecting) return;

            string ip = ipInputField != null ? ipInputField.text.Trim() : "";
            if (string.IsNullOrEmpty(ip))
            {
                SetJoinStatus("Please enter a valid IP address.");
                return;
            }

            int port = ParsePort(joinPortInputField, NetworkManager.DEFAULT_PORT);
            SetJoinStatus($"Connecting to {ip}:{port}...");
            _connecting = true;
            if (connectButton != null) connectButton.interactable = false;

            NetworkManager.Instance.StartClient(ip, port);
        }

        // Network message handler

        private void OnNetworkMessage(string json)
        {
            var baseMsg = JsonUtility.FromJson<BaseMessage>(json);
            if (baseMsg == null) return;

            switch (baseMsg.type)
            {
                case "__CONNECTED__":
                    if (!NetworkManager.Instance.IsHost)
                    {
                        SetJoinStatus("Connected! Waiting for host to start...");
                        NetworkManager.Instance.Send(new HelloMessage { playerId = 2 });
                    }
                    else
                    {
                        SetHostStatus("Opponent connected! Waiting for handshake...");
                    }
                    break;

                case "__CONNECT_FAILED__":
                    SetJoinStatus("Could not connect. Check the IP / port and try again.");
                    _connecting = false;
                    if (connectButton != null) connectButton.interactable = true;
                    break;

                case "__DISCONNECTED__":
                    SetHostStatus("Opponent disconnected.");
                    SetJoinStatus("Disconnected from host.");
                    break;

                case "HELLO":
                    if (NetworkManager.Instance.IsHost)
                    {
                        SetHostStatus("Opponent ready! Starting game...");
                        Invoke(nameof(HostBeginGame), 0.8f);
                    }
                    break;
            }
        }

        private void HostBeginGame() => GameManager.Instance.HostStartGame();

        // Helpers

        private void SetHostStatus(string msg)
        {
            if (hostStatusText != null) hostStatusText.text = msg;
        }

        private void SetJoinStatus(string msg)
        {
            if (joinStatusText != null) joinStatusText.text = msg;
        }

        private static int ParsePort(TMP_InputField field, int fallback)
        {
            if (field != null && int.TryParse(field.text, out int p) && p > 0 && p < 65536)
                return p;
            return fallback;
        }
    }
}
