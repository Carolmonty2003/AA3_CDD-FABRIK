using UnityEngine;

public class DataCorePickup : MonoBehaviour
{
    [Tooltip("Si true, en vez de desactivar, destruye el objeto.")]
    public bool destroyOnPickup = false;

    public void Pickup()
    {

        if (destroyOnPickup)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}
