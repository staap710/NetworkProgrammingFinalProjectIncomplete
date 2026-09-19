using UnityEngine;
using CoinRush.Networking;

namespace CoinRush.Game
{
    public class Coin : MonoBehaviour
    {
        [HideInInspector] public int coinId;

        private bool _pendingCollection;
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
                GameManager.Instance.LocalCoinCollected(coinId, player.playerId);
            }
            else
            {
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

