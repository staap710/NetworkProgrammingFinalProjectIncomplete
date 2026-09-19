using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CoinRush.Networking
{
    /// <summary>
    /// Manages the raw TCP P2P connection.
    ///
    /// One player acts as HOST  -> calls StartHost()  -> opens a TcpListener.
    /// The other acts as CLIENT -> calls StartClient() -> connects to the host.
    ///
    /// All sending is synchronous (tiny JSON lines).
    /// All receiving runs on a background thread and enqueues messages so they
    /// can be safely dispatched on the Unity main thread in Update().
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        // Singleton
        public static NetworkManager Instance { get; private set; }

        // Public state
        public bool IsHost      { get; private set; }
        public bool IsConnected { get; private set; }

        // Events (always raised on the main thread)
        /// <summary>Fired when a raw JSON message string arrives.</summary>
        public event Action<string> OnMessageReceived;

        public const int DEFAULT_PORT = 7777;

        // Internal marker strings (never sent over the wire)
        public const string MSG_CONNECTED      = "{\"type\":\"__CONNECTED__\"}";
        public const string MSG_CONNECT_FAILED = "{\"type\":\"__CONNECT_FAILED__\"}";
        public const string MSG_DISCONNECTED   = "{\"type\":\"__DISCONNECTED__\"}";

        private TcpListener   _listener;
        private TcpClient     _tcpClient;
        private NetworkStream _stream;
        private Thread        _receiveThread;

        // Thread-safe queue; drained on the Unity main thread in Update()
        private readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();

        // Lifecycle

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            while (_incoming.TryDequeue(out string json))
                OnMessageReceived?.Invoke(json);
        }

        void OnDestroy()        => Disconnect();
        void OnApplicationQuit() => Disconnect();

        // Public API

        /// <summary>Open a listener and wait for one client to connect.</summary>
        public void StartHost(int port = DEFAULT_PORT)
        {
            IsHost = true;
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Debug.Log($"[Network] Listening on port {port}...");

            Thread t = new Thread(() =>
            {
                try
                {
                    _tcpClient       = _listener.AcceptTcpClient();
                    _tcpClient.NoDelay = true;   // minimise latency
                    _stream    = _tcpClient.GetStream();
                    IsConnected = true;
                    _incoming.Enqueue(MSG_CONNECTED);
                    BeginReceiving();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Network] Accept error: {ex.Message}");
                    _incoming.Enqueue(MSG_CONNECT_FAILED);
                }
            }) { IsBackground = true };
            t.Start();
        }

        /// <summary>Connect to a host by IP and port.</summary>
        public void StartClient(string ip, int port = DEFAULT_PORT)
        {
            IsHost = false;
            Thread t = new Thread(() =>
            {
                try
                {
                    _tcpClient       = new TcpClient();
                    _tcpClient.NoDelay = true;
                    _tcpClient.Connect(ip, port);
                    _stream    = _tcpClient.GetStream();
                    IsConnected = true;
                    _incoming.Enqueue(MSG_CONNECTED);
                    BeginReceiving();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Network] Connect error: {ex.Message}");
                    _incoming.Enqueue(MSG_CONNECT_FAILED);
                }
            }) { IsBackground = true };
            t.Start();
        }

        /// <summary>Serialise <paramref name="msg"/> to JSON and send it.</summary>
        public void Send<T>(T msg) where T : BaseMessage
            => SendRaw(JsonUtility.ToJson(msg));

        /// <summary>Send a raw JSON string (must not contain newlines).</summary>
        public void SendRaw(string json)
        {
            if (_stream == null || !IsConnected) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(json + "\n");
                lock (_stream)
                    _stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] Send error: {ex.Message}");
            }
        }

        /// <summary>Close the socket gracefully.</summary>
        public void Disconnect()
        {
            IsConnected = false;
            try { _stream?.Close(); }    catch { }
            try { _tcpClient?.Close(); } catch { }
            try { _listener?.Stop(); }   catch { }
            _stream    = null;
            _tcpClient = null;
            _listener  = null;
        }

        /// <summary>Returns the first non-loopback IPv4 address of this machine.</summary>
        public string GetLocalIP()
        {
            try
            {
                foreach (IPAddress addr in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
                        return addr.ToString();
                }
            }
            catch { }
            return "127.0.0.1";
        }

        // Private helpers

        private void BeginReceiving()
        {
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            _receiveThread.Start();
        }

        /// <summary>Reads newline-delimited JSON from the stream on a background thread.</summary>
        private void ReceiveLoop()
        {
            try
            {
                var reader = new StreamReader(_stream, Encoding.UTF8);
                while (IsConnected)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;   // clean close
                    if (!string.IsNullOrWhiteSpace(line))
                        _incoming.Enqueue(line);
                }
            }
            catch (Exception ex)
            {
                if (IsConnected)
                    Debug.LogError($"[Network] Receive error: {ex.Message}");
            }
            finally
            {
                IsConnected = false;
                _incoming.Enqueue(MSG_DISCONNECTED);
            }
        }
    }
}
