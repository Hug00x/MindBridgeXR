using UnityEngine;

/*
 * Mantém uma música ambiente única durante toda a experiência.
 * Este gestor configura o AudioSource como som 2D em loop e evita duplicados
 * quando novas cenas são carregadas.
 */
[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicManager : MonoBehaviour
{
    private static BackgroundMusicManager instance;

    // Configuração editável da faixa usada como ambiente global.
    [Header("Music")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.08f;
    [SerializeField] private bool playOnStart = true;

    private AudioSource audioSource;

    // Garante persistência entre cenas e prepara o AudioSource base.
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

    // Inicia automaticamente a música quando a cena arranca, se configurado.
    private void Start()
    {
        if (playOnStart)
            Play();
    }

    // Mantém valores coerentes no Inspector durante ajustes em modo editor.
    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
            ConfigureAudioSource();
    }

    // API simples usada por outros objetos para controlar a música global.
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

    // Aplica sempre o mesmo perfil: loop, volume controlado e áudio não espacial.
    private void ConfigureAudioSource()
    {
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
        audioSource.clip = musicClip;
    }
}
