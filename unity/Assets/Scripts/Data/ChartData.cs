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
