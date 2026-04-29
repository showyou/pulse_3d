using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.Video;

public enum Judgment { Perfect, Good, Miss }

enum GameState { Select, Loading, Playing, Result }

public class RhythmGameManager : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Autoplay")]
    public bool autoPlay = false;

    // ---------------------------------------------------------------
    // State
    GameState _state = GameState.Select;

    // Song selection
    SongMetadataList _songList;
    int              _selectedIndex;
    SongMetadata     _currentSong;

    // Runtime
    public float MusicTime { get; private set; }

    int   _score, _combo, _maxCombo;
    int   _perfect, _good, _miss;
    bool  _isPlaying;
    double _startDspTime;

    ChartData _chart;
    int       _spawnIndex;

    readonly List<NoteController>[] _laneNotes = new List<NoteController>[GameConstants.NUM_LANES];
    readonly NoteController[]       _holdNote  = new NoteController[6];
    readonly bool[]                 _keyHeld   = new bool[6];
    readonly float[]                _holdTick  = new float[6];

    InputHandler   _input;
    HighwayBuilder _highway;
    VideoPlayer    _videoPlayer;

    // Note pool (#4)
    readonly Queue<NoteController> _notePool = new Queue<NoteController>();

    // UI
    string _judgmentText  = "";
    float  _judgmentTimer;
    string _statusMsg     = "";

    // Group colors — mirrors HighwayBuilder.GroupColors
    static readonly Color[] GroupColors = {
        new Color(0.0f, 1.0f, 1.0f),
        new Color(0.2f, 0.4f, 1.0f),
        new Color(0.6f, 0.2f, 1.0f),
        new Color(1.0f, 0.3f, 0.6f),
        new Color(1.0f, 0.55f, 0.1f),
        new Color(0.2f, 1.0f, 0.4f),
    };
    static readonly string[] GroupLabels = { "A", "S", "D", "J", "K", "L" };

    // ---------------------------------------------------------------
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

        LoadMetadata();
    }

    // ---------------------------------------------------------------
    // Metadata / Song selection (#10)
    void LoadMetadata()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "songs", "metadata.json");
        if (File.Exists(path))
            _songList = JsonUtility.FromJson<SongMetadataList>(File.ReadAllText(path));
        if (_songList?.songs == null || _songList.songs.Length == 0)
            _songList = new SongMetadataList { songs = new SongMetadata[0] };
        _state = GameState.Select;
    }

    void StartLoadingSong(SongMetadata song)
    {
        _currentSong = song;
        _state       = GameState.Loading;
        _statusMsg   = "Loading...";
        StartCoroutine(LoadSongCoroutine(song));
    }

    IEnumerator LoadSongCoroutine(SongMetadata song)
    {
        string chartPath = Path.Combine(Application.streamingAssetsPath, song.chart);
        if (!File.Exists(chartPath))
        {
            _statusMsg = $"Chart not found: {song.chart}";
            _state     = GameState.Select;
            yield break;
        }

        _chart = ParseChart(File.ReadAllText(chartPath));
        if (_chart == null || _chart.notes == null || _chart.notes.Count == 0)
        {
            _statusMsg = "Chart parse failed.";
            _state     = GameState.Select;
            yield break;
        }
        _chart.notes.Sort((a, b) => a.t.CompareTo(b.t));

        // Load audio
        string audioFile = _chart.meta?.audioFile ?? "";
        if (!string.IsNullOrEmpty(audioFile))
        {
            string audioPath = new Uri(Path.Combine(Application.streamingAssetsPath, "songs", audioFile)).AbsoluteUri;
            _statusMsg = "Loading audio...";
            var audioType = Path.GetExtension(audioFile).ToLower() switch {
                ".ogg"  => AudioType.OGGVORBIS,
                ".mp3"  => AudioType.MPEG,
                ".wav"  => AudioType.WAV,
                _       => AudioType.OGGVORBIS,
            };
            var handler = new DownloadHandlerAudioClip(audioPath, audioType);
            handler.streamAudio = false;
            using var req = new UnityWebRequest(audioPath) { downloadHandler = handler };
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
                audioSource.clip = DownloadHandlerAudioClip.GetContent(req);
            else
                Debug.LogWarning($"[PULSE] Audio load failed: {req.error}");
        }
        else
        {
            audioSource.clip = null;
        }

        SetupBackgroundVideo(_chart.meta?.videoFile ?? "");
        _statusMsg = "";
        StartGame();
    }

    // ---------------------------------------------------------------
    // Chart parsing (#6)
    ChartData ParseChart(string json)
    {
        // Strip BOM and any leading non-JSON text (e.g. accidental ```json markers)
        json = json.TrimStart('\xEF', '\xBB', '\xBF');
        int brace = json.IndexOf('{');
        if (brace > 0) json = json.Substring(brace);
        json = json.Trim();

        try
        {
            var data = JsonUtility.FromJson<ChartData>(json);
            if (data?.format == "pulse3d_v1" && data.notes?.Count > 0)
                return data;
        }
        catch (Exception e) { Debug.LogWarning($"[PULSE] ChartData parse: {e.Message}"); }

        FumenRoot fumen = null;
        try { fumen = JsonUtility.FromJson<FumenRoot>(json); }
        catch (Exception e) { Debug.LogWarning($"[PULSE] FumenRoot parse: {e.Message}"); return null; }

        if (fumen?.notes == null || fumen.notes.Count == 0) return null;

        var chart  = new ChartData();
        chart.meta = new ChartMeta {
            title     = fumen.title ?? "",
            duration  = (int)(fumen.totalDuration * 1000),
            audioFile = fumen.audioFile ?? "",
            videoFile = fumen.videoFile ?? "",
        };
        chart.notes = new List<ChartNote>(fumen.notes.Count);
        foreach (var n in fumen.notes)
        {
            bool isLong    = n.duration > 0.05f;
            int  mappedLane = n.lane % GameConstants.NUM_LANES;
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

    // ---------------------------------------------------------------
    // Game flow
    void StartGame()
    {
        // Destroy all active notes still in lanes (not yet pooled)
        var seen = new HashSet<NoteController>();
        for (int i = 0; i < GameConstants.NUM_LANES; i++)
        {
            foreach (var n in _laneNotes[i])
                if (n != null && seen.Add(n)) Destroy(n.gameObject);
            _laneNotes[i].Clear();
        }

        // Clear pool leftovers from previous run
        foreach (var n in _notePool)
            if (n != null) Destroy(n.gameObject);
        _notePool.Clear();
        for (int g = 0; g < 6; g++) { _holdNote[g] = null; _keyHeld[g] = false; _holdTick[g] = 0f; }

        _score = _combo = _maxCombo = _perfect = _good = _miss = 0;
        _spawnIndex    = 0;
        _judgmentText  = "";
        _judgmentTimer = 0f;

        _startDspTime = AudioSettings.dspTime + 1.0;
        if (audioSource.clip != null)
            audioSource.PlayScheduled(_startDspTime);

        _isPlaying = true;
        _state     = GameState.Playing;

        if (_videoPlayer != null)
            StartCoroutine(PlayVideoWhenReady());

        Debug.Log($"[PULSE] StartGame: {_chart.meta.title}  notes={_chart.notes.Count}");
    }

    void EndGame()
    {
        _isPlaying = false;
        _state     = GameState.Result;
        if (_videoPlayer != null) _videoPlayer.Stop();
    }

    void RetrySong()
    {
        if (_currentSong != null)
            StartLoadingSong(_currentSong);
    }

    void ReturnToSelect()
    {
        if (_videoPlayer != null) { Destroy(_videoPlayer.gameObject); _videoPlayer = null; }
        audioSource.Stop();
        audioSource.clip = null;
        _state = GameState.Select;
    }

    // ---------------------------------------------------------------
    // Video (#12)
    void SetupBackgroundVideo(string videoFile)
    {
        if (_videoPlayer != null) { Destroy(_videoPlayer.gameObject); _videoPlayer = null; }
        if (string.IsNullOrEmpty(videoFile)) return;

        string path = "file://" + Path.Combine(Application.streamingAssetsPath, "songs", videoFile);
        var go = new GameObject("BackgroundVideo");
        _videoPlayer = go.AddComponent<VideoPlayer>();
        _videoPlayer.renderMode      = VideoRenderMode.CameraFarPlane;
        _videoPlayer.targetCamera    = Camera.main;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        _videoPlayer.isLooping       = true;
        _videoPlayer.playOnAwake     = false;
        _videoPlayer.url             = path;
        _videoPlayer.Prepare();
    }

    IEnumerator PlayVideoWhenReady()
    {
        yield return new WaitUntil(() => _videoPlayer.isPrepared);
        double lag = AudioSettings.dspTime - _startDspTime;
        if (lag > 0) _videoPlayer.time = lag;
        _videoPlayer.Play();
    }

    // ---------------------------------------------------------------
    // Update
    void Update()
    {
        if (_state == GameState.Select)
        {
            HandleSelectInput();
            return;
        }
        if (_state == GameState.Result)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[Key.R].wasPressedThisFrame) RetrySong();
                if (kb[Key.Escape].wasPressedThisFrame) ReturnToSelect();
            }
            return;
        }
        if (_state != GameState.Playing || !_isPlaying || _chart == null) return;

        MusicTime = (float)(AudioSettings.dspTime - _startDspTime);

        SpawnDueNotes();
        if (autoPlay) AutoPlayUpdate();
        CheckHeldKeysForLongNotes();
        UpdateHoldTicks();
        UpdateJudgmentDisplay();
        UpdateCameraBob();

        // Song end detection (#8)
        float endTime = _chart.meta.duration > 0
            ? _chart.meta.duration / 1000f + 1.5f
            : (_chart.notes.Count > 0 ? _chart.notes[_chart.notes.Count - 1].t / 1000f + 3f : 10f);
        if (MusicTime >= endTime && _spawnIndex >= _chart.notes.Count)
            EndGame();
    }

    void HandleSelectInput()
    {
        if (_songList == null || _songList.songs.Length == 0) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[Key.UpArrow].wasPressedThisFrame)
            _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
        if (kb[Key.DownArrow].wasPressedThisFrame)
            _selectedIndex = Mathf.Min(_songList.songs.Length - 1, _selectedIndex + 1);
        if (kb[Key.Enter].wasPressedThisFrame || kb[Key.Space].wasPressedThisFrame)
            StartLoadingSong(_songList.songs[_selectedIndex]);
    }

    // ---------------------------------------------------------------
    // Note pool (#4)
    void SpawnNote(ChartNote n)
    {
        NoteController ctrl;
        if (_notePool.Count > 0)
        {
            ctrl = _notePool.Dequeue();
            ctrl.gameObject.SetActive(true);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Note";
            Destroy(go.GetComponent<Collider>());
            ctrl = go.AddComponent<NoteController>();
        }
        ctrl.Init(n.t / 1000f, n.lanes, n.isLong, n.holdMs / 1000f, this, ReturnNote);
        foreach (int lane in n.lanes)
            _laneNotes[lane].Add(ctrl);
    }

    void ReturnNote(NoteController n) => _notePool.Enqueue(n);

    // ---------------------------------------------------------------
    // Autoplay (#9)
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
                var note = FindBestNote(GameConstants.KEY_LANES[g, 0], GameConstants.KEY_LANES[g, 1]);
                if (note == null || MusicTime < note.HitTimeSeconds - GameConstants.HIT_WINDOW_PERFECT) continue;
                bool isLong = note.IsLong;
                OnGroupPressed(g);
                if (!isLong) OnGroupReleased(g);
            }
        }
    }

    // ---------------------------------------------------------------
    void SpawnDueNotes()
    {
        while (_spawnIndex < _chart.notes.Count)
        {
            var n = _chart.notes[_spawnIndex];
            if (MusicTime < n.t / 1000f - GameConstants.TRAVEL_TIME) break;
            SpawnNote(n);
            _spawnIndex++;
        }
    }

    void OnGroupPressed(int group)
    {
        _keyHeld[group] = true;
        _highway.SetGroupGlow(group, true);
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
        if (_holdNote[group] != null) return;
        var best = FindBestNote(GameConstants.KEY_LANES[group, 0], GameConstants.KEY_LANES[group, 1]);
        if (best == null) return;

        float err = Mathf.Abs(MusicTime - best.HitTimeSeconds);
        var j = err <= GameConstants.HIT_WINDOW_PERFECT ? Judgment.Perfect : Judgment.Good;

        RegisterJudgment(j);
        best.OnHit();
        _highway.FlashLight(group);
        RemoveFromLanes(best);

        if (best.IsLong) { _holdNote[group] = best; _holdTick[group] = 0f; }
    }

    void CheckHeldKeysForLongNotes()
    {
        for (int g = 0; g < 6; g++)
        {
            if (!_keyHeld[g] || _holdNote[g] != null) continue;
            var note = FindBestNote(GameConstants.KEY_LANES[g, 0], GameConstants.KEY_LANES[g, 1]);
            if (note == null || !note.IsLong) continue;
            TryHit(g);
        }
    }

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
            { bestErr = err; best = note; }
        }
        return best;
    }

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

    public void OnNoteExpired(NoteController note) => RemoveFromLanes(note);

    void RemoveFromLanes(NoteController note)
    {
        if (note.Lanes == null) return;
        foreach (int lane in note.Lanes)
            _laneNotes[lane].Remove(note);
    }

    void RegisterJudgment(Judgment j)
    {
        switch (j)
        {
            case Judgment.Perfect:
                _perfect++; _combo++;
                _score += 100 * (1 + _combo / 4);
                break;
            case Judgment.Good:
                _good++; _combo++;
                _score += 50 * (1 + _combo / 4);
                break;
            case Judgment.Miss:
                _miss++; _combo = 0;
                break;
        }
        if (_combo > _maxCombo) _maxCombo = _combo;
        _judgmentText  = j.ToString().ToUpper();
        _judgmentTimer = 0.6f;
    }

    void UpdateJudgmentDisplay()
    {
        if (_judgmentTimer > 0f) _judgmentTimer -= Time.deltaTime;
    }

    void UpdateCameraBob()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float t = Time.time;
        cam.transform.position = new Vector3(
            Mathf.Sin(t * 0.7f) * 0.03f,
            5.5f + Mathf.Sin(t * 1.1f) * 0.02f, 9f);
        cam.transform.LookAt(new Vector3(0f, 0f, -8f));
    }

    // ---------------------------------------------------------------
    // GUI (#8, #10, #11)
    void OnGUI()
    {
        switch (_state)
        {
            case GameState.Select:  DrawSelectScreen();  break;
            case GameState.Loading: DrawLoadingScreen(); break;
            case GameState.Playing: DrawPlayingUI();     break;
            case GameState.Result:  DrawResultScreen();  break;
        }
    }

    // -- Select screen (#10, #11) --
    void DrawSelectScreen()
    {
        DrawDarkOverlay(0.75f);

        var titleStyle = LabelStyle(52, FontStyle.Bold, new Color(0.35f, 0.75f, 1f));
        titleStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, 30, Screen.width, 68), "PULSE 3D", titleStyle);

        if (_songList == null || _songList.songs.Length == 0)
        {
            var noSong = LabelStyle(24, FontStyle.Normal, new Color(0.7f, 0.7f, 0.7f));
            noSong.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, Screen.height * 0.5f - 20, Screen.width, 40),
                "No songs found in StreamingAssets/songs/metadata.json", noSong);
            return;
        }

        float itemH  = 56f;
        float listY  = 120f;
        float listX  = Screen.width * 0.12f;

        for (int i = 0; i < _songList.songs.Length; i++)
        {
            var  song     = _songList.songs[i];
            bool selected = i == _selectedIndex;
            float y = listY + i * itemH;

            if (selected)
            {
                GUI.color = new Color(0.2f, 0.45f, 0.7f, 0.5f);
                GUI.DrawTexture(new Rect(listX - 10, y + 4, Screen.width * 0.78f, itemH - 8), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            Color nameCol  = selected ? Color.white : new Color(0.75f, 0.75f, 0.75f);
            Color infoCol  = selected ? new Color(0.6f, 0.85f, 1f) : new Color(0.4f, 0.55f, 0.7f);
            string prefix  = selected ? "▶  " : "    ";

            GUI.Label(new Rect(listX, y + 8, Screen.width * 0.5f, itemH),
                prefix + song.title, LabelStyle(26, FontStyle.Bold, nameCol));

            float durSec = song.duration / 1000f;
            string info  = $"{song.bpm:0} BPM  {(int)(durSec / 60)}:{(int)(durSec % 60):D2}";
            GUI.Label(new Rect(Screen.width * 0.65f, y + 12, Screen.width * 0.25f, itemH),
                info, LabelStyle(20, FontStyle.Normal, infoCol));
        }

        var hint = LabelStyle(18, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f));
        hint.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, Screen.height - 50, Screen.width, 36),
            "↑ ↓  Select    ENTER / SPACE  Start", hint);

        if (!string.IsNullOrEmpty(_statusMsg))
            GUI.Label(new Rect(0, Screen.height - 90, Screen.width, 36),
                _statusMsg, LabelStyle(20, FontStyle.Normal, Color.red));
    }

    void DrawLoadingScreen()
    {
        DrawDarkOverlay(0.85f);
        var s = LabelStyle(32, FontStyle.Bold, new Color(0.6f, 0.8f, 1f));
        s.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 50), _statusMsg, s);
    }

    // -- Playing UI (#11) --
    void DrawPlayingUI()
    {
        DrawProgressBar();
        DrawScoreCombo();
        DrawJudgment();
        DrawKeyIndicators();
        DrawAutoButton();
    }

    void DrawProgressBar()
    {
        float dur = _chart?.meta?.duration > 0 ? _chart.meta.duration / 1000f : 1f;
        float p   = Mathf.Clamp01(MusicTime / dur);

        GUI.color = new Color(0.08f, 0.08f, 0.15f, 0.9f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, 6), Texture2D.whiteTexture);
        GUI.color = new Color(0.3f, 0.65f, 1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width * p, 6), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DrawScoreCombo()
    {
        GUI.Label(new Rect(16, 14, 320, 46), $"{_score:N0}",
            LabelStyle(34, FontStyle.Bold, Color.white));
        if (_combo > 1)
            GUI.Label(new Rect(16, 58, 200, 34), $"x{_combo} COMBO",
                LabelStyle(22, FontStyle.Bold, new Color(1f, 0.9f, 0.25f)));
    }

    void DrawJudgment()
    {
        if (_judgmentTimer <= 0f) return;
        float alpha = Mathf.Clamp01(_judgmentTimer / 0.25f);
        Color c = _judgmentText == "PERFECT" ? new Color(1f, 0.95f, 0.2f, alpha)
                : _judgmentText == "GOOD"    ? new Color(0.2f, 0.9f, 1f,  alpha)
                :                              new Color(1f,  0.25f, 0.25f, alpha);
        var s = LabelStyle(50, FontStyle.Bold, c);
        s.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, Screen.height * 0.34f, Screen.width, 64), _judgmentText, s);
    }

    void DrawKeyIndicators()
    {
        const float keyW = 72f, keyH = 48f, gap = 6f;
        float totalW = 6 * keyW + 5 * gap;
        float sx = (Screen.width - totalW) * 0.5f;
        float sy = Screen.height - keyH - 14f;

        for (int g = 0; g < 6; g++)
        {
            float x    = sx + g * (keyW + gap);
            Color c    = GroupColors[g];
            bool  held = _keyHeld[g];

            GUI.color = held ? c : new Color(c.r * 0.22f, c.g * 0.22f, c.b * 0.22f, 0.85f);
            GUI.DrawTexture(new Rect(x, sy, keyW, keyH), Texture2D.whiteTexture);

            var ls = LabelStyle(22, FontStyle.Bold,
                held ? Color.black : new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f));
            ls.alignment = TextAnchor.MiddleCenter;
            GUI.color    = Color.white;
            GUI.Label(new Rect(x, sy, keyW, keyH), GroupLabels[g], ls);
        }
        GUI.color = Color.white;
    }

    void DrawAutoButton()
    {
        var s = LabelStyle(20, FontStyle.Bold, autoPlay ? Color.cyan : new Color(0.6f, 0.6f, 0.6f));
        s.alignment = TextAnchor.MiddleCenter;
        if (GUI.Button(new Rect(Screen.width - 140, 10, 130, 34), autoPlay ? "AUTO: ON" : "AUTO: OFF"))
            autoPlay = !autoPlay;
    }

    // -- Result screen (#8, #11) --
    void DrawResultScreen()
    {
        DrawDarkOverlay(0.88f);

        var titleStyle = LabelStyle(56, FontStyle.Bold, new Color(0.35f, 0.75f, 1f));
        titleStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, 30, Screen.width, 72), "RESULT", titleStyle);

        string songTitle = _currentSong?.title ?? _chart?.meta?.title ?? "";
        var songStyle = LabelStyle(28, FontStyle.Normal, new Color(0.75f, 0.75f, 0.75f));
        songStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, 110, Screen.width, 42), songTitle, songStyle);

        // Rank
        int   total = _perfect + _good + _miss;
        float acc   = total > 0 ? (_perfect * 100f + _good * 50f) / (total * 100f) : 0f;
        string rank = acc >= 0.97f ? "S" : acc >= 0.88f ? "A" : acc >= 0.72f ? "B" : "C";
        Color rankColor = rank == "S" ? new Color(1f, 0.9f, 0.2f)
                        : rank == "A" ? new Color(0.2f, 1f, 0.5f)
                        : rank == "B" ? new Color(0.4f, 0.7f, 1f)
                        :               new Color(0.7f, 0.7f, 0.7f);

        float cx = Screen.width * 0.5f;
        GUI.Label(new Rect(cx - 260, 170, 360, 60),
            $"{_score:N0}", LabelStyle(50, FontStyle.Bold, Color.white));

        var rankStyle = LabelStyle(72, FontStyle.Bold, rankColor);
        rankStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(cx + 110, 158, 140, 80), rank, rankStyle);

        // Stats
        var statStyle = LabelStyle(24, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f));
        statStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, 252, Screen.width, 36),
            $"PERFECT  {_perfect}    GOOD  {_good}    MISS  {_miss}", statStyle);
        GUI.Label(new Rect(0, 286, Screen.width, 36),
            $"MAX COMBO  {_maxCombo}", statStyle);

        // Buttons
        float btnY = Screen.height * 0.62f;
        float btnW = 200f, btnH = 52f;
        if (GUI.Button(new Rect(cx - btnW - 16, btnY, btnW, btnH), "RETRY"))
            RetrySong();
        if (GUI.Button(new Rect(cx + 16, btnY, btnW, btnH), "SELECT"))
            ReturnToSelect();

        var hint = LabelStyle(16, FontStyle.Normal, new Color(0.45f, 0.45f, 0.45f));
        hint.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, btnY + btnH + 10, Screen.width, 28),
            "R: Retry    ESC: Select", hint);
    }

    // ---------------------------------------------------------------
    // Helpers
    static void DrawDarkOverlay(float alpha)
    {
        GUI.color = new Color(0f, 0f, 0.04f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static GUIStyle LabelStyle(int size, FontStyle style, Color color)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style };
        s.normal.textColor = color;
        return s;
    }

    static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        cam.orthographic  = false;
        cam.fieldOfView   = 65f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane  = 150f;
        cam.transform.position = new Vector3(0f, 5.5f, 9f);
        cam.transform.LookAt(new Vector3(0f, 0f, -8f));
        RenderSettings.fog        = true;
        RenderSettings.fogMode    = FogMode.Exponential;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor   = new Color(0.05f, 0.05f, 0.1f);
        RenderSettings.ambientLight = new Color(0.1f, 0.1f, 0.2f);
    }
}
