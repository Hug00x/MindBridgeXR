using UnityEngine;

public class PersistentXRRig : MonoBehaviour
{
    private static PersistentXRRig instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}