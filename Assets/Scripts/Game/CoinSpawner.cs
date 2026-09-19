using UnityEngine;
using CoinRush.Networking;

namespace CoinRush.Game
{
    /// <summary>
    /// Placed in GameScene. Reads PendingStartMessage from GameManager and
    /// instantiates a Coin prefab at each position so both machines start with
    /// an identical set of coins (host generated the positions; client received them).
    /// </summary>
    public class CoinSpawner : MonoBehaviour
    {
        public GameObject coinPrefab;

        void Start()
        {
            if (GameManager.Instance == null || coinPrefab == null) return;

            StartMessage msg = GameManager.Instance.PendingStartMessage;
            if (msg == null)
            {
                Debug.LogWarning("[CoinSpawner] No PendingStartMessage found.");
                return;
            }

            for (int i = 0; i < msg.coinCount; i++)
            {
                Vector3 pos = new Vector3(msg.coinPositionsX[i], msg.coinPositionsY[i], 0f);
                GameObject go = Instantiate(coinPrefab, pos, Quaternion.identity);
                Coin coin = go.GetComponent<Coin>();
                if (coin != null) coin.coinId = i;
            }
        }
    }
}
