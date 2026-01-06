using System;
using UnityEngine;

public class SequenceButton : MonoBehaviour
{
    [SerializeField] private Transform pressPoint;

    public bool IsPressed { get; private set; }
    public bool AcceptPress { get; set; }
    public event Action<SequenceButton> Pressed;

    public Vector3 GetPressWorldPosition() => pressPoint ? pressPoint.position : transform.position;

    public void SimulatePress()
    {
        if (!AcceptPress || IsPressed) return;
        IsPressed = true;
        Pressed?.Invoke(this);
    }

    public void ResetState()
    {
        IsPressed = false;
        AcceptPress = false;
    }
}
