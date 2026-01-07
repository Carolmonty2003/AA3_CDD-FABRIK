using System;
using UnityEngine;

public class SequenceButton : MonoBehaviour
{
    public bool IsPressed { get; private set; }

    public bool AcceptPress { get; set; }

    public event Action<SequenceButton> Pressed;

    public Vector3 GetPressWorldPosition() => transform.position;

    public void SimulatePress()
    {
        // Bloquea la pulsación si no está habilitado (AcceptPress) o si ya fue pulsado.
        if (!AcceptPress || IsPressed) return;

        // Marca como pulsado y dispara el evento para que el sistema de secuencia lo procese.
        IsPressed = true;
        Pressed?.Invoke(this);
    }

    public void ResetState()
    {
        // Resetea el estado para reutilizar el botón en otra ronda/secuencia.
        IsPressed = false;
        AcceptPress = false;
    }
}
