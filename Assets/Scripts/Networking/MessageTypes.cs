using System;
using UnityEngine;

namespace CoinRush.Networking
{
    // ─────────────────────────────────────────────────────────────────────────
    // Base class every message inherits from.
    // The "type" field is the discriminator used in switch statements.
    // ─────────────────────────────────────────────────────────────────────────
    [Serializable]
    public class BaseMessage
    {
        public string type;
    }

    // ── Handshake ─────────────────────────────────────────────────────────────

    /// <summary>Client → Host immediately after TCP connection.</summary>
    [Serializable]
    public class HelloMessage : BaseMessage
    {
        public int playerId; // always 2 for the joining client
        public HelloMessage() { type = "HELLO"; }
    }

    /// <summary>
    /// Host → Client: authoritative initial state.
    /// Contains all coin spawn positions so both machines start identically.
    /// </summary>
    [Serializable]
    public class StartMessage : BaseMessage
    {
        public int timerSeconds;
        public int coinCount;
        public float[] coinPositionsX;
        public float[] coinPositionsY;
        public int map; // reserved for future multi-map support
        public StartMessage() { type = "START"; }
    }

    // ── Gameplay ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Both → Both at ~20 Hz: current position + velocity for dead-reckoning.
    /// seq is a monotonically increasing counter to discard stale packets.
    /// </summary>
    [Serializable]
    public class InputMessage : BaseMessage
    {
        public int playerId;
        public float x;
        public float y;
        public float velX;
        public float velY;
        public bool facingLeft;
        public int seq;
        public InputMessage() { type = "INPUT"; }
    }

    // ── Coins ─────────────────────────────────────────────────────────────────

    /// <summary>Local player → Host: "I touched coin #coinId."</summary>
    [Serializable]
    public class CoinCollectedMessage : BaseMessage
    {
        public int coinId;
        public int playerId;
        public CoinCollectedMessage() { type = "COIN_COLLECTED"; }
    }

    /// <summary>
    /// Host → Both: authoritative confirmation. collectedBy is the winner of any
    /// simultaneous-pickup race. The coin disappears on all machines on receipt.
    /// </summary>
    [Serializable]
    public class CoinAckMessage : BaseMessage
    {
        public int coinId;
        public int collectedBy; // playerId of the scorer
        public CoinAckMessage() { type = "COIN_ACK"; }
    }

    // ── Score / Timer ─────────────────────────────────────────────────────────

    /// <summary>Host → Client once per second to keep the client score display in sync.</summary>
    [Serializable]
    public class ScoreUpdateMessage : BaseMessage
    {
        public int p1Score;
        public int p2Score;
        public ScoreUpdateMessage() { type = "SCORE_UPDATE"; }
    }

    /// <summary>Host → Both when the countdown reaches zero.</summary>
    [Serializable]
    public class GameOverMessage : BaseMessage
    {
        public int winner;   // 1 or 2, or 0 for a tie
        public int p1Score;
        public int p2Score;
        public GameOverMessage() { type = "GAME_OVER"; }
    }

    // ── Power-ups ─────────────────────────────────────────────────────────────

    /// <summary>Host → Both: a new power-up has appeared at (x, y).</summary>
    [Serializable]
    public class PowerupSpawnMessage : BaseMessage
    {
        public int powerupId;
        public string powerupType; // "SPEED" or "STUN"
        public float x;
        public float y;
        public PowerupSpawnMessage() { type = "POWERUP_SPAWN"; }
    }

    /// <summary>Local player → Host: "I touched powerup #powerupId."</summary>
    [Serializable]
    public class PowerupCollectedMessage : BaseMessage
    {
        public int powerupId;
        public int playerId;
        public PowerupCollectedMessage() { type = "POWERUP_COLLECTED"; }
    }

    /// <summary>Host → Both: authoritative powerup confirmation including effect type.</summary>
    [Serializable]
    public class PowerupAckMessage : BaseMessage
    {
        public int powerupId;
        public int collectedBy;
        public string powerupType;
        public PowerupAckMessage() { type = "POWERUP_ACK"; }
    }
}
