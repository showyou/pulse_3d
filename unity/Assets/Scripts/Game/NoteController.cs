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
    public bool  IsSlide         { get; private set; }
    public int   SlideEndGroup   { get; private set; }
    public bool  IsHeld          { get; private set; }

    RhythmGameManager      _manager;
    Action<NoteController> _returnToPool;
    GameObject _holdBody;
    Material   _holdBodyMat;
    float      _holdEndTime;
    GameObject _slideTarget;
    GameObject _highlight;       // 上面ハイライト（全ノーツ）
    GameObject _frontHighlight;  // 手前フェースハイライト（全ノーツ）
    float      _topHighY;        // 上面ハイライトの Y 座標

    static readonly Color ColorTap       = new Color(1.0f, 0.30f, 0.58f);
    static readonly Color ColorTapHi     = new Color(1.0f, 0.70f, 0.85f);
    static readonly Color ColorHeld      = new Color(1.0f, 0.95f, 0.2f);
    static readonly Color ColorHeldHi    = new Color(1.0f, 1.0f,  0.65f);
    static readonly Color ColorLong      = new Color(0.2f, 0.85f, 1.0f);
    static readonly Color ColorLongHi    = new Color(0.6f, 0.97f, 1.0f);
    static readonly Color ColorSlide     = new Color(1.0f, 0.55f, 0.05f);
    static readonly Color ColorSlideHi   = new Color(1.0f, 0.80f, 0.45f);
    static readonly Color ColorBody      = new Color(0.15f, 0.65f, 1.0f, 0.7f);
    static readonly Color ColorTarget    = new Color(1.0f, 0.75f, 0.2f);

    static Material _sharedTapMat;
    static Material _sharedTapHiMat;
    static Material _sharedHeldMat;
    static Material _sharedHeldHiMat;
    static Material _sharedLongMat;
    static Material _sharedLongHiMat;
    static Material _sharedSlideMat;
    static Material _sharedSlideHiMat;

    static Material SharedMat(bool isLong, bool isSlide, bool isHeld)
    {
        if (isSlide) return _sharedSlideMat ??= MakeMat(ColorSlide);
        if (isLong)  return _sharedLongMat  ??= MakeMat(ColorLong);
        if (isHeld)  return _sharedHeldMat  ??= MakeMat(ColorHeld);
        return           _sharedTapMat  ??= MakeMat(ColorTap);
    }

    static Material HiMat(bool isLong, bool isSlide, bool isHeld)
    {
        if (isSlide) return _sharedSlideHiMat ??= MakeMat(ColorSlideHi);
        if (isLong)  return _sharedLongHiMat  ??= MakeMat(ColorLongHi);
        if (isHeld)  return _sharedHeldHiMat  ??= MakeMat(ColorHeldHi);
        return           _sharedTapHiMat  ??= MakeMat(ColorTapHi);
    }

    static Material MakeMat(Color c)
    {
        var m = new Material(SafeShader());
        HighwayBuilder.ApplyColor(m, c);
        return m;
    }

    public void Init(float hitTimeSec, int[] lanes, bool isLong, float holdSec,
                     RhythmGameManager manager, Action<NoteController> returnToPool,
                     int slideEndGroup = -1, bool isHeld = false)
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
        IsSlide        = slideEndGroup >= 0;
        SlideEndGroup  = slideEndGroup;
        IsHeld         = isHeld;

        float x = LaneCenter(lanes);
        float w = LaneSpan(lanes) * 0.88f;

        float noteH = (isLong || IsSlide || isHeld) ? 0.12f : 0.22f;
        transform.position   = new Vector3(x, 0.06f, GameConstants.NOTE_Z_SPAWN);
        transform.localScale = new Vector3(w, noteH, 0.25f);
        GetComponent<Renderer>().sharedMaterial = SharedMat(isLong, IsSlide, isHeld);

        var hiMat = HiMat(isLong, IsSlide, isHeld);
        _topHighY = 0.06f + noteH * 0.5f;

        // 上面ハイライト（全ノーツ）
        _highlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(_highlight.GetComponent<Collider>());
        _highlight.transform.localScale = new Vector3(w * 0.88f, 0.02f, 0.23f);
        _highlight.GetComponent<Renderer>().sharedMaterial = hiMat;

        // 手前フェースハイライト（全ノーツ）
        _frontHighlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(_frontHighlight.GetComponent<Collider>());
        _frontHighlight.transform.localScale = new Vector3(w * 0.85f, noteH * 0.75f, 0.03f);
        _frontHighlight.GetComponent<Renderer>().sharedMaterial = hiMat;

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

        if (IsSlide)
        {
            _slideTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(_slideTarget.GetComponent<Collider>());
            _slideTarget.transform.localScale = new Vector3(GameConstants.LANE_SPACING * 1.7f, 0.12f, 0.18f);
            var tMat = new Material(SafeShader());
            HighwayBuilder.ApplyColor(tMat, ColorTarget);
            _slideTarget.GetComponent<Renderer>().sharedMaterial = tMat;
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
            UpdateHighlights(GameConstants.NOTE_Z_HIT);
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

        if (_slideTarget != null)
            _slideTarget.transform.position = new Vector3(SlideEndX(), 0.08f, z);

        UpdateHighlights(z);

        if (!IsHit && now > HitTimeSeconds + GameConstants.HIT_WINDOW_GOOD)
        {
            if (IsLong && HoldDuration > 0f && now < HitTimeSeconds + HoldDuration)
            {
                transform.position = new Vector3(transform.position.x, 0.06f, GameConstants.NOTE_Z_HIT);
                UpdateHighlights(GameConstants.NOTE_Z_HIT);
                if (_holdBody != null)
                {
                    float rem  = HitTimeSeconds + HoldDuration - now;
                    float blen = Mathf.Max(0.01f, rem * GameConstants.NOTE_SPEED);
                    _holdBody.transform.localScale = new Vector3(
                        _holdBody.transform.localScale.x,
                        _holdBody.transform.localScale.y, blen);
                    _holdBody.transform.position = new Vector3(
                        transform.position.x, 0.03f, GameConstants.NOTE_Z_HIT - blen * 0.5f);
                }
                return;
            }
            IsMissed = true;
            _manager.OnNoteMissed(this);
            Finish();
            return;
        }

        if (z > GameConstants.NOTE_Z_DEAD) { _manager.OnNoteExpired(this); Finish(); }
    }

    void UpdateHighlights(float z)
    {
        float x = transform.position.x;
        if (_highlight != null)
            _highlight.transform.position = new Vector3(x, _topHighY, z);
        if (_frontHighlight != null)
            _frontHighlight.transform.position = new Vector3(x, 0.06f, z + 0.14f);
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
        if (_holdBody)       { Destroy(_holdBody);       _holdBody       = null; }
        if (_holdBodyMat)    { Destroy(_holdBodyMat);    _holdBodyMat    = null; }
        if (_slideTarget)    { Destroy(_slideTarget);    _slideTarget    = null; }
        if (_highlight)      { Destroy(_highlight);      _highlight      = null; }
        if (_frontHighlight) { Destroy(_frontHighlight); _frontHighlight = null; }

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
        if (_holdBody)       Destroy(_holdBody);
        if (_holdBodyMat)    Destroy(_holdBodyMat);
        if (_slideTarget)    Destroy(_slideTarget);
        if (_highlight)      Destroy(_highlight);
        if (_frontHighlight) Destroy(_frontHighlight);
    }

    float SlideEndX()
    {
        int lA = GameConstants.KEY_LANES[SlideEndGroup, 0];
        int lB = GameConstants.KEY_LANES[SlideEndGroup, 1];
        return (5.5f - (lA + lB) * 0.5f) * GameConstants.LANE_SPACING;
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
