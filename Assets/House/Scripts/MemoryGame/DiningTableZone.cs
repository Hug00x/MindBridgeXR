using System;
using UnityEngine;

/*
 * Zona que deteta a chegada do jogador à mesa da sala de jantar.
 * Emite o evento apenas uma vez por fase para iniciar o minijogo de memória
 * no momento em que o jogador se aproxima o suficiente.
 */
public class DiningTableZone : MonoBehaviour
{
    // Tag usada para reconhecer o jogador ou a raiz do rig.
    [SerializeField] private string playerTag = "Player";

    public event Action PlayerArrived;

    private bool hasTriggered;

    // Deteta entrada direta do jogador na zona.
    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !IsPlayer(other))
            return;

        hasTriggered = true;
        PlayerArrived?.Invoke();
    }

    // Cobre o caso em que o jogador já está dentro da zona quando ela é ativada.
    private void OnTriggerStay(Collider other)
    {
        if (hasTriggered || !IsPlayer(other))
            return;

        hasTriggered = true;
        PlayerArrived?.Invoke();
    }

    // Permite reutilizar a zona quando a fase é reiniciada.
    public void ResetZone()
    {
        hasTriggered = false;
    }

    // Reconhece colliders do próprio jogador ou filhos do rig.
    private bool IsPlayer(Collider other)
    {
        return other.CompareTag(playerTag) ||
               (other.transform.root != null && other.transform.root.CompareTag(playerTag));
    }
}
