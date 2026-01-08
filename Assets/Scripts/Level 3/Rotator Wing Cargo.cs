using UnityEngine;

public class RotatorWingCargo : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 2000f;
    private Transform rotator;

    void Start()
    {
        rotator = transform;
    }

    void Update()
    {
        rotator.Rotate(0f, 0f, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
