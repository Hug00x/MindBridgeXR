using System;
using UnityEngine;

public class DiningTableZone : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    public event Action PlayerArrived;

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !IsPlayer(other))
            return;

        hasTriggered = true;
        PlayerArrived?.Invoke();
    }

    private void OnTriggerStay(Collider other)
    {
        if (hasTriggered || !IsPlayer(other))
            return;

        hasTriggered = true;
        PlayerArrived?.Invoke();
    }

    public void ResetZone()
    {
        hasTriggered = false;
    }

    private bool IsPlayer(Collider other)
    {
        return other.CompareTag(playerTag) ||
               (other.transform.root != null && other.transform.root.CompareTag(playerTag));
    }
}
