using System;
using UnityEngine;

public class NoteController : MonoBehaviour
{
    public float HitTimeSeconds  { get; private set; }
    public int[] Lanes           { get; private set; }
    public bool  IsLong          { get; private set; }
    public float HoldDuration    { get; private set; }
    public bool  IsHit           { get; private set; }
    public bool  IsMissed        { get; private set; }

    RhythmGameManager    _manager;
    Action<NoteController> _returnToPool;
    GameObject _holdBody;
    Material   _holdBodyMat;
    float      _holdEndTime;

    static readonly Color ColorTap  = new Color(1.0f, 0.95f, 0.2f);
    static readonly Color ColorLong = new Color(0.2f, 0.85f, 1.0f);
    static readonly Color ColorBody = new Color(0.15f, 0.65f, 1.0f, 0.7f);

    // Shared materials — created once, reused across all notes
    static Material _sharedTapMat;
    static Material _sharedLongMat;

    static Material SharedMat(bool isLong)
    {
        if (isLong)
            return _sharedLongMat != null ? _sharedLongMat
                : (_sharedLongMat = MakeMat(ColorLong));
        return _sharedTapMat != null ? _sharedTapMat
            : (_sharedTapMat = MakeMat(ColorTap));
    }

    static Material MakeMat(Color c)
    {
        var m = new Material(SafeShader());
        HighwayBuilder.ApplyColor(m, c);
        return m;
    }

    public void Init(float hitTimeSec, int[] lanes, bool isLong, float holdSec,
                     RhythmGameManager manager, Action<NoteController> returnToPool)
    {
        HitTimeSeconds = hitTimeSec;
        Lanes          = lanes;
        IsLong         = isLong;
        HoldDuration   = holdSec;
        IsHit          = false;
        IsMissed       = false;
        _holdEndTime   = 0f;
        _manager       = manager;
        _returnToPool  = returnToPool;

        float x = LaneCenter(lanes);
        float w = LaneSpan(lanes) * 0.88f;

        transform.position   = new Vector3(x, 0.06f, GameConstants.NOTE_Z_SPAWN);
        transform.localScale = new Vector3(w, 0.12f, 0.25f);
        GetComponent<Renderer>().sharedMaterial = SharedMat(isLong);

        if (isLong && holdSec > 0f)
        {
            _holdBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(_holdBody.GetComponent<Collider>());
            float bodyLen = holdSec * GameConstants.NOTE_SPEED;
            _holdBody.transform.position   = new Vector3(x, 0.03f, GameConstants.NOTE_Z_SPAWN - bodyLen * 0.5f);
            _holdBody.transform.localScale = new Vector3(w * 0.65f, 0.07f, bodyLen);
            _holdBodyMat = new Material(SafeShader());
            HighwayBuilder.ApplyColor(_holdBodyMat, ColorBody);
            _holdBody.GetComponent<Renderer>().sharedMaterial = _holdBodyMat;
        }
    }

    static Shader SafeShader() =>
        Shader.Find("Unlit/Color")
     ?? Shader.Find("Universal Render Pipeline/Unlit")
     ?? Shader.Find("Standard");

    void Update()
    {
        if (IsMissed) return;

        float now = _manager.MusicTime;

        if (IsHit && IsLong)
        {
            transform.position = new Vector3(transform.position.x, 0.06f, GameConstants.NOTE_Z_HIT);
            if (_holdBody != null)
            {
                float rem = _holdEndTime - now;
                if (rem <= 0f) { Finish(); return; }
                float blen = rem * GameConstants.NOTE_SPEED;
                _holdBody.transform.localScale = new Vector3(
                    _holdBody.transform.localScale.x,
                    _holdBody.transform.localScale.y,
                    Mathf.Max(0.01f, blen));
                _holdBody.transform.position = new Vector3(
                    transform.position.x, 0.03f,
                    GameConstants.NOTE_Z_HIT - blen * 0.5f);
            }
            return;
        }

        float z = GameConstants.NOTE_Z_HIT - (HitTimeSeconds - now) * GameConstants.NOTE_SPEED;
        transform.position = new Vector3(transform.position.x, 0.06f, z);

        if (_holdBody != null)
        {
            float blen = _holdBody.transform.localScale.z;
            _holdBody.transform.position = new Vector3(transform.position.x, 0.03f, z - blen * 0.5f);
        }

        if (!IsHit && now > HitTimeSeconds + GameConstants.HIT_WINDOW_GOOD)
        {
            IsMissed = true;
            _manager.OnNoteMissed(this);
            Finish();
            return;
        }

        if (z > GameConstants.NOTE_Z_DEAD) { _manager.OnNoteExpired(this); Finish(); }
    }

    public void OnHit()
    {
        IsHit = true;
        if (IsLong)
            _holdEndTime = HitTimeSeconds + HoldDuration;
        else
            Finish();
    }

    public void OnHoldReleased() => Finish();

    void Finish()
    {
        if (_holdBody) { Destroy(_holdBody); _holdBody = null; }
        if (_holdBodyMat != null) { Destroy(_holdBodyMat); _holdBodyMat = null; }

        if (_returnToPool != null)
        {
            gameObject.SetActive(false);
            _returnToPool(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (_holdBody) Destroy(_holdBody);
        if (_holdBodyMat != null) Destroy(_holdBodyMat);
    }

    static float LaneCenter(int[] lanes)
    {
        float sum = 0;
        foreach (int l in lanes) sum += l;
        return (5.5f - sum / lanes.Length) * GameConstants.LANE_SPACING;
    }

    static float LaneSpan(int[] lanes)
    {
        int lo = lanes[0], hi = lanes[0];
        foreach (int l in lanes) { if (l < lo) lo = l; if (l > hi) hi = l; }
        return (hi - lo + 1) * GameConstants.LANE_SPACING;
    }
}
