using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

public enum Judgment { Perfect, Good, Miss }

public class RhythmGameManager : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Chart")]
    [Tooltip("charts/ 以下のファイル名（拡張子なし）。例: demo, starmine")]
    public string chartId = "demo";

    // -- runtime state --
    public float MusicTime { get; private set; }

    int _score;
    int _combo;
    int _perfect, _good, _miss;

    bool _isPlaying;
    double _startDspTime;

    ChartData _chart;
    int _spawnIndex;

    // active notes on screen, indexed by lane
    readonly List<NoteController>[] _laneNotes = new List<NoteController>[GameConstants.NUM_LANES];

    // long-note hold state per key group
    readonly NoteController[] _holdNote    = new NoteController[6];
    readonly bool[]            _keyHeld    = new bool[6];
    readonly float[]           _holdTick   = new float[6]; // 100ms tick timer

    InputHandler _input;
    HighwayBuilder _highway;

    [Header("Autoplay")]
    public bool autoPlay = false;

    VideoPlayer _videoPlayer;

    // -- UI state --
    string _judgmentText = "";
    float _judgmentTimer;
    string _statusMsg = "Loading...";

    void Awake()
    {
        for (int i = 0; i < GameConstants.NUM_LANES; i++)
            _laneNotes[i] = new List<NoteController>();
    }

    void Start()
    {
        SetupCamera();

        _highway = gameObject.AddComponent<HighwayBuilder>();
        _highway.Build();

        _input = gameObject.AddComponent<InputHandler>();
        _input.OnKeyPressed  += OnGroupPressed;
        _input.OnKeyReleased += OnGroupReleased;

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        LoadChart();
    }

    // ---------------------------------------------------------------
    void LoadChart()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "charts", chartId + ".json");
        Debug.Log($"[PULSE] Loading chart: {path}");

        if (!File.Exists(path) && chartId != "demo")
        {
            Debug.LogWarning($"[PULSE] '{chartId}' not found — falling back to 'demo'");
            chartId = "demo";
            path = Path.Combine(Application.streamingAssetsPath, "charts", "demo.json");
        }

        if (!File.Exists(path))
        {
            _statusMsg = $"Chart not found: charts/{chartId}.json";
            Debug.LogError($"[PULSE] {_statusMsg}");
            return;
        }

        _chart = ParseChart(File.ReadAllText(path));

        if (_chart == null || _chart.notes == null || _chart.notes.Count == 0)
        {
            _statusMsg = $"Chart parse failed: {chartId}";
            Debug.LogError($"[PULSE] {_statusMsg}");
            return;
        }

        _chart.notes.Sort((a, b) => a.t.CompareTo(b.t));
        Debug.Log($"[PULSE] Loaded '{_chart.meta.title}' — {_chart.notes.Count} notes.");
        StartCoroutine(LoadAudioThenStart());
    }

    IEnumerator LoadAudioThenStart()
    {
        string audioFile = _chart?.meta?.audioFile;
        if (!string.IsNullOrEmpty(audioFile))
        {
            string audioPath = "file://" + Path.Combine(Application.streamingAssetsPath, "songs", audioFile);
            _statusMsg = "Loading audio...";
            using var req = UnityWebRequestMultimedia.GetAudioClip(audioPath, AudioType.UNKNOWN);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                audioSource.clip = DownloadHandlerAudioClip.GetContent(req);
                Debug.Log($"[PULSE] Audio loaded: {audioFile}");
            }
            else
            {
                Debug.LogWarning($"[PULSE] Audio load failed ({audioFile}): {req.error}");
            }
        }
        SetupBackgroundVideo(_chart?.meta?.videoFile ?? "");
        StartGame();
    }

    // Unity chart format か HTML fumen.json かを自動判別してパース
    ChartData ParseChart(string json)
    {
        var data = JsonUtility.FromJson<ChartData>(json);
        if (data?.meta != null && data.notes?.Count > 0)
            return data;

        // HTML fumen.json フォーマット
        var fumen = JsonUtility.FromJson<FumenRoot>(json);
        if (fumen?.notes == null || fumen.notes.Count == 0) return null;

        var chart = new ChartData();
        chart.meta = new ChartMeta {
            title     = fumen.title ?? chartId,
            duration  = (int)(fumen.totalDuration * 1000),
            audioFile = fumen.audioFile ?? "",
        };
        chart.notes = new List<ChartNote>(fumen.notes.Count);
        foreach (var n in fumen.notes)
        {
            bool isLong    = n.duration > 0.05f;
            int mappedLane = n.lane % GameConstants.NUM_LANES;
            chart.notes.Add(new ChartNote {
                t      = (int)(n.time * 1000),
                lanes  = new[] { mappedLane },
                isLong = isLong,
                holdMs = isLong ? (int)(n.duration * 1000) : 0,
            });
        }
        Debug.Log($"[PULSE] Converted fumen.json '{fumen.title}' ({fumen.notes.Count} notes)");
        return chart;
    }

    void StartGame()
    {
        _statusMsg = "";
        _spawnIndex = 0;
        _score = _combo = _perfect = _good = _miss = 0;

        // Sync audio with dspTime for tight timing
        _startDspTime = AudioSettings.dspTime + 1.0; // 1s lead-in
        if (audioSource.clip != null)
            audioSource.PlayScheduled(_startDspTime);

        _isPlaying = true;

        if (_videoPlayer != null)
            StartCoroutine(PlayVideoWhenReady());
    }

    void SetupBackgroundVideo(string videoFile)
    {
        if (string.IsNullOrEmpty(videoFile)) return;

        if (_videoPlayer != null) Destroy(_videoPlayer.gameObject);

        string path = "file://" + Path.Combine(Application.streamingAssetsPath, "songs", videoFile);
        var go = new GameObject("BackgroundVideo");
        _videoPlayer = go.AddComponent<VideoPlayer>();
        _videoPlayer.renderMode    = VideoRenderMode.CameraFarPlane;
        _videoPlayer.targetCamera  = Camera.main;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        _videoPlayer.isLooping     = true;
        _videoPlayer.playOnAwake   = false;
        _videoPlayer.url           = path;
        _videoPlayer.Prepare();
        Debug.Log($"[PULSE] Preparing video: {videoFile}");
    }

    IEnumerator PlayVideoWhenReady()
    {
        yield return new WaitUntil(() => _videoPlayer.isPrepared);
        // 音楽スタート時刻に合わせてオフセット補正
        double lag = AudioSettings.dspTime - _startDspTime;
        if (lag > 0) _videoPlayer.time = lag;
        _videoPlayer.Play();
        Debug.Log($"[PULSE] Video started (offset {lag:F3}s)");
    }

    // ---------------------------------------------------------------
    void Update()
    {
        if (!_isPlaying || _chart == null) return;

        MusicTime = (float)(AudioSettings.dspTime - _startDspTime);

        SpawnDueNotes();
        if (autoPlay) AutoPlayUpdate();
        CheckHeldKeysForLongNotes(); // キー押しっぱなしでロングノーツを自動キャッチ
        UpdateHoldTicks();           // ホールド中の100msティックスコア
        UpdateJudgmentDisplay();
        UpdateCameraBob();
    }

    void AutoPlayUpdate()
    {
        for (int g = 0; g < 6; g++)
        {
            if (_holdNote[g] != null)
            {
                if (MusicTime >= _holdNote[g].HitTimeSeconds + _holdNote[g].HoldDuration)
                    OnGroupReleased(g);
            }
            else
            {
                int laneA = GameConstants.KEY_LANES[g, 0];
                int laneB = GameConstants.KEY_LANES[g, 1];
                NoteController note = FindBestNote(laneA, laneB);
                if (note == null) continue;
                if (MusicTime < note.HitTimeSeconds - GameConstants.HIT_WINDOW_PERFECT) continue;

                bool isLong = note.IsLong;
                OnGroupPressed(g);
                if (!isLong) OnGroupReleased(g); // タップは即リリース
            }
        }
    }

    void UpdateCameraBob()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float t = Time.time;
        cam.transform.position = new Vector3(
            Mathf.Sin(t * 0.7f) * 0.03f,
            5.5f + Mathf.Sin(t * 1.1f) * 0.02f,
            9f);
        cam.transform.LookAt(new Vector3(0f, 0f, -8f));
    }

    void SpawnDueNotes()
    {
        while (_spawnIndex < _chart.notes.Count)
        {
            var n = _chart.notes[_spawnIndex];
            float hitSec = n.t / 1000f;

            if (MusicTime < hitSec - GameConstants.TRAVEL_TIME) break;

            SpawnNote(n);
            _spawnIndex++;
        }
    }

    void SpawnNote(ChartNote n)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Note";
        Destroy(go.GetComponent<Collider>());

        var ctrl = go.AddComponent<NoteController>();
        ctrl.Init(n.t / 1000f, n.lanes, n.isLong, n.holdMs / 1000f, this);

        foreach (int lane in n.lanes)
            _laneNotes[lane].Add(ctrl);
    }

    // ---------------------------------------------------------------
    void OnGroupPressed(int group)
    {
        _keyHeld[group] = true;
        _highway.SetGroupGlow(group, true); // レーングロー（ゲーム外でも光る）

        if (!_isPlaying) return;

        TryHit(group);
    }

    void OnGroupReleased(int group)
    {
        _keyHeld[group] = false;
        _highway.SetGroupGlow(group, false);

        if (_holdNote[group] != null)
        {
            _holdNote[group].OnHoldReleased();
            _holdNote[group] = null;
            _holdTick[group] = 0f;
        }
    }

    void TryHit(int group)
    {
        if (_holdNote[group] != null) return; // 既にホールド中

        int laneA = GameConstants.KEY_LANES[group, 0];
        int laneB = GameConstants.KEY_LANES[group, 1];
        NoteController best = FindBestNote(laneA, laneB);
        if (best == null) return;

        float err = Mathf.Abs(MusicTime - best.HitTimeSeconds);
        Judgment j = err <= GameConstants.HIT_WINDOW_PERFECT ? Judgment.Perfect : Judgment.Good;

        RegisterJudgment(j);
        best.OnHit();
        _highway.FlashLight(group);
        RemoveFromLanes(best);

        if (best.IsLong)
        {
            _holdNote[group] = best;
            _holdTick[group] = 0f;
        }
    }

    // キーを押し続けている間、ウィンドウに入ったロングノーツを自動キャッチ
    void CheckHeldKeysForLongNotes()
    {
        for (int g = 0; g < 6; g++)
        {
            if (!_keyHeld[g] || _holdNote[g] != null) continue;
            int laneA = GameConstants.KEY_LANES[g, 0];
            int laneB = GameConstants.KEY_LANES[g, 1];
            NoteController note = FindBestNote(laneA, laneB);
            if (note == null || !note.IsLong) continue;
            TryHit(g);
        }
    }

    // ホールド中は100msごとにスコアティック（HTML版準拠）
    void UpdateHoldTicks()
    {
        for (int g = 0; g < 6; g++)
        {
            var note = _holdNote[g];
            if (note == null) { _holdTick[g] = 0f; continue; }

            _holdTick[g] += Time.deltaTime;
            if (_holdTick[g] >= 0.1f)
            {
                _holdTick[g] -= 0.1f;
                _score += 10 * (1 + _combo / 4);
            }
        }
    }

    NoteController FindBestNote(int laneA, int laneB)
    {
        NoteController best = null;
        float bestErr = float.MaxValue;

        foreach (var note in Candidates(laneA, laneB))
        {
            if (note.IsHit || note.IsMissed) continue;
            float err = Mathf.Abs(MusicTime - note.HitTimeSeconds);
            if (err <= GameConstants.HIT_WINDOW_GOOD && err < bestErr)
            {
                bestErr = err;
                best = note;
            }
        }
        return best;
    }

    // Deduplicated notes across two lanes
    IEnumerable<NoteController> Candidates(int laneA, int laneB)
    {
        var seen = new HashSet<NoteController>();
        foreach (var n in _laneNotes[laneA]) if (seen.Add(n)) yield return n;
        foreach (var n in _laneNotes[laneB]) if (seen.Add(n)) yield return n;
    }

    public void OnNoteMissed(NoteController note)
    {
        RegisterJudgment(Judgment.Miss);
        RemoveFromLanes(note);
    }

    void RemoveFromLanes(NoteController note)
    {
        foreach (int lane in note.Lanes)
            _laneNotes[lane].Remove(note);
    }

    // ---------------------------------------------------------------
    void RegisterJudgment(Judgment j)
    {
        // HTML版のスコア式: base * (1 + floor(combo/4))
        switch (j)
        {
            case Judgment.Perfect:
                _perfect++;
                _combo++;
                _score += 100 * (1 + _combo / 4);
                break;
            case Judgment.Good:
                _good++;
                _combo++;
                _score += 50 * (1 + _combo / 4);
                break;
            case Judgment.Miss:
                _miss++;
                _combo = 0;
                break;
        }
        _judgmentText = j.ToString().ToUpper();
        _judgmentTimer = 0.6f;
    }

    void UpdateJudgmentDisplay()
    {
        if (_judgmentTimer > 0f)
            _judgmentTimer -= Time.deltaTime;
    }

    // ---------------------------------------------------------------
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(10, 10, 400, 40), $"SCORE  {_score:N0}", style);
        GUI.Label(new Rect(10, 50, 400, 40), $"COMBO  {_combo}", style);

        // オートプレイ トグル
        var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        btnStyle.normal.textColor = autoPlay ? Color.cyan : Color.white;
        if (GUI.Button(new Rect(Screen.width - 190, 10, 180, 38), autoPlay ? "AUTO: ON" : "AUTO: OFF", btnStyle))
            autoPlay = !autoPlay;

        if (_judgmentTimer > 0f)
        {
            style.fontSize = 42;
            style.normal.textColor = _judgmentText == "PERFECT" ? Color.yellow
                                   : _judgmentText == "GOOD"    ? Color.cyan
                                   : Color.red;
            GUI.Label(new Rect(Screen.width * 0.5f - 120, Screen.height * 0.5f - 40, 300, 60),
                      _judgmentText, style);
        }

        style.fontSize = 18;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(10, Screen.height - 55, 600, 28),
                  $"PERFECT {_perfect}  GOOD {_good}  MISS {_miss}  [A/S/D/J/K/L]", style);

        // 常時デバッグ行
        style.fontSize = 15;
        style.normal.textColor = new Color(1f, 1f, 0.4f);
        int noteCount = _chart?.notes?.Count ?? 0;
        GUI.Label(new Rect(10, Screen.height - 28, 700, 24),
                  $"chart={chartId}  playing={_isPlaying}  notes={noteCount}  time={MusicTime:F2}  spawn={_spawnIndex}", style);

        if (!string.IsNullOrEmpty(_statusMsg))
        {
            style.fontSize = 26;
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(10, Screen.height * 0.45f, 700, 80), _statusMsg, style);
        }
    }

    // ---------------------------------------------------------------
    static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Force perspective — SampleScene defaults to Orthographic in 2D templates
        cam.orthographic = false;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 150f; // highway extends ~104 units from camera

        cam.transform.position = new Vector3(0f, 5.5f, 9f);
        cam.transform.LookAt(new Vector3(0f, 0f, -8f));

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.1f);
        RenderSettings.ambientLight = new Color(0.1f, 0.1f, 0.2f);
    }
}
