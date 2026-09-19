using System;
using UnityEngine;

namespace CoinRush.Networking
{
    [Serializable]
    public class BaseMessage
    {
        public string type;
    }

    [Serializable]
    public class HelloMessage : BaseMessage
    {
        public int playerId;
        public HelloMessage() { type = "HELLO"; }
    }

    [Serializable]
    public class StartMessage : BaseMessage
    {
        public int timerSeconds;
        public int coinCount;
        public float[] coinPositionsX;
        public float[] coinPositionsY;
        public int map;
        public StartMessage() { type = "START"; }
    }

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

    [Serializable]
    public class CoinCollectedMessage : BaseMessage
    {
        public int coinId;
        public int playerId;
        public CoinCollectedMessage() { type = "COIN_COLLECTED"; }
    }

    [Serializable]
    public class CoinAckMessage : BaseMessage
    {
        public int coinId;
        public int collectedBy;
        public CoinAckMessage() { type = "COIN_ACK"; }
    }

    [Serializable]
    public class ScoreUpdateMessage : BaseMessage
    {
        public int p1Score;
        public int p2Score;
        public ScoreUpdateMessage() { type = "SCORE_UPDATE"; }
    }

    [Serializable]
    public class GameOverMessage : BaseMessage
    {
        public int winner;   // 1, 2, or 0 for a tie
        public int p1Score;
        public int p2Score;
        public GameOverMessage() { type = "GAME_OVER"; }
    }

    [Serializable]
    public class PowerupSpawnMessage : BaseMessage
    {
        public int powerupId;
        public string powerupType;
        public float x;
        public float y;
        public PowerupSpawnMessage() { type = "POWERUP_SPAWN"; }
    }

    [Serializable]
    public class PowerupCollectedMessage : BaseMessage
    {
        public int powerupId;
        public int playerId;
        public PowerupCollectedMessage() { type = "POWERUP_COLLECTED"; }
    }

    [Serializable]
    public class PowerupAckMessage : BaseMessage
    {
        public int powerupId;
        public int collectedBy;
        public string powerupType;
        public PowerupAckMessage() { type = "POWERUP_ACK"; }
    }
}

