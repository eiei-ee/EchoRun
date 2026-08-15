using System;
using UnityEngine;

[Serializable]
public sealed class EchoRunSaveData
{
    public int version = 7;
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
    public string playerStyleJson = "";
    public string lastEchoContractJson = "";
    public int[] powerUpInventory = new int[4];
    public int selectedPowerUp = -1;
    public long savedAtUtcTicks;
}

[Serializable]
public sealed class EchoRunSaveEnvelope
{
    public int schemaVersion = 1;
    public long generation;
    public string payload = "";
    public string checksum = "";
}

public static class EchoRunSaveSystem
{
    public const string SaveKey = "EchoRunSaveV1";
    public const string SaveSlotAKey = "EchoRunSaveV1.A";
    public const string SaveSlotBKey = "EchoRunSaveV1.B";
    public const string ActiveSaveSlotKey = "EchoRunSaveV1.ActiveSlot";
    public const string TelemetryKey = "EchoRunLastTelemetryV1";
    public const int CurrentVersion = 7;

    private const string ShadowProfileKey = "AIShadowProfileV1";
    private const int SaveEnvelopeVersion = 1;

    private static EchoRunSaveData _data;
    private static bool _initialized;
    private static int _activeSlot = -1;
    private static long _generation;

    public static bool LoadedExistingArchive { get; private set; }
    public static bool MigratedLegacyData { get; private set; }
    public static bool RecoveredFromBackup { get; private set; }

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
        LoadedExistingArchive = false;
        MigratedLegacyData = false;
        RecoveredFromBackup = false;
        _activeSlot = -1;
        _generation = 0;

        if (TryLoadBestSlot(out EchoRunSaveData slotData,
                out int slot, out long generation))
        {
            _data = slotData;
            _activeSlot = slot;
            _generation = generation;
            LoadedExistingArchive = true;
            Normalize();
            RestoreLegacyKeys();
            bool movedTelemetry = MoveTelemetryOutOfArchive();
            PlayerPrefs.SetInt(ActiveSaveSlotKey, _activeSlot);
            if (movedTelemetry) WriteArchive(true);
            else PlayerPrefs.Save();
            return;
        }

        if (TryLoadLegacyArchive(out EchoRunSaveData legacyData))
        {
            _data = legacyData;
            LoadedExistingArchive = true;
            Normalize();
            RestoreLegacyKeys();
            MoveTelemetryOutOfArchive();
            WriteArchive(true);
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
        return PlayerPrefs.GetString(
            TelemetryKey, _data.lastRunTelemetryJson ?? "");
    }

