using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CoinRush.Networking;

namespace CoinRush.Game
{
    /// <summary>
    /// HOST-only: periodically spawns power-ups and broadcasts their positions.
    /// CLIENT: receives POWERUP_SPAWN events from GameManager and instantiates
    ///         the prefab locally.
    ///
    /// Power-up types:
    ///   SPEED - grants the collector a 1.75x speed boost for 5 seconds.
    ///   STUN  - stuns the OPPONENT for 2 seconds.
    /// </summary>
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

        // Track live power-ups so HostConfirmPowerup can look up the type
        private readonly List<Powerup> _activePowerups = new List<Powerup>();
        private int _nextId;

        void Start()
        {
            if (GameManager.Instance == null) return;

            // Client: listen for POWERUP_SPAWN events
            GameManager.Instance.OnPowerupSpawn += HandleRemotePowerupSpawn;

            // Host: kick off the spawn coroutine
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

            // Broadcast to client first
            NetworkManager.Instance.Send(spawnMsg);

            // Tell GameManager to initialise the arbitration slot
            GameManager.Instance.InitPowerupArbitration(_nextId);

            // Instantiate locally
            SpawnLocal(spawnMsg);
        }

        // CLIENT: react to POWERUP_SPAWN from GameManager

        private void HandleRemotePowerupSpawn(PowerupSpawnMessage msg)
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost) return;
            SpawnLocal(msg);
        }

        // Shared instantiation

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

        /// <summary>
        /// Called by GameManager (host) once the collection race is won.
        /// Looks up the type, builds the POWERUP_ACK, and publishes it.
        /// </summary>
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
