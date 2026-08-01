using System;
using UnityEngine;

[Serializable]
public sealed class EchoRunSaveData
{
    public int version = 5;
    public int highScore;
    public int totalCoins;
    public int targetFrameRate = 60;
    public float musicVolume = 0.5f;
    public float sfxVolume = 1f;
    public int characterPreset;
    public string shadowProfileJson = "";
    public float[] directorWeights;
    public int directorModelUpdateCount;
    public string directorPolicyJson = "";
    public int runSequence;
    public string lastRunTelemetryJson = "";
    public string skillProfileJson = "";
    public int[] powerUpInventory = new int[4];
    public int selectedPowerUp = -1;
    public long savedAtUtcTicks;
}

public static class EchoRunSaveSystem
{
    public const string SaveKey = "EchoRunSaveV1";
    public const int CurrentVersion = 5;

    private const string ShadowProfileKey = "AIShadowProfileV1";

    private static EchoRunSaveData _data;
    private static bool _initialized;

    public static bool LoadedExistingArchive { get; private set; }
    public static bool MigratedLegacyData { get; private set; }

    public static int DirectorModelUpdateCount
    {
        get
        {
            EnsureInitialized();
            return _data.directorModelUpdateCount;
        }
    }

    public static int TotalCoins
    {
        get
        {
            EnsureInitialized();
            return _data.totalCoins;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInitialized();

        if (GameObject.Find("EchoRun Save System") != null) return;
        GameObject host = new GameObject("EchoRun Save System");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<EchoRunSaveLifecycle>();
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        string archiveJson = PlayerPrefs.GetString(SaveKey, "");
        if (!string.IsNullOrEmpty(archiveJson))
        {
            try
            {
                _data = JsonUtility.FromJson<EchoRunSaveData>(archiveJson);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("EchoRun archive could not be loaded: " + exception.Message);
            }
        }

        if (_data != null)
        {
            LoadedExistingArchive = true;
            Normalize();
            RestoreLegacyKeys();
            return;
        }

        MigratedLegacyData = HasLegacyData();
        _data = new EchoRunSaveData();
        CaptureLegacyKeys();
        WriteArchive(true);
    }

    public static string GetShadowProfileJson()
    {
        EnsureInitialized();
        return _data.shadowProfileJson ?? "";
    }

    public static void SaveShadowProfile(string profileJson)
    {
        EnsureInitialized();
        _data.shadowProfileJson = profileJson ?? "";
        PlayerPrefs.SetString(ShadowProfileKey, _data.shadowProfileJson);
        CaptureLegacyKeys();
        WriteArchive(true);
    }

    public static float[] GetDirectorWeights()
    {
        EnsureInitialized();
        return Clone(_data.directorWeights);
    }

    public static string GetDirectorPolicyJson()
    {
        EnsureInitialized();
        return _data.directorPolicyJson ?? "";
    }

    public static void SaveDirector(float[] weights, int modelUpdateCount,
        string policyJson = null)
    {
        EnsureInitialized();
        _data.directorWeights = Clone(weights);
        _data.directorModelUpdateCount = Mathf.Max(0, modelUpdateCount);
        if (policyJson != null)
            _data.directorPolicyJson = policyJson;
        CaptureLegacyKeys();
        WriteArchive(true);
    }

    public static int ReserveRunSequence()
    {
        EnsureInitialized();
        _data.runSequence = Mathf.Max(0, _data.runSequence) + 1;
        WriteArchive(true);
        return _data.runSequence;
    }

    public static string GetLastRunTelemetryJson()
    {
        EnsureInitialized();
        return _data.lastRunTelemetryJson ?? "";
    }

    public static void SaveLastRunTelemetry(string telemetryJson)
    {
        EnsureInitialized();
        _data.lastRunTelemetryJson = telemetryJson ?? "";
        WriteArchive(true);
    }

    public static string GetSkillProfileJson()
    {
        EnsureInitialized();
        return _data.skillProfileJson ?? "";
    }

    public static void SaveSkillProfile(string profileJson)
    {
        EnsureInitialized();
        _data.skillProfileJson = profileJson ?? "";
        WriteArchive(true);
    }

    public static int[] GetPowerUpInventory()
    {
        EnsureInitialized();
        return CloneInventory(_data.powerUpInventory);
    }

    public static int GetPowerUpCount(PowerUpId id)
    {
        EnsureInitialized();
        int index = (int)id;
        return index >= 0 && index < _data.powerUpInventory.Length
            ? _data.powerUpInventory[index]
            : 0;
    }

    public static PowerUpId GetSelectedPowerUp()
    {
        EnsureInitialized();
        return Enum.IsDefined(typeof(PowerUpId), _data.selectedPowerUp)
            ? (PowerUpId)_data.selectedPowerUp
            : PowerUpId.None;
    }

    public static bool TryPurchasePowerUp(PowerUpId id, int cost)
    {
        EnsureInitialized();
        int index = (int)id;
        int normalizedCost = Mathf.Max(0, cost);
        if (index < 0 || index >= _data.powerUpInventory.Length
            || _data.totalCoins < normalizedCost)
            return false;

        _data.totalCoins -= normalizedCost;
        _data.powerUpInventory[index]++;
        PlayerPrefs.SetInt("TotalCoins", _data.totalCoins);
        WriteArchive(true);
        return true;
    }

    public static bool SelectPowerUp(PowerUpId id)
    {
        EnsureInitialized();
        int index = (int)id;
        if (index < 0 || index >= _data.powerUpInventory.Length
            || _data.powerUpInventory[index] <= 0)
            return false;
        _data.selectedPowerUp = index;
        WriteArchive(true);
        return true;
    }

    public static bool ConsumePowerUp(PowerUpId id)
    {
        EnsureInitialized();
        int index = (int)id;
        if (index < 0 || index >= _data.powerUpInventory.Length
            || _data.powerUpInventory[index] <= 0)
            return false;
        _data.powerUpInventory[index]--;
        _data.selectedPowerUp = -1;
        WriteArchive(true);
        return true;
    }

    public static void ResetAITraining()
    {
        EnsureInitialized();
        _data.shadowProfileJson = "";
        _data.directorWeights = null;
        _data.directorModelUpdateCount = 0;
        _data.directorPolicyJson = "";
        _data.lastRunTelemetryJson = "";
        _data.skillProfileJson = "";
        PlayerPrefs.DeleteKey(ShadowProfileKey);
        WriteArchive(true);
    }

    public static void SaveProgress(int highScore, int totalCoins)
    {
        EnsureInitialized();
        _data.highScore = Mathf.Max(0, highScore);
        _data.totalCoins = Mathf.Max(0, totalCoins);
        PlayerPrefs.SetInt("HighScore", _data.highScore);
        PlayerPrefs.SetInt("TotalCoins", _data.totalCoins);
        CaptureLegacyKeys();
        WriteArchive(true);
    }

    public static void SaveFrameRate(int targetFrameRate)
    {
        EnsureInitialized();
        _data.targetFrameRate = Mathf.Max(1, targetFrameRate);
        PlayerPrefs.SetInt("TargetFrameRate", _data.targetFrameRate);
        WriteArchive(true);
    }

    public static void SaveAudio(float musicVolume, float sfxVolume, bool flush)
    {
        EnsureInitialized();
        _data.musicVolume = Mathf.Clamp01(musicVolume);
        _data.sfxVolume = Mathf.Clamp01(sfxVolume);
        PlayerPrefs.SetFloat("MusicVolume", _data.musicVolume);
        PlayerPrefs.SetFloat("SfxVolume", _data.sfxVolume);
        WriteArchive(flush);
    }

    public static void SaveCharacterPreset(int preset)
    {
        EnsureInitialized();
        _data.characterPreset = Mathf.Max(0, preset);
        PlayerPrefs.SetInt("CharacterPreset", _data.characterPreset);
        WriteArchive(true);
    }

    public static void SaveLegacyState()
    {
        EnsureInitialized();
        CaptureLegacyKeys();
        WriteArchive(true);
    }

    private static void Normalize()
    {
        _data.version = CurrentVersion;
        _data.highScore = Mathf.Max(0, _data.highScore);
        _data.totalCoins = Mathf.Max(0, _data.totalCoins);
        _data.targetFrameRate = _data.targetFrameRate > 0
            ? _data.targetFrameRate
            : 60;
        _data.musicVolume = Mathf.Clamp01(_data.musicVolume);
        _data.sfxVolume = Mathf.Clamp01(_data.sfxVolume);
        _data.characterPreset = Mathf.Max(0, _data.characterPreset);
        _data.shadowProfileJson = _data.shadowProfileJson ?? "";
        _data.directorWeights = Clone(_data.directorWeights);
        _data.directorModelUpdateCount = Mathf.Max(0, _data.directorModelUpdateCount);
        _data.directorPolicyJson = _data.directorPolicyJson ?? "";
        _data.runSequence = Mathf.Max(0, _data.runSequence);
        _data.lastRunTelemetryJson = _data.lastRunTelemetryJson ?? "";
        _data.skillProfileJson = _data.skillProfileJson ?? "";
        _data.powerUpInventory = CloneInventory(_data.powerUpInventory);
        _data.selectedPowerUp = _data.selectedPowerUp >= 0
                                && _data.selectedPowerUp < _data.powerUpInventory.Length
                                && _data.powerUpInventory[_data.selectedPowerUp] > 0
            ? _data.selectedPowerUp
            : -1;
    }

    private static bool HasLegacyData()
    {
        return PlayerPrefs.HasKey("HighScore")
               || PlayerPrefs.HasKey("TotalCoins")
               || PlayerPrefs.HasKey(ShadowProfileKey)
               || PlayerPrefs.HasKey("TargetFrameRate")
               || PlayerPrefs.HasKey("MusicVolume")
               || PlayerPrefs.HasKey("SfxVolume")
               || PlayerPrefs.HasKey("CharacterPreset");
    }

    private static void CaptureLegacyKeys()
    {
        _data.version = CurrentVersion;
        _data.highScore = Mathf.Max(0,
            PlayerPrefs.GetInt("HighScore", _data.highScore));
        _data.totalCoins = Mathf.Max(0,
            PlayerPrefs.GetInt("TotalCoins", _data.totalCoins));
        _data.targetFrameRate = Mathf.Max(1,
            PlayerPrefs.GetInt("TargetFrameRate", _data.targetFrameRate));
        _data.musicVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat("MusicVolume", _data.musicVolume));
        _data.sfxVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat("SfxVolume", _data.sfxVolume));
        _data.characterPreset = Mathf.Max(0,
            PlayerPrefs.GetInt("CharacterPreset", _data.characterPreset));
        _data.shadowProfileJson = PlayerPrefs.GetString(
            ShadowProfileKey, _data.shadowProfileJson ?? "");
    }

    private static void RestoreLegacyKeys()
    {
        PlayerPrefs.SetInt("HighScore", _data.highScore);
        PlayerPrefs.SetInt("TotalCoins", _data.totalCoins);
        PlayerPrefs.SetInt("TargetFrameRate", _data.targetFrameRate);
        PlayerPrefs.SetFloat("MusicVolume", _data.musicVolume);
        PlayerPrefs.SetFloat("SfxVolume", _data.sfxVolume);
        PlayerPrefs.SetInt("CharacterPreset", _data.characterPreset);
        if (!string.IsNullOrEmpty(_data.shadowProfileJson))
            PlayerPrefs.SetString(ShadowProfileKey, _data.shadowProfileJson);
        else
            PlayerPrefs.DeleteKey(ShadowProfileKey);
    }

    private static void WriteArchive(bool flush)
    {
        Normalize();
        _data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_data));
        if (flush) PlayerPrefs.Save();
    }

    private static float[] Clone(float[] values)
    {
        return values == null ? null : (float[])values.Clone();
    }

    private static int[] CloneInventory(int[] values)
    {
        int[] result = new int[4];
        if (values != null)
            Array.Copy(values, result, Mathf.Min(values.Length, result.Length));
        for (int i = 0; i < result.Length; i++)
            result[i] = Mathf.Max(0, result[i]);
        return result;
    }
}

public sealed class EchoRunSaveLifecycle : MonoBehaviour
{
    void OnApplicationPause(bool paused)
    {
        if (paused) EchoRunSaveSystem.SaveLegacyState();
    }

    void OnApplicationQuit()
    {
        EchoRunSaveSystem.SaveLegacyState();
    }
}
