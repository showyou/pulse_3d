using System;
using System.Collections.Generic;

[Serializable]
public class ChartNote
{
    public int t;        // hit time in milliseconds
    public int[] lanes;
    public bool isLong;
    public int holdMs;
}

[Serializable]
public class ChartMeta
{
    public string title;
    public float bpm;
    public int duration;
    public string audioFile; // StreamingAssets/songs/ 以下のファイル名。例: "demo.ogg"
    public string videoFile; // 背景動画。例: "demo.mp4"（省略可）
}

[Serializable]
public class ChartData
{
    public string format;
    public int version;
    public ChartMeta meta;
    public List<ChartNote> notes;
}

[Serializable]
public class SongMetadata
{
    public string id;
    public string title;
    public float bpm;
    public float duration;
    public string mode;
    public string chart;
}

// JsonUtility cannot parse top-level arrays, so metadata.json uses {"songs":[...]}
[Serializable]
public class SongMetadataList
{
    public SongMetadata[] songs;
}

// HTML版 extract_notes.py が出力する fumen.json フォーマット
[Serializable]
public class FumenNote
{
    public float time;      // ヒット時刻（秒）
    public float duration;  // ノート長（秒）、0 ならタップ
    public int lane;        // 0-15（C4=0, D#5=15）
    public float amplitude;
}

[Serializable]
public class FumenRoot
{
    public string title;
    public string audioFile;
    public string videoFile;
    public float totalDuration;
    public List<FumenNote> notes;
}
