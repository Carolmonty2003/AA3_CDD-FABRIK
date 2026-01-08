using UnityEngine;

public class DataCoreCollector : MonoBehaviour
{
    [Header("IK (optional but recommended)")]
    public SmoothIKTargetProxy smoothProxy;

    DataCorePickup touchingCore;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && touchingCore != null)
        {
            touchingCore.Pickup();

            if (smoothProxy != null)
            {
                if (smoothProxy.desiredTarget == touchingCore.transform)
                {
                    smoothProxy.SetDesiredTarget(null);
                    smoothProxy.EnableIK(false);
                }
            }

            touchingCore = null;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var core = other.GetComponent<DataCorePickup>();
        if (core != null) touchingCore = core;
    }

    void OnTriggerExit(Collider other)
    {
        var core = other.GetComponent<DataCorePickup>();
        if (core != null && touchingCore == core) touchingCore = null;
    }
}
