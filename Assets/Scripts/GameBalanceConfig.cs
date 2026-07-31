using System;
using UnityEngine;

public enum PowerUpId
{
    None = -1,
    Shield = 0,
    Magnet = 1,
    ScoreBoost = 2,
    TurboStart = 3
}

[Serializable]
public sealed class PowerUpBalance
{
    public string id;
    public string displayName;
    public string description;
    public int cost;
    public float duration;
    public float value;
}

[Serializable]
public sealed class GameplayBalance
{
    public float startSpeed = 10f;
    public float maxSpeed = 40f;
    public float speedIncreaseRate = 0.5f;
    public int coinScore = 10;
    public float magnetRadius = 7f;
}

[Serializable]
public sealed class TrackBalance
{
    public float obstacleChance = 0.7f;
    public float coinChance = 0.6f;
    public float turnChance = 0.24f;
}

[Serializable]
public sealed class AIBalance
{
    public float shadowLearningRate = 0.08f;
    public int minimumTrainingSamples = 24;
    public int minimumActiveSamples = 6;
    public float directorExploration = 0.35f;
}

[Serializable]
public sealed class GameBalanceData
{
    public GameplayBalance gameplay = new GameplayBalance();
    public TrackBalance track = new TrackBalance();
    public AIBalance ai = new AIBalance();
    public PowerUpBalance[] powerUps;
}

public static class GameBalanceConfig
{
    private static GameBalanceData _current;

    public static GameBalanceData Current
    {
        get
        {
            if (_current == null) _current = Load();
            return _current;
        }
    }

    public static PowerUpBalance GetPowerUp(PowerUpId id)
    {
        int index = (int)id;
        PowerUpBalance[] definitions = Current.powerUps;
        return definitions != null && index >= 0 && index < definitions.Length
            ? definitions[index]
            : null;
    }

    public static void ReloadForTests()
    {
        _current = null;
    }

    private static GameBalanceData Load()
    {
        TextAsset asset = Resources.Load<TextAsset>("Config/game-balance");
        GameBalanceData data = asset != null
            ? JsonUtility.FromJson<GameBalanceData>(asset.text)
            : null;
        if (data == null)
        {
            Debug.LogWarning("Game balance config is missing; using safe defaults.");
            data = new GameBalanceData();
        }
        data.gameplay = data.gameplay ?? new GameplayBalance();
        data.track = data.track ?? new TrackBalance();
        data.ai = data.ai ?? new AIBalance();
        data.powerUps = NormalizePowerUps(data.powerUps);
        return data;
    }

    private static PowerUpBalance[] NormalizePowerUps(PowerUpBalance[] source)
    {
        PowerUpBalance[] fallback =
        {
            new PowerUpBalance { id = "shield", displayName = "相位护盾", description = "抵消一次碰撞", cost = 60, value = 1f },
            new PowerUpBalance { id = "magnet", displayName = "磁轨吸附", description = "自动吸取附近金币", cost = 45, duration = 20f, value = 1f },
            new PowerUpBalance { id = "scoreBoost", displayName = "双倍协议", description = "积分翻倍", cost = 75, duration = 25f, value = 2f },
            new PowerUpBalance { id = "turboStart", displayName = "涡轮起步", description = "以更高速度起跑", cost = 50, duration = 12f, value = 7f }
        };
        if (source == null || source.Length != fallback.Length) return fallback;
        for (int i = 0; i < source.Length; i++)
            if (source[i] == null) source[i] = fallback[i];
        return source;
    }
}
