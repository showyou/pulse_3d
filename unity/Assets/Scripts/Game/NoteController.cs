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

    // ロング＋スライド = レーン移動ロングノーツ
    public bool IsSlideLong => IsLong && IsSlide;

    RhythmGameManager      _manager;
    Action<NoteController> _returnToPool;
    GameObject _holdBody;
    Material   _holdBodyMat;
    float      _holdEndTime;
    GameObject _slideTarget;
    GameObject _highlight;
    GameObject _frontHighlight;
    float      _topHighY;
    float      _startX;         // 始点X（スライドロング用）
    float      _slideLongEndX;  // 終点X（スライドロング用）

    static readonly Color ColorTap         = new Color(1.0f, 0.30f, 0.58f);
    static readonly Color ColorTapHi       = new Color(1.0f, 0.70f, 0.85f);
    static readonly Color ColorHeld        = new Color(1.0f, 0.95f, 0.2f);
    static readonly Color ColorHeldHi      = new Color(1.0f, 1.0f,  0.65f);
    static readonly Color ColorLong        = new Color(0.2f, 0.85f, 1.0f);
    static readonly Color ColorLongHi      = new Color(0.6f, 0.97f, 1.0f);
    static readonly Color ColorSlide       = new Color(1.0f, 0.55f, 0.05f);
    static readonly Color ColorSlideHi     = new Color(1.0f, 0.80f, 0.45f);
    static readonly Color ColorSlideLong   = new Color(0.15f, 1.0f, 0.65f);  // ティール
    static readonly Color ColorSlideLongHi = new Color(0.55f, 1.0f, 0.82f);
    static readonly Color ColorBody        = new Color(0.15f, 0.65f, 1.0f, 0.7f);
    static readonly Color ColorSlideLongBody = new Color(0.1f, 0.8f, 0.5f, 0.7f);
    static readonly Color ColorTarget      = new Color(1.0f, 0.75f, 0.2f);

    static Material _sharedTapMat;
    static Material _sharedTapHiMat;
    static Material _sharedHeldMat;
    static Material _sharedHeldHiMat;
    static Material _sharedLongMat;
    static Material _sharedLongHiMat;
    static Material _sharedSlideMat;
    static Material _sharedSlideHiMat;
    static Material _sharedSlideLongMat;
    static Material _sharedSlideLongHiMat;

    static Material SharedMat(bool isLong, bool isSlide, bool isHeld)
    {
        if (isSlide && isLong) return _sharedSlideLongMat ??= MakeMat(ColorSlideLong);
        if (isSlide)           return _sharedSlideMat     ??= MakeMat(ColorSlide);
        if (isLong)            return _sharedLongMat      ??= MakeMat(ColorLong);
        if (isHeld)            return _sharedHeldMat      ??= MakeMat(ColorHeld);
        return                        _sharedTapMat       ??= MakeMat(ColorTap);
    }

    static Material HiMat(bool isLong, bool isSlide, bool isHeld)
    {
        if (isSlide && isLong) return _sharedSlideLongHiMat ??= MakeMat(ColorSlideLongHi);
        if (isSlide)           return _sharedSlideHiMat     ??= MakeMat(ColorSlideHi);
        if (isLong)            return _sharedLongHiMat      ??= MakeMat(ColorLongHi);
        if (isHeld)            return _sharedHeldHiMat      ??= MakeMat(ColorHeldHi);
        return                        _sharedTapHiMat       ??= MakeMat(ColorTapHi);
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
        _startX = x;

        // スライドロング終点X
        if (IsSlideLong)
        {
            int lA = GameConstants.KEY_LANES[slideEndGroup, 0];
            int lB = GameConstants.KEY_LANES[slideEndGroup, 1];
            _slideLongEndX = (5.5f - (lA + lB) * 0.5f) * GameConstants.LANE_SPACING;
        }

        float noteH = (isLong || IsSlide || isHeld) ? 0.12f : 0.22f;
        transform.position   = new Vector3(x, 0.06f, GameConstants.NOTE_Z_SPAWN);
        transform.localScale = new Vector3(w, noteH, 0.25f);
        GetComponent<Renderer>().sharedMaterial = SharedMat(isLong, IsSlide, isHeld);

        var hiMat = HiMat(isLong, IsSlide, isHeld);
        _topHighY = 0.06f + noteH * 0.5f;

        _highlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(_highlight.GetComponent<Collider>());
        _highlight.transform.localScale = new Vector3(w * 0.88f, 0.02f, 0.23f);
        _highlight.GetComponent<Renderer>().sharedMaterial = hiMat;

        _frontHighlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(_frontHighlight.GetComponent<Collider>());
        _frontHighlight.transform.localScale = new Vector3(w * 0.85f, noteH * 0.75f, 0.03f);
        _frontHighlight.GetComponent<Renderer>().sharedMaterial = hiMat;

        if (isLong && holdSec > 0f)
        {
            _holdBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(_holdBody.GetComponent<Collider>());
            float blenZ = holdSec * GameConstants.NOTE_SPEED;
            _holdBodyMat = new Material(SafeShader());
            HighwayBuilder.ApplyColor(_holdBodyMat, IsSlideLong ? ColorSlideLongBody : ColorBody);
            _holdBody.GetComponent<Renderer>().sharedMaterial = _holdBodyMat;

            if (IsSlideLong)
                InitSlideLongBody(x, GameConstants.NOTE_Z_SPAWN, blenZ, w);
            else
            {
                _holdBody.transform.position   = new Vector3(x, 0.03f, GameConstants.NOTE_Z_SPAWN - blenZ * 0.5f);
                _holdBody.transform.localScale = new Vector3(w * 0.65f, 0.07f, blenZ);
            }
        }

        // スライドタップのみ終点インジケータを表示（スライドロングは不要）
        if (IsSlide && !IsSlideLong)
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

    // スライドロングのボディ初期化（Init時のみ）
    void InitSlideLongBody(float headX, float headZ, float blenZ, float w)
    {
        float dx   = _slideLongEndX - headX;
        float len3d = Mathf.Sqrt(dx * dx + blenZ * blenZ);
        _holdBody.transform.localScale = new Vector3(w * 0.65f, 0.07f, Mathf.Max(0.01f, len3d));
        _holdBody.transform.position   = new Vector3(
            (headX + _slideLongEndX) * 0.5f, 0.03f, headZ - blenZ * 0.5f);
        _holdBody.transform.rotation   = Quaternion.LookRotation(
            new Vector3(headX - _slideLongEndX, 0f, blenZ).normalized, Vector3.up);
    }

    // ボディの位置・スケール・回転を更新（毎フレーム）
    void SetBodyTransform(float headX, float headZ, float blenZ)
    {
        float bz = Mathf.Max(0.01f, blenZ);
        if (IsSlideLong)
        {
            float dx    = _slideLongEndX - headX;
            float len3d = Mathf.Max(0.01f, Mathf.Sqrt(dx * dx + bz * bz));
            _holdBody.transform.localScale = new Vector3(
                _holdBody.transform.localScale.x,
                _holdBody.transform.localScale.y, len3d);
            _holdBody.transform.position = new Vector3(
                (headX + _slideLongEndX) * 0.5f, 0.03f, headZ - bz * 0.5f);
            _holdBody.transform.rotation = Quaternion.LookRotation(
                new Vector3(headX - _slideLongEndX, 0f, bz).normalized, Vector3.up);
        }
        else
        {
            _holdBody.transform.localScale = new Vector3(
                _holdBody.transform.localScale.x,
                _holdBody.transform.localScale.y, bz);
            _holdBody.transform.position = new Vector3(headX, 0.03f, headZ - bz * 0.5f);
        }
    }

    void Update()
    {
        if (IsMissed) return;

        float now = _manager.MusicTime;

        if (IsHit && IsLong)
        {
            float rem = _holdEndTime - now;

            // 始点→終点へのヘッド移動（スライドロング）
            float headX = IsSlideLong && HoldDuration > 0f
                ? Mathf.Lerp(_startX, _slideLongEndX, 1f - Mathf.Clamp01(rem / HoldDuration))
                : transform.position.x;

            transform.position = new Vector3(headX, 0.06f, GameConstants.NOTE_Z_HIT);
            UpdateHighlights(GameConstants.NOTE_Z_HIT);

            if (_holdBody != null)
            {
                if (rem <= 0f) { Finish(); return; }
                SetBodyTransform(headX, GameConstants.NOTE_Z_HIT, rem * GameConstants.NOTE_SPEED);
            }
            return;
        }

        float z = GameConstants.NOTE_Z_HIT - (HitTimeSeconds - now) * GameConstants.NOTE_SPEED;
        transform.position = new Vector3(transform.position.x, 0.06f, z);

        if (_holdBody != null)
        {
            if (IsSlideLong)
            {
                // 回転・スケールは固定、z位置のみ追従
                float blenZ = HoldDuration * GameConstants.NOTE_SPEED;
                _holdBody.transform.position = new Vector3(
                    (_startX + _slideLongEndX) * 0.5f, 0.03f, z - blenZ * 0.5f);
            }
            else
            {
                float blen = _holdBody.transform.localScale.z;
                _holdBody.transform.position = new Vector3(transform.position.x, 0.03f, z - blen * 0.5f);
            }
        }

        if (_slideTarget != null)
            _slideTarget.transform.position = new Vector3(SlideEndX(), 0.08f, z);

        UpdateHighlights(z);

        if (!IsHit && now > HitTimeSeconds + GameConstants.HIT_WINDOW_GOOD)
        {
            if (IsLong && HoldDuration > 0f && now < HitTimeSeconds + HoldDuration)
            {
                float rem    = HitTimeSeconds + HoldDuration - now;
                float blenZ  = Mathf.Max(0.01f, rem * GameConstants.NOTE_SPEED);
                float headX  = IsSlideLong
                    ? Mathf.Lerp(_startX, _slideLongEndX, 1f - Mathf.Clamp01(rem / HoldDuration))
                    : transform.position.x;

                transform.position = new Vector3(headX, 0.06f, GameConstants.NOTE_Z_HIT);
                UpdateHighlights(GameConstants.NOTE_Z_HIT);
                if (_holdBody != null)
                    SetBodyTransform(headX, GameConstants.NOTE_Z_HIT, blenZ);
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
