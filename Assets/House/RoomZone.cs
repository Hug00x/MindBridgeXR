using UnityEngine;

public class RoomZone : MonoBehaviour
{
    public string roomName;
    public GameObject highlightObject;

    public void SetHighlight(bool state)
    {
        if (highlightObject != null)
            highlightObject.SetActive(state);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (TaskManager.Instance != null)
            TaskManager.Instance.PlayerEnteredRoom(this);
    }
}