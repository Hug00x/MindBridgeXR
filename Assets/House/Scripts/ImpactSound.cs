using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody))]
public class ImpactSound : MonoBehaviour
{
    public AudioClip impactClip;
    public float minImpactSpeed = 0.6f;
    public float maxImpactSpeed = 5f;
    public float cooldown = 0.1f;

    private AudioSource audioSource;
    private float lastImpactTime;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

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