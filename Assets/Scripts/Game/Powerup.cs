using UnityEngine;
using CoinRush.Networking;

namespace CoinRush.Game
{
    /// <summary>
    /// Attached to each power-up prefab.
    ///
    /// Works the same pattern as Coin:
    ///   - Local player touches it -> HOST routes directly, CLIENT sends over network.
    ///   - Destroys itself on POWERUP_ACK.
    /// </summary>
    public class Powerup : MonoBehaviour
    {
        [HideInInspector] public int    powerupId;
        [HideInInspector] public string powerupType; // "SPEED" or "STUN"

        private bool _collected;

        void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPowerupAck += HandlePowerupAck;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPowerupAck -= HandlePowerupAck;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected) return;

            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null || !player.isLocalPlayer) return;

            _collected = true;
            gameObject.SetActive(false); // optimistic hide

            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
                GameManager.Instance.LocalPowerupCollected(powerupId, player.playerId);
            else
                NetworkManager.Instance.Send(new PowerupCollectedMessage
                {
                    powerupId = powerupId,
                    playerId  = player.playerId
                });
        }

        private void HandlePowerupAck(PowerupAckMessage ack)
        {
            if (ack.powerupId != powerupId) return;
            Destroy(gameObject);
        }
    }
}
