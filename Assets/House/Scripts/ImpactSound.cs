using UnityEngine;

/*
 * Reproduz feedback sonoro quando um objeto físico colide com impacto suficiente.
 * O volume é calculado a partir da velocidade relativa da colisão e limitado por
 * um cooldown para evitar sons repetidos em contactos contínuos.
 */
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody))]
public class ImpactSound : MonoBehaviour
{
    // Parâmetros que controlam quando e com que intensidade o impacto é audível.
    public AudioClip impactClip;
    public float minImpactSpeed = 0.6f;
    public float maxImpactSpeed = 5f;
    public float cooldown = 0.1f;

    private AudioSource audioSource;
    private float lastImpactTime;

    // Cache do emissor de áudio local.
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    // Filtra colisões fracas ou demasiado frequentes antes de tocar o som.
    private void OnCollisionEnter(Collision collision)
    {
        if (impactClip == null) return;

        if (Time.time - lastImpactTime < cooldown)
            return;

        float speed = collision.relativeVelocity.magnitude;

        if (speed < minImpactSpeed)
            return;

        float volume = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed);
        volume = Mathf.Clamp01(volume);

        audioSource.PlayOneShot(impactClip, volume);
        lastImpactTime = Time.time;
    }
}
