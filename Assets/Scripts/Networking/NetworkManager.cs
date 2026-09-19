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
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        public bool IsHost      { get; private set; }
        public bool IsConnected { get; private set; }

        public event Action<string> OnMessageReceived;

        public const int DEFAULT_PORT = 7777;

        public const string MSG_CONNECTED      = "{\"type\":\"__CONNECTED__\"}";
        public const string MSG_CONNECT_FAILED = "{\"type\":\"__CONNECT_FAILED__\"}";
        public const string MSG_DISCONNECTED   = "{\"type\":\"__DISCONNECTED__\"}";

        private TcpListener   _listener;
        private TcpClient     _tcpClient;
        private NetworkStream _stream;
        private Thread        _receiveThread;

        private readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();

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
                    _tcpClient.NoDelay = true;
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

        public void Send<T>(T msg) where T : BaseMessage
            => SendRaw(JsonUtility.ToJson(msg));

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

        private void BeginReceiving()
        {
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            _receiveThread.Start();
        }

        private void ReceiveLoop()
        {
            try
            {
                var reader = new StreamReader(_stream, Encoding.UTF8);
                while (IsConnected)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
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

