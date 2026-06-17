using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicManager : MonoBehaviour
{
    private static BackgroundMusicManager instance;

    [Header("Music")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.08f;
    [SerializeField] private bool playOnStart = true;

    private AudioSource audioSource;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
            ConfigureAudioSource();
    }

    public void Play()
    {
        if (musicClip == null)
            return;

        if (audioSource.clip != musicClip)
            audioSource.clip = musicClip;

        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    public void Stop()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);

        if (audioSource != null)
            audioSource.volume = volume;
    }

    private void ConfigureAudioSource()
    {
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
        audioSource.clip = musicClip;
    }
}
