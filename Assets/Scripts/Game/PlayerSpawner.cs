using UnityEngine;
using CoinRush.Networking;

namespace CoinRush.Game
{
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        public GameObject playerPrefab;

        [Header("Spawn Points")]
        public Transform spawnPoint1;
        public Transform spawnPoint2;

        [Header("References")]
        public CameraController cameraController;

        void Start()
        {
            if (GameManager.Instance == null) return;

            int localId  = GameManager.Instance.LocalPlayerId;
            int remoteId = localId == 1 ? 2 : 1;

            Transform localSpawn  = localId  == 1 ? spawnPoint1 : spawnPoint2;
            Transform remoteSpawn = remoteId == 1 ? spawnPoint1 : spawnPoint2;

            var localGo = Instantiate(playerPrefab, localSpawn.position, Quaternion.identity);
            var localCtrl = localGo.GetComponent<PlayerController>();
            localCtrl.playerId     = localId;
            localCtrl.isLocalPlayer = true;
            localGo.name = $"Player{localId}_Local";

            var remoteGo = Instantiate(playerPrefab, remoteSpawn.position, Quaternion.identity);
            var remoteCtrl = remoteGo.GetComponent<PlayerController>();
            remoteCtrl.playerId     = remoteId;
            remoteCtrl.isLocalPlayer = false;
            var remoteRb = remoteGo.GetComponent<Rigidbody2D>();
            if (remoteRb != null)
            {
                remoteRb.gravityScale = 0f;
                remoteRb.bodyType     = RigidbodyType2D.Kinematic;
            }
            remoteGo.name = $"Player{remoteId}_Remote";

            if (cameraController != null)
            {
                cameraController.targetA = localGo.transform;
                cameraController.targetB = remoteGo.transform;
            }
        }
    }
}

