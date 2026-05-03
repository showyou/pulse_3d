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
    public bool  IsMultiSlide    { get; private set; }  // マルチウェイポイントスライド

    // ロング＋スライドエンドグループ指定 = レーン移動ロングノーツ（旧形式）
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
    float      _startX;
    float      _slideLongEndX;

    // マルチウェイポイント用
    float[]      _wpX;          // 各ウェイポイントのワールドX
    float[]      _wpOffsetSec;  // 各ウェイポイントのヒット時刻からの相対時間（秒）
    GameObject[] _slideSegObjs; // 区間ごとのボディセグメント
    bool         _isMultiSlide;

    static readonly Color ColorTap           = new Color(1.0f, 0.30f, 0.58f);
    static readonly Color ColorTapHi         = new Color(1.0f, 0.70f, 0.85f);
    static readonly Color ColorHeld          = new Color(1.0f, 0.95f, 0.2f);
    static readonly Color ColorHeldHi        = new Color(1.0f, 1.0f,  0.65f);
    static readonly Color ColorLong          = new Color(0.2f, 0.85f, 1.0f);
    static readonly Color ColorLongHi        = new Color(0.6f, 0.97f, 1.0f);
    static readonly Color ColorSlide         = new Color(1.0f, 0.55f, 0.05f);
    static readonly Color ColorSlideHi       = new Color(1.0f, 0.80f, 0.45f);
    static readonly Color ColorSlideLong     = new Color(0.15f, 1.0f, 0.65f);
    static readonly Color ColorSlideLongHi   = new Color(0.55f, 1.0f, 0.82f);
    static readonly Color ColorBody          = new Color(0.15f, 0.65f, 1.0f, 0.7f);
    static readonly Color ColorSlideLongBody = new Color(0.1f,  0.8f,  0.5f, 0.7f);
    static readonly Color ColorTarget        = new Color(1.0f, 0.75f, 0.2f);

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

    static Material SharedMat(bool isLong, bool isSlide, bool isHeld, bool isMultiSlide)
    {
        if (isMultiSlide)      return _sharedSlideLongMat   ??= MakeMat(ColorSlideLong);
        if (isSlide && isLong) return _sharedSlideLongMat   ??= MakeMat(ColorSlideLong);
        if (isSlide)           return _sharedSlideMat       ??= MakeMat(ColorSlide);
        if (isLong)            return _sharedLongMat        ??= MakeMat(ColorLong);
        if (isHeld)            return _sharedHeldMat        ??= MakeMat(ColorHeld);
        return                        _sharedTapMat         ??= MakeMat(ColorTap);
    }

    static Material HiMat(bool isLong, bool isSlide, bool isHeld, bool isMultiSlide)
    {
        if (isMultiSlide)      return _sharedSlideLongHiMat ??= MakeMat(ColorSlideLongHi);
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
                     int slideEndGroup = -1, bool isHeld = false,
                     SlidePoint[] slidePoints = null)
    {
        HitTimeSeconds = hitTimeSec;
        Lanes          = lanes;
        IsLong         = isLong;
        IsHit          = false;
        IsMissed       = false;
        _holdEndTime   = 0f;
        _manager       = manager;
        _returnToPool  = returnToPool;
        IsSlide        = slideEndGroup >= 0;
        SlideEndGroup  = slideEndGroup;
        IsHeld         = isHeld;

        // マルチウェイポイント判定
        _isMultiSlide = isLong && slidePoints != null && slidePoints.Length >= 2;
        IsMultiSlide  = _isMultiSlide;

        // holdSec: マルチスライドは最終ウェイポイントのoffsetMsを優先
        HoldDuration = _isMultiSlide
            ? slidePoints[slidePoints.Length - 1].offsetMs / 1000f
            : holdSec;

        float x = LaneCenter(lanes);
        float w = LaneSpan(lanes) * 0.88f;
        _startX = x;

        // 旧形式スライドロング終点X
        if (IsSlideLong)
        {
            int lA = GameConstants.KEY_LANES[slideEndGroup, 0];
            int lB = GameConstants.KEY_LANES[slideEndGroup, 1];
            _slideLongEndX = (5.5f - (lA + lB) * 0.5f) * GameConstants.LANE_SPACING;
        }

        float noteH = (isLong || IsSlide || isHeld || _isMultiSlide) ? 0.12f : 0.22f;
        transform.position   = new Vector3(x, 0.06f, GameConstants.NOTE_Z_SPAWN);
        transform.localScale = new Vector3(w, noteH, 0.25f);
        GetComponent<Renderer>().sharedMaterial = SharedMat(isLong, IsSlide, isHeld, _isMultiSlide);

        var hiMat = HiMat(isLong, IsSlide, isHeld, _isMultiSlide);
        _topHighY = 0.06f + noteH * 0.5f;

        _highlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(_highlight.GetComponent<Collider>());
        _highlight.transform.localScale = new Vector3(w * 0.88f, 0.02f, 0.23f);
        _highlight.GetComponent<Renderer>().sharedMaterial = hiMat;

        _frontHighlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(_frontHighlight.GetComponent<Collider>());
        _frontHighlight.transform.localScale = new Vector3(w * 0.85f, noteH * 0.75f, 0.03f);
        _frontHighlight.GetComponent<Renderer>().sharedMaterial = hiMat;

        if (_isMultiSlide)
        {
            // ウェイポイントデータ構築
            _wpX         = new float[slidePoints.Length];
            _wpOffsetSec = new float[slidePoints.Length];
            for (int i = 0; i < slidePoints.Length; i++)
            {
                _wpX[i]         = GroupCenterX(slidePoints[i].group);
                _wpOffsetSec[i] = slidePoints[i].offsetMs / 1000f;
            }

            // セグメントオブジェクト生成
            float segW = GameConstants.LANE_SPACING * 1.3f;
            _slideSegObjs = new GameObject[slidePoints.Length - 1];
            var segMat = new Material(SafeShader());
            HighwayBuilder.ApplyColor(segMat, ColorSlideLongBody);
            for (int i = 0; i < _slideSegObjs.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "SlideSegment";
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = new Vector3(segW, 0.07f, 1f);
                go.GetComponent<Renderer>().sharedMaterial = segMat;
                _slideSegObjs[i] = go;
            }
            UpdateSegmentsScrolling(GameConstants.NOTE_Z_SPAWN);
        }
        else if (isLong && HoldDuration > 0f)
        {
            _holdBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(_holdBody.GetComponent<Collider>());
            float blenZ = HoldDuration * GameConstants.NOTE_SPEED;
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

    // ---------------------------------------------------------------
    // スライドロング（旧形式）ボディ初期化
    void InitSlideLongBody(float headX, float headZ, float blenZ, float w)
    {
        float dx    = _slideLongEndX - headX;
        float len3d = Mathf.Sqrt(dx * dx + blenZ * blenZ);
        _holdBody.transform.localScale = new Vector3(w * 0.65f, 0.07f, Mathf.Max(0.01f, len3d));
        _holdBody.transform.position   = new Vector3(
            (headX + _slideLongEndX) * 0.5f, 0.03f, headZ - blenZ * 0.5f);
        _holdBody.transform.rotation   = Quaternion.LookRotation(
            new Vector3(headX - _slideLongEndX, 0f, blenZ).normalized, Vector3.up);
    }

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

    // ---------------------------------------------------------------
    // マルチウェイポイント セグメント更新
    void UpdateSegmentsScrolling(float headZ)
    {
        for (int i = 0; i < _slideSegObjs.Length; i++)
        {
            float x0 = _wpX[i],     z0 = headZ - _wpOffsetSec[i]     * GameConstants.NOTE_SPEED;
            float x1 = _wpX[i + 1], z1 = headZ - _wpOffsetSec[i + 1] * GameConstants.NOTE_SPEED;
            SetSegmentCube(_slideSegObjs[i], x0, z0, x1, z1);
        }
    }

    void UpdateSegmentsHeld(float elapsed)
    {
        // 現在どの区間にいるか
        int curSeg = 0;
        while (curSeg < _slideSegObjs.Length - 1 && elapsed >= _wpOffsetSec[curSeg + 1])
            curSeg++;

        for (int i = 0; i < _slideSegObjs.Length; i++)
        {
            if (i < curSeg) { _slideSegObjs[i].SetActive(false); continue; }
            _slideSegObjs[i].SetActive(true);

            float x0, z0, x1, z1;
            if (i == curSeg)
            {
                x0 = ComputeHeadX(elapsed);
                z0 = GameConstants.NOTE_Z_HIT;
                x1 = _wpX[i + 1];
                z1 = GameConstants.NOTE_Z_HIT - (_wpOffsetSec[i + 1] - elapsed) * GameConstants.NOTE_SPEED;
            }
            else
            {
                x0 = _wpX[i];
                z0 = GameConstants.NOTE_Z_HIT - (_wpOffsetSec[i]     - elapsed) * GameConstants.NOTE_SPEED;
                x1 = _wpX[i + 1];
                z1 = GameConstants.NOTE_Z_HIT - (_wpOffsetSec[i + 1] - elapsed) * GameConstants.NOTE_SPEED;
            }
            SetSegmentCube(_slideSegObjs[i], x0, z0, x1, z1);
        }
    }

    // セグメントキューブの位置・スケール・回転を設定（x0,z0=ヘッド端, x1,z1=テール端）
    static void SetSegmentCube(GameObject go, float x0, float z0, float x1, float z1)
    {
        float dx = x0 - x1, dz = z0 - z1;
        float len = Mathf.Sqrt(dx * dx + dz * dz);
        if (len < 0.001f) { go.SetActive(false); return; }
        go.SetActive(true);
        go.transform.position  = new Vector3((x0 + x1) * 0.5f, 0.03f, (z0 + z1) * 0.5f);
        go.transform.localScale = new Vector3(
            go.transform.localScale.x, go.transform.localScale.y, len);
        go.transform.rotation = Quaternion.LookRotation(
            new Vector3(dx, 0f, dz).normalized, Vector3.up);
    }

    // 経過時間からヘッドのワールドX座標を補間
    float ComputeHeadX(float elapsed)
    {
        for (int i = 0; i < _wpX.Length - 1; i++)
        {
            if (elapsed <= _wpOffsetSec[i + 1])
            {
                float dur = _wpOffsetSec[i + 1] - _wpOffsetSec[i];
                float t   = dur > 0f ? Mathf.Clamp01((elapsed - _wpOffsetSec[i]) / dur) : 1f;
                return Mathf.Lerp(_wpX[i], _wpX[i + 1], t);
            }
        }
        return _wpX[_wpX.Length - 1];
    }

    static float GroupCenterX(int group)
    {
        float avg = (GameConstants.KEY_LANES[group, 0] + GameConstants.KEY_LANES[group, 1]) * 0.5f;
        return (5.5f - avg) * GameConstants.LANE_SPACING;
    }

    // ---------------------------------------------------------------
    void Update()
    {
        if (IsMissed) return;

        float now = _manager.MusicTime;

        if (IsHit && IsLong)
        {
            float rem     = _holdEndTime - now;
            float elapsed = HoldDuration - rem;

            float headX;
            if (_isMultiSlide)
            {
                if (rem <= 0f) { Finish(); return; }
                headX = ComputeHeadX(elapsed);
                transform.position = new Vector3(headX, 0.06f, GameConstants.NOTE_Z_HIT);
                UpdateHighlights(GameConstants.NOTE_Z_HIT);
                UpdateSegmentsHeld(elapsed);
            }
            else
            {
                headX = IsSlideLong && HoldDuration > 0f
                    ? Mathf.Lerp(_startX, _slideLongEndX, Mathf.Clamp01(elapsed / HoldDuration))
                    : transform.position.x;
                transform.position = new Vector3(headX, 0.06f, GameConstants.NOTE_Z_HIT);
                UpdateHighlights(GameConstants.NOTE_Z_HIT);
                if (_holdBody != null)
                {
                    if (rem <= 0f) { Finish(); return; }
                    SetBodyTransform(headX, GameConstants.NOTE_Z_HIT, rem * GameConstants.NOTE_SPEED);
                }
            }
            return;
        }

        float z = GameConstants.NOTE_Z_HIT - (HitTimeSeconds - now) * GameConstants.NOTE_SPEED;
        transform.position = new Vector3(transform.position.x, 0.06f, z);

        if (_isMultiSlide)
        {
            UpdateSegmentsScrolling(z);
        }
        else if (_holdBody != null)
        {
            if (IsSlideLong)
            {
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
                float rem     = HitTimeSeconds + HoldDuration - now;
                float elapsed = HoldDuration - rem;
                float blenZ   = Mathf.Max(0.01f, rem * GameConstants.NOTE_SPEED);

                float headX;
                if (_isMultiSlide)
                {
                    headX = ComputeHeadX(elapsed);
                    transform.position = new Vector3(headX, 0.06f, GameConstants.NOTE_Z_HIT);
                    UpdateHighlights(GameConstants.NOTE_Z_HIT);
                    UpdateSegmentsHeld(elapsed);
                }
                else
                {
                    headX = IsSlideLong
                        ? Mathf.Lerp(_startX, _slideLongEndX, Mathf.Clamp01(elapsed / HoldDuration))
                        : transform.position.x;
                    transform.position = new Vector3(headX, 0.06f, GameConstants.NOTE_Z_HIT);
                    UpdateHighlights(GameConstants.NOTE_Z_HIT);
                    if (_holdBody != null)
                        SetBodyTransform(headX, GameConstants.NOTE_Z_HIT, blenZ);
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
        if (_slideSegObjs != null)
        {
            foreach (var go in _slideSegObjs) if (go) Destroy(go);
            _slideSegObjs = null;
        }

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
        if (_slideSegObjs != null)
            foreach (var go in _slideSegObjs) if (go) Destroy(go);
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
