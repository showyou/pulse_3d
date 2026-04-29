using System;
using UnityEngine;

public class HitEffectController : MonoBehaviour
{
    float  _t;
    Material _mat;
    Color  _color;
    Action<HitEffectController> _returnToPool;

    public void Play(Vector3 pos, Color color, Action<HitEffectController> returnToPool)
    {
        transform.position   = pos;
        transform.localScale = new Vector3(1.0f, 0.08f, 1.0f);
        _color       = color;
        _t           = 0f;
        _returnToPool = returnToPool;
        gameObject.SetActive(true);

        if (_mat == null)
        {
            var shader = Shader.Find("Sprites/Default")
                      ?? Shader.Find("Unlit/Transparent")
                      ?? Shader.Find("Standard");
            _mat = new Material(shader);
            GetComponent<Renderer>().material = _mat;
        }
        _color.a = 1f;
        _mat.color = _color;
    }

    void Update()
    {
        _t += Time.deltaTime / 0.20f;
        if (_t >= 1f)
        {
            gameObject.SetActive(false);
            _returnToPool?.Invoke(this);
            return;
        }
        float s = Mathf.Lerp(1.0f, 3.2f, _t);
        transform.localScale = new Vector3(s, 0.06f, s);
        _color.a = Mathf.Lerp(1f, 0f, _t * _t);
        _mat.color = _color;
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