    public static void SaveLastRunTelemetry(string telemetryJson)
    {
        EnsureInitialized();
        _data.lastRunTelemetryJson = "";
        string normalized = telemetryJson ?? "";
        if (string.IsNullOrEmpty(normalized))
            PlayerPrefs.DeleteKey(TelemetryKey);
        else
            PlayerPrefs.SetString(TelemetryKey, normalized);
        PlayerPrefs.Save();
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

    public static string GetPlayerStyleJson()
    {
        EnsureInitialized();
        return _data.playerStyleJson ?? "";
    }

    public static void SavePlayerStyle(string profileJson)
    {
        EnsureInitialized();
        _data.playerStyleJson = profileJson ?? "";
        WriteArchive(true);
    }

    public static string GetLastEchoContractJson()
    {
        EnsureInitialized();
        return _data.lastEchoContractJson ?? "";
    }

    public static void SaveLastEchoContract(string contractJson)
    {
        EnsureInitialized();
        _data.lastEchoContractJson = contractJson ?? "";
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
        _data.playerStyleJson = "";
        _data.lastEchoContractJson = "";
        PlayerPrefs.DeleteKey(ShadowProfileKey);
        PlayerPrefs.DeleteKey(TelemetryKey);
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
        _data.playerStyleJson = _data.playerStyleJson ?? "";
        _data.lastEchoContractJson = _data.lastEchoContractJson ?? "";
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

    private static bool TryLoadBestSlot(out EchoRunSaveData data,
        out int slot, out long generation)
    {
        bool validA = TryReadSlot(SaveSlotAKey,
            out EchoRunSaveData dataA, out long generationA);
        bool validB = TryReadSlot(SaveSlotBKey,
            out EchoRunSaveData dataB, out long generationB);
        int preferred = PlayerPrefs.GetInt(ActiveSaveSlotKey, -1);

        if (!validA && !validB)
        {
            data = null;
            slot = -1;
            generation = 0;
            return false;
        }

        if (validA && validB)
        {
            if (generationA == generationB)
                slot = preferred == 1 ? 1 : 0;
            else
                slot = generationA > generationB ? 0 : 1;
        }
        else
        {
            slot = validA ? 0 : 1;
        }

        data = slot == 0 ? dataA : dataB;
        generation = slot == 0 ? generationA : generationB;
        RecoveredFromBackup = preferred >= 0 && preferred != slot;
        return true;
    }

    private static bool TryReadSlot(string key, out EchoRunSaveData data,
        out long generation)
    {
        data = null;
        generation = 0;
        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return false;

        try
        {
            EchoRunSaveEnvelope envelope =
                JsonUtility.FromJson<EchoRunSaveEnvelope>(json);
            if (envelope == null
                || envelope.schemaVersion != SaveEnvelopeVersion
                || envelope.generation <= 0
                || string.IsNullOrEmpty(envelope.payload)
                || !string.Equals(envelope.checksum,
                    StableHash.ComputeHex(envelope.payload),
                    StringComparison.Ordinal))
                return false;

            data = JsonUtility.FromJson<EchoRunSaveData>(envelope.payload);
            if (data == null) return false;
            generation = envelope.generation;
            return true;
        }
        catch (Exception)
        {
            data = null;
            generation = 0;
            return false;
        }
    }

    private static bool TryLoadLegacyArchive(out EchoRunSaveData data)
    {
        data = null;
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) return false;
        try
        {
            data = JsonUtility.FromJson<EchoRunSaveData>(json);
            return data != null;
        }
        catch (Exception)
        {
            data = null;
            return false;
        }
    }

    private static bool MoveTelemetryOutOfArchive()
    {
        string inlineTelemetry = _data.lastRunTelemetryJson ?? "";
        if (string.IsNullOrEmpty(inlineTelemetry)) return false;
        if (!PlayerPrefs.HasKey(TelemetryKey))
            PlayerPrefs.SetString(TelemetryKey, inlineTelemetry);
        _data.lastRunTelemetryJson = "";
        return true;
    }

    private static void WriteArchive(bool flush)
    {
        Normalize();
        _data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
        string payload = JsonUtility.ToJson(_data);
        long nextGeneration = _generation + 1;
        var envelope = new EchoRunSaveEnvelope
        {
            schemaVersion = SaveEnvelopeVersion,
            generation = nextGeneration,
            payload = payload,
            checksum = StableHash.ComputeHex(payload)
        };
        int targetSlot = _activeSlot == 0 ? 1 : 0;
        string targetKey = targetSlot == 0 ? SaveSlotAKey : SaveSlotBKey;

        try
        {
            PlayerPrefs.SetString(targetKey, JsonUtility.ToJson(envelope));
            if (flush) PlayerPrefs.Save();
            if (!TryReadSlot(targetKey, out _, out long writtenGeneration)
                || writtenGeneration != nextGeneration)
                return;

            _activeSlot = targetSlot;
            _generation = nextGeneration;
            PlayerPrefs.SetInt(ActiveSaveSlotKey, _activeSlot);
            PlayerPrefs.DeleteKey(SaveKey);
            if (flush) PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogError("EchoRun archive could not be committed: "
                           + exception.Message);
        }
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
