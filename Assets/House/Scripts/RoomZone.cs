using UnityEngine;

public class RoomZone : MonoBehaviour
{
    [Header("Identificação")]
    public string roomID;
    public string roomName;

    [Header("Visual")]
    public GameObject highlightObject;
    public GameObject visitedMarkerObject;

    public void SetHighlight(bool state)
    {
        if (highlightObject != null)
            highlightObject.SetActive(state);
    }

    public void SetVisitedMark(bool state)
    {
        if (visitedMarkerObject != null)
            visitedMarkerObject.SetActive(state);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (TaskManager.Instance != null)
            TaskManager.Instance.PlayerEnteredRoom(this);
    }
}