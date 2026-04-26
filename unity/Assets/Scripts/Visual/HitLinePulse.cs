using UnityEngine;

// Pulses the hit line's color between red and bright orange.
public class HitLinePulse : MonoBehaviour
{
    Material _mat;

    void Start()
    {
        // Use an instance material so we can animate it without affecting others
        _mat = GetComponent<Renderer>().material;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
        Color c = Color.Lerp(new Color(0.8f, 0f, 0f), new Color(1f, 0.6f, 0.1f), t);
        HighwayBuilder.ApplyColor(_mat, c);
    }
}
