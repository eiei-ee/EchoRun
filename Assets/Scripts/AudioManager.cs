using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

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

    [Header("Music")]
    public AudioClip bgmClip;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Footsteps")]
    public float footstepInterval = 0.35f;
    private float _footstepTimer;
    private bool _isPlayingFootsteps;

    // Procedural audio cache
    private Dictionary<string, AudioClip> _procClips = new Dictionary<string, AudioClip>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.volume = musicVolume;
    }

    void Start()
    {
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);
        if (_musicSource != null) _musicSource.volume = musicVolume;

        if (bgmClip != null)
        {
            _musicSource.clip = bgmClip;
            _musicSource.Play();
        }
    }

    void Update()
    {
        if (!_isPlayingFootsteps) return;

        _footstepTimer += Time.deltaTime;
        if (_footstepTimer >= footstepInterval)
        {
            _footstepTimer = 0f;
            PlaySFX(footstepClip, 0.15f);
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

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (_musicSource != null) _musicSource.volume = musicVolume;
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat("SfxVolume", sfxVolume);
    }

    void PlaySFX(AudioClip clip, float volumeScale = 1f, string procKey = null)
    {
        if (clip == null && !string.IsNullOrEmpty(procKey))
            clip = GetProceduralClip(procKey);

        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    AudioClip GetProceduralClip(string key)
    {
        if (_procClips.TryGetValue(key, out AudioClip cached))
            return cached;

        AudioClip proc = key switch
        {
            "jump"   => GenerateToneSweep(300f, 550f, 0.15f),
            "slide"  => GenerateNoise(0.12f),
            "coin"   => GenerateToneSweep(800f, 1200f, 0.1f),
            "dodge"  => GenerateToneSweep(400f, 200f, 0.1f),
            "death"  => GenerateToneSweep(400f, 60f, 0.4f),
            _        => GenerateNoise(0.05f),
        };
        _procClips[key] = proc;
        return proc;
    }

    AudioClip GenerateToneSweep(float freqStart, float freqEnd, float duration)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(freqStart, freqEnd, t / duration);
            float envelope = 1f - (t / duration); // linear fade
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.4f;
        }
        AudioClip clip = AudioClip.Create("proc_" + duration, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateNoise(float duration)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (t / duration);
            data[i] = (Random.value * 2f - 1f) * envelope * 0.25f;
        }
        AudioClip clip = AudioClip.Create("proc_noise_" + duration, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
