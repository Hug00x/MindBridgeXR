using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/*
 * Reproduz feedback sonoro quando um objeto XR é agarrado.
 * O script liga-se ao XRGrabInteractable do próprio objeto e toca um clip curto
 * no momento em que a seleção começa.
 */
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class GrabSound : MonoBehaviour
{
    // Clip e volume expostos para cada objeto ter feedback próprio.
    public AudioClip grabClip;
    [Range(0f, 1f)] public float volume = 1f;

    private AudioSource audioSource;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    // Cache dos componentes obrigatórios.
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }

    // Liga e desliga o evento XR conforme o objeto fica ativo.
    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrab);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
    }

    // Toca o som sem interromper outros sons emitidos pelo mesmo AudioSource.
    private void OnGrab(SelectEnterEventArgs args)
    {
        if (grabClip == null) return;
        audioSource.PlayOneShot(grabClip, volume);
    }
}
