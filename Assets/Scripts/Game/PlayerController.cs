using UnityEngine;
using CoinRush.Networking;

namespace CoinRush.Game
{
    /// <summary>
    /// Attached to the Player prefab.
    ///
    /// isLocalPlayer = true  -> reads keyboard input, sends INPUT messages.
    /// isLocalPlayer = false -> receives INPUT messages, interpolates position.
    ///
    /// Controls:
    ///   Local player 1 (host)  : A/D or Left/Right to move, W/Space/Up to jump.
    ///   Local player 2 (client): same keys (both players run on separate machines).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Identity")]
        public int  playerId;      // 1 = host player, 2 = client player
        public bool isLocalPlayer;

        [Header("Movement")]
        public float moveSpeed  = 5f;
        public float jumpForce  = 12f;

        [Header("Ground Check")]
        public Transform groundCheckPoint;
        public float     groundCheckRadius = 0.1f;
        public LayerMask groundLayer;

        [Header("Visuals")]
        public SpriteRenderer spriteRenderer;
        public Color p1Color = new Color(0.2f, 0.5f, 1f);
        public Color p2Color = new Color(1f, 0.3f, 0.3f);

        // Internal state
        private Rigidbody2D _rb;
        private bool  _isGrounded;
        private int   _inputSeq;
        private float _inputTimer;
        private const float INPUT_RATE = 0.05f; // 20 Hz

        // Remote interpolation
        private Vector2 _remoteTargetPos;
        private bool    _hasRemoteData;
        private int     _lastRemoteSeq = -1;

        // Power-up state
        private float _speedMultiplier = 1f;
        private float _stunTimer;
        public bool IsStunned => _stunTimer > 0f;

        // Lifecycle

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        void Start()
        {
            // Colour-code the sprite
            if (spriteRenderer != null)
                spriteRenderer.color = (playerId == 1) ? p1Color : p2Color;

            _remoteTargetPos = transform.position;

            if (GameManager.Instance == null) return;

            if (isLocalPlayer)
                GameManager.Instance.OnRemoteInput += OnRemoteInputReceived;

            GameManager.Instance.OnPowerupAck += OnPowerupAckReceived;
        }

        void OnDestroy()
        {
            if (GameManager.Instance == null) return;
            if (isLocalPlayer) GameManager.Instance.OnRemoteInput -= OnRemoteInputReceived;
            GameManager.Instance.OnPowerupAck -= OnPowerupAckReceived;
        }

        // Update

        void Update()
        {
            if (!isLocalPlayer)
            {
                SmoothRemotePlayer();
                return;
            }

            // Stun countdown
            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                return; // no input while stunned
            }

            HandleMovementInput();
        }

        void FixedUpdate()
        {
            if (!isLocalPlayer) return;

            // Throttled position broadcast
            _inputTimer += Time.fixedDeltaTime;
            if (_inputTimer >= INPUT_RATE)
            {
                _inputTimer = 0f;
                SendPositionUpdate();
            }
        }

        // Input handling

        private void HandleMovementInput()
        {
            float h = Input.GetAxisRaw("Horizontal");
            _rb.linearVelocity = new Vector2(h * moveSpeed * _speedMultiplier, _rb.linearVelocity.y);

            if (spriteRenderer != null && h != 0f)
                spriteRenderer.flipX = (h < 0f);

            // Ground check
            if (groundCheckPoint != null)
                _isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);

            bool jumpPressed = Input.GetKeyDown(KeyCode.Space)
                            || Input.GetKeyDown(KeyCode.W)
                            || Input.GetKeyDown(KeyCode.UpArrow);
            if (_isGrounded && jumpPressed)
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
        }

        private void SendPositionUpdate()
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;

            NetworkManager.Instance.Send(new InputMessage
            {
                playerId  = playerId,
                x         = transform.position.x,
                y         = transform.position.y,
                velX      = _rb.linearVelocity.x,
                velY      = _rb.linearVelocity.y,
                facingLeft = spriteRenderer != null && spriteRenderer.flipX,
                seq       = _inputSeq++
            });
        }

        // Remote player interpolation

        private void OnRemoteInputReceived(InputMessage msg)
        {
            // Only apply to the OTHER player; also discard out-of-order packets
            if (msg.playerId == playerId || msg.seq <= _lastRemoteSeq) return;
            _lastRemoteSeq    = msg.seq;
            _remoteTargetPos  = new Vector2(msg.x, msg.y);
            if (spriteRenderer != null) spriteRenderer.flipX = msg.facingLeft;
            _hasRemoteData = true;
        }

        private void SmoothRemotePlayer()
        {
            if (!_hasRemoteData) return;
            transform.position = Vector2.Lerp(transform.position, _remoteTargetPos, Time.deltaTime * 15f);
        }

        // Power-up effects

        private void OnPowerupAckReceived(PowerupAckMessage ack)
        {
            if (!isLocalPlayer) return;

            switch (ack.powerupType)
            {
                case "SPEED" when ack.collectedBy == playerId:
                    ApplySpeedBoost(1.75f, 5f);
                    break;

                case "STUN" when ack.collectedBy != playerId:
                    // The OTHER player collected a stun -> we are stunned
                    ApplyStun(2f);
                    break;
            }
        }

        public void ApplySpeedBoost(float multiplier, float duration)
        {
            _speedMultiplier = multiplier;
            CancelInvoke(nameof(ResetSpeed));
            Invoke(nameof(ResetSpeed), duration);
        }

        private void ResetSpeed() => _speedMultiplier = 1f;

        public void ApplyStun(float duration)
        {
            _stunTimer = duration;
            _rb.linearVelocity = Vector2.zero;
        }

        // Gizmo for ground check
        void OnDrawGizmosSelected()
        {
            if (groundCheckPoint == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
}
