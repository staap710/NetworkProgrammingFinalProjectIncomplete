using UnityEngine;
using CoinRush.Networking;

namespace CoinRush.Game
{
    /// <summary>
    /// Attached to the Coin prefab.
    ///
    /// When the LOCAL player enters the trigger:
    ///   - HOST:   calls GameManager.LocalCoinCollected() directly.
    ///   - CLIENT: sends COIN_COLLECTED over the network.
    ///
    /// The coin destroys itself on receipt of COIN_ACK (from GameManager event),
    /// ensuring both machines remove the same coin at the same time regardless of
    /// who picked it up and regardless of race conditions.
    /// </summary>
    public class Coin : MonoBehaviour
    {
        [HideInInspector] public int coinId;

        // Prevent duplicate sends
        private bool _pendingCollection;
        // Set when ACK is confirmed; prevents late ACKs from re-triggering
        private bool _collected;

        void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnCoinAck += HandleCoinAck;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnCoinAck -= HandleCoinAck;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected || _pendingCollection) return;

            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null || !player.isLocalPlayer) return;

            _pendingCollection = true;

            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                // Host routes directly through the arbitration logic
                GameManager.Instance.LocalCoinCollected(coinId, player.playerId);
            }
            else
            {
                // Client sends request; waits for COIN_ACK before destroying
                NetworkManager.Instance.Send(new CoinCollectedMessage
                {
                    coinId   = coinId,
                    playerId = player.playerId
                });
            }
        }

        private void HandleCoinAck(CoinAckMessage ack)
        {
            if (ack.coinId != coinId || _collected) return;
            _collected = true;
            Destroy(gameObject);
        }
    }
}
