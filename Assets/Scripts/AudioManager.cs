using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null || FindObjectOfType<AudioManager>() != null) return;
        new GameObject("Audio Manager").AddComponent<AudioManager>();
    }

    [Header("Audio Sources")]
    private AudioSource _sfxSource;
    private AudioSource _musicSource;

    [Header("SFX Clips (assign .wav/.mp3/.ogg, or leave null for procedural beeps)")]
    public AudioClip jumpClip;
    public AudioClip slideClip;
    public AudioClip coinClip;
    public AudioClip dodgeObstacleClip;
    public AudioClip deathClip;
    public AudioClip footstepClip;
    public AudioClip[] footstepClips;
    public AudioClip collisionClip;
    public AudioClip uiClickClip;
    public AudioClip uiConfirmClip;
    public AudioClip uiErrorClip;

    [Header("Music")]
    public AudioClip bgmClip;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Footsteps")]
    public float footstepInterval = 0.35f;
    private float _footstepTimer;
    private bool _isPlayingFootsteps;
    private int _footstepIndex;

    // Procedural audio cache
    private Dictionary<string, AudioClip> _procClips = new Dictionary<string, AudioClip>();
    private const int SampleRate = 22050;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EchoRunSaveSystem.EnsureInitialized();

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.spatialBlend = 0f;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;
        _musicSource.volume = musicVolume;
    }

    void Start()
    {
        LoadBundledAudio();
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);
        if (_musicSource != null) _musicSource.volume = musicVolume;

        AudioClip music = bgmClip != null ? bgmClip : GetProceduralClip("bgm");
        if (music == null) return;
        _musicSource.clip = music;
        _musicSource.Play();
    }

    void Update()
    {
        if (!_isPlayingFootsteps) return;

        _footstepTimer += Time.deltaTime;
        if (_footstepTimer >= footstepInterval)
        {
            _footstepTimer = 0f;
            AudioClip step = footstepClips != null && footstepClips.Length > 0
                ? footstepClips[_footstepIndex++ % footstepClips.Length]
                : footstepClip;
            PlaySFX(step, 0.28f, "footstep");
        }
    }

    public void StartFootsteps()
    {
        _isPlayingFootsteps = true;
        _footstepTimer = 0f;
    }

    public void StopFootsteps()
    {
        _isPlayingFootsteps = false;
    }

    public void PlayJump()  => PlaySFX(jumpClip, 1f, "jump");
    public void PlaySlide() => PlaySFX(slideClip, 0.6f, "slide");
    public void PlayCoin()  => PlaySFX(coinClip, 0.7f, "coin");
    public void PlayDodgeObstacle() => PlaySFX(dodgeObstacleClip, 0.6f, "dodge");
    public void PlayDeath() => PlaySFX(deathClip, 0.9f, "death");
    public void PlayCollision() => PlaySFX(collisionClip, 0.82f, "death");
    public void PlayUIClick() => PlaySFX(uiClickClip, 0.55f, "dodge");
    public void PlayUIConfirm() => PlaySFX(uiConfirmClip, 0.65f, "coin");
    public void PlayUIError() => PlaySFX(uiErrorClip, 0.65f, "death");

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (_musicSource != null) _musicSource.volume = musicVolume;
        EchoRunSaveSystem.SaveAudio(musicVolume, sfxVolume, false);
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        EchoRunSaveSystem.SaveAudio(musicVolume, sfxVolume, false);
    }

    void PlaySFX(AudioClip clip, float volumeScale = 1f, string procKey = null)
    {
        if (clip == null && !string.IsNullOrEmpty(procKey))
            clip = GetProceduralClip(procKey);

        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    private void LoadBundledAudio()
    {
        if (bgmClip == null) bgmClip = Resources.Load<AudioClip>("Audio/bgm_transit");
        if (coinClip == null) coinClip = Resources.Load<AudioClip>("Audio/coin");
        if (collisionClip == null) collisionClip = Resources.Load<AudioClip>("Audio/collision");
        if (uiClickClip == null) uiClickClip = Resources.Load<AudioClip>("Audio/ui_click");
        if (uiConfirmClip == null) uiConfirmClip = Resources.Load<AudioClip>("Audio/ui_confirm");
        if (uiErrorClip == null) uiErrorClip = Resources.Load<AudioClip>("Audio/ui_error");
        if (footstepClips == null || footstepClips.Length == 0)
        {
            AudioClip first = Resources.Load<AudioClip>("Audio/footstep_01");
            AudioClip second = Resources.Load<AudioClip>("Audio/footstep_02");
            if (first != null && second != null) footstepClips = new[] { first, second };
            else if (first != null) footstepClips = new[] { first };
        }
    }

    AudioClip GetProceduralClip(string key)
    {
        if (_procClips.TryGetValue(key, out AudioClip cached))
            return cached;

        AudioClip proc = key switch
        {
            "bgm"      => GenerateTransitLoop(),
            "footstep" => GenerateFootstep(),
            "jump"     => GenerateToneSweep(260f, 620f, 0.18f),
            "slide"    => GenerateNoiseBurst(0.18f, 0.18f),
            "coin"     => GenerateToneSweep(760f, 1320f, 0.13f),
            "dodge"    => GenerateToneSweep(520f, 240f, 0.14f),
            "death"    => GenerateToneSweep(360f, 52f, 0.48f),
            _           => GenerateNoiseBurst(0.06f, 0.12f),
        };
        _procClips[key] = proc;
        return proc;
    }

    AudioClip GenerateToneSweep(float freqStart, float freqEnd, float duration)
    {
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float freq = Mathf.Lerp(freqStart, freqEnd, t / duration);
            float normalized = Mathf.Clamp01(t / duration);
            float attack = Mathf.Clamp01(normalized / 0.08f);
            float envelope = attack * Mathf.Pow(1f - normalized, 1.6f);
            float phase = 2f * Mathf.PI * freq * t;
            data[i] = (Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.18f)
                      * envelope * 0.32f;
        }
        AudioClip clip = AudioClip.Create("proc_tone_" + duration, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateNoiseBurst(float duration, float level)
    {
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        var random = new System.Random(7319 + samples);
        float filtered = 0f;
        for (int i = 0; i < samples; i++)
        {
            float normalized = i / (float)Mathf.Max(1, samples - 1);
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered = Mathf.Lerp(filtered, white, 0.22f);
            float envelope = Mathf.Sin(Mathf.PI * normalized)
                             * Mathf.Pow(1f - normalized, 0.7f);
            data[i] = filtered * envelope * level;
        }
        AudioClip clip = AudioClip.Create("proc_noise_" + duration, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateFootstep()
    {
        const float duration = 0.11f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        var random = new System.Random(1907);
        float filtered = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float normalized = t / duration;
            float envelope = Mathf.Pow(1f - Mathf.Clamp01(normalized), 3f);
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered = Mathf.Lerp(filtered, white, 0.14f);
            float body = Mathf.Sin(2f * Mathf.PI * 82f * t) * 0.62f;
            data[i] = (body + filtered * 0.38f) * envelope * 0.34f;
        }
        AudioClip clip = AudioClip.Create("proc_footstep", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateTransitLoop()
    {
        const float duration = 16f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        int[] arpeggio = { 0, 7, 12, 7, 3, 10, 12, 15 };

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float beatPhase = Mathf.Repeat(t * 2f, 1f);
            float beatEnvelope = Mathf.Exp(-beatPhase * 11f);
            float pulse = Mathf.Sin(2f * Mathf.PI * 58f * t) * beatEnvelope;

            float pad = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.46f
                        + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.22f
                        + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.12f;

            float stepPosition = Mathf.Repeat(t * 2f, 1f);
            int step = Mathf.FloorToInt(t * 2f) % arpeggio.Length;
            float noteFrequency = 220f * Mathf.Pow(2f, arpeggio[step] / 12f);
            float noteEnvelope = Mathf.Sin(Mathf.PI * stepPosition);
            noteEnvelope *= noteEnvelope * Mathf.Pow(1f - stepPosition, 0.35f);
            float signal = Mathf.Sin(2f * Mathf.PI * noteFrequency * t)
                           + Mathf.Sin(2f * Mathf.PI * noteFrequency * 2f * t) * 0.12f;

            float longBreath = 0.72f + Mathf.Sin(2f * Mathf.PI * t / 8f) * 0.18f;
            data[i] = Mathf.Clamp((pad * 0.105f * longBreath)
                                  + (pulse * 0.055f)
                                  + (signal * noteEnvelope * 0.045f), -0.28f, 0.28f);
        }

        AudioClip clip = AudioClip.Create("proc_transit_loop", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        foreach (AudioClip clip in _procClips.Values)
            if (clip != null) Destroy(clip);
        _procClips.Clear();
        Instance = null;
    }
}
