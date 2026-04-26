using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class InputHandler : MonoBehaviour
{
    static readonly Key[] GroupKeys = {
        Key.A, Key.S, Key.D,
        Key.J, Key.K, Key.L,
    };

    public event Action<int> OnKeyPressed;
    public event Action<int> OnKeyReleased;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        for (int g = 0; g < GroupKeys.Length; g++)
        {
            var key = kb[GroupKeys[g]];
            if (key.wasPressedThisFrame)  OnKeyPressed?.Invoke(g);
            if (key.wasReleasedThisFrame) OnKeyReleased?.Invoke(g);
        }
    }
}
