using UnityEngine;
using CoinRush.Networking;

namespace CoinRush.Game
{
    /// <summary>
    /// Placed in GameScene. Instantiates both the local and remote player prefabs
    /// at their correct spawn points and wires them up to the camera controller.
    /// Reads LocalPlayerId from GameManager to decide which player is "us".
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        public GameObject playerPrefab;

        [Header("Spawn Points")]
        public Transform spawnPoint1; // Player 1 (host)
        public Transform spawnPoint2; // Player 2 (client)

        [Header("References")]
        public CameraController cameraController;

        void Start()
        {
            if (GameManager.Instance == null) return;

            int localId  = GameManager.Instance.LocalPlayerId;
            int remoteId = localId == 1 ? 2 : 1;

            Transform localSpawn  = localId  == 1 ? spawnPoint1 : spawnPoint2;
            Transform remoteSpawn = remoteId == 1 ? spawnPoint1 : spawnPoint2;

            // Local player
            var localGo = Instantiate(playerPrefab, localSpawn.position, Quaternion.identity);
            var localCtrl = localGo.GetComponent<PlayerController>();
            localCtrl.playerId     = localId;
            localCtrl.isLocalPlayer = true;
            localGo.name = $"Player{localId}_Local";

            // Remote player (no physics input; position is interpolated)
            var remoteGo = Instantiate(playerPrefab, remoteSpawn.position, Quaternion.identity);
            var remoteCtrl = remoteGo.GetComponent<PlayerController>();
            remoteCtrl.playerId     = remoteId;
            remoteCtrl.isLocalPlayer = false;
            // Disable the remote player's Rigidbody2D gravity so it doesn't fall
            // (its position is fully driven by incoming INPUT messages)
            var remoteRb = remoteGo.GetComponent<Rigidbody2D>();
            if (remoteRb != null)
            {
                remoteRb.gravityScale = 0f;
                remoteRb.bodyType     = RigidbodyType2D.Kinematic;
            }
            remoteGo.name = $"Player{remoteId}_Remote";

            // Hook up the camera
            if (cameraController != null)
            {
                cameraController.targetA = localGo.transform;
                cameraController.targetB = remoteGo.transform;
            }
        }
    }
}
