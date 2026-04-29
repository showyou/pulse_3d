using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class InputHandler : MonoBehaviour
{
    static readonly Key[] GroupKeys = {
        Key.A, Key.S, Key.D,
        Key.J, Key.K, Key.L,
    };

    public event Action<int> OnKeyPressed;
    public event Action<int> OnKeyReleased;

    // RhythmGameManager が設定するタッチゾーン取得デリゲート
    public Func<int, Rect> GetGroupRect;

    readonly Dictionary<int, int> _touchToGroup = new Dictionary<int, int>();

    void OnEnable()  => EnhancedTouchSupport.Enable();
    void OnDisable() => EnhancedTouchSupport.Disable();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            for (int g = 0; g < GroupKeys.Length; g++)
            {
                var key = kb[GroupKeys[g]];
                if (key.wasPressedThisFrame)  OnKeyPressed?.Invoke(g);
                if (key.wasReleasedThisFrame) OnKeyReleased?.Invoke(g);
            }
        }

        if (GetGroupRect == null) return;

        foreach (var touch in Touch.activeTouches)
        {
            // InputSystem の screenPosition は左下原点、OnGUI は左上原点
            var guiPos = new Vector2(
                touch.screenPosition.x,
                Screen.height - touch.screenPosition.y);

            if (touch.phase == TouchPhase.Began)
            {
                int g = GroupFromPos(guiPos);
                if (g >= 0 && !_touchToGroup.ContainsKey(touch.touchId))
                {
                    _touchToGroup[touch.touchId] = g;
                    OnKeyPressed?.Invoke(g);
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (_touchToGroup.TryGetValue(touch.touchId, out int g))
                {
                    _touchToGroup.Remove(touch.touchId);
                    OnKeyReleased?.Invoke(g);
                }
            }
        }
    }

    int GroupFromPos(Vector2 guiPos)
    {
        for (int g = 0; g < 6; g++)
            if (GetGroupRect(g).Contains(guiPos)) return g;
        return -1;
    }
}
