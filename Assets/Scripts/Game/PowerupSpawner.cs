using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CoinRush.Networking;

namespace CoinRush.Game
{
    public class PowerupSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject speedBoostPrefab;
        public GameObject stunPrefab;

        [Header("Spawn Settings")]
        public float spawnIntervalMin = 12f;
        public float spawnIntervalMax = 22f;
        public float areaMinX = -7f;
        public float areaMaxX =  7f;
        public float areaMinY = -1f;
        public float areaMaxY =  3f;

        private readonly List<Powerup> _activePowerups = new List<Powerup>();
        private int _nextId;

        void Start()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnPowerupSpawn += HandleRemotePowerupSpawn;

            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
                StartCoroutine(HostSpawnLoop());
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPowerupSpawn -= HandleRemotePowerupSpawn;
        }

        // HOST: spawn loop

        private IEnumerator HostSpawnLoop()
        {
            // Small initial delay so players can settle before the first powerup
            yield return new WaitForSeconds(8f);

            while (GameManager.Instance != null && GameManager.Instance.GameRunning)
            {
                float wait = Random.Range(spawnIntervalMin, spawnIntervalMax);
                yield return new WaitForSeconds(wait);
                if (GameManager.Instance == null || !GameManager.Instance.GameRunning) break;

                HostSpawnOne();
            }
        }

        private void HostSpawnOne()
        {
            string[] types = { "SPEED", "STUN" };
            string   chosen = types[Random.Range(0, types.Length)];
            float    x = Random.Range(areaMinX, areaMaxX);
            float    y = Random.Range(areaMinY, areaMaxY);
            int      id = _nextId++;

            var spawnMsg = new PowerupSpawnMessage
            {
                powerupId   = id,
                powerupType = chosen,
                x           = x,
                y           = y
            };

            NetworkManager.Instance.Send(spawnMsg);
            GameManager.Instance.InitPowerupArbitration(_nextId);
            SpawnLocal(spawnMsg);
        }

        private void HandleRemotePowerupSpawn(PowerupSpawnMessage msg)
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost) return;
            SpawnLocal(msg);
        }

        private void SpawnLocal(PowerupSpawnMessage msg)
        {
            GameObject prefab = msg.powerupType == "SPEED" ? speedBoostPrefab : stunPrefab;
            if (prefab == null) return;

            Vector3    pos = new Vector3(msg.x, msg.y, 0f);
            GameObject go  = Instantiate(prefab, pos, Quaternion.identity);
            Powerup    pu  = go.GetComponent<Powerup>();
            if (pu == null) return;

            pu.powerupId   = msg.powerupId;
            pu.powerupType = msg.powerupType;
            _activePowerups.Add(pu);
        }

        public void HostConfirmPowerup(int powerupId, int collectedBy)
        {
            Powerup found = _activePowerups.Find(p => p != null && p.powerupId == powerupId);
            if (found == null) return;

            var ack = new PowerupAckMessage
            {
                powerupId   = powerupId,
                collectedBy = collectedBy,
                powerupType = found.powerupType
            };
            _activePowerups.Remove(found);
            GameManager.Instance.PublishPowerupAck(ack);
        }
    }
}

