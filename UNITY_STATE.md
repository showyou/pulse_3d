# PULSE 3D Unity版 — 引継ぎ仕様書

最終更新: 2026-04-29
Git branch: `unity`
Remote: `https://github.com/showyou/pulse_3d.git`

---

## 環境

| 項目 | 値 |
|------|-----|
| Unity バージョン | 6000.4.4f1 (Unity 6) |
| レンダリング | Universal Render Pipeline (URP) |
| 入力 | Input System パッケージ |
| WSL 作業ディレクトリ | `/home/yuki/src/pulse_3d` |
| Windows クローン | `C:\Users\yuki\src\pulse_3d` |
| Unity Hub で開くパス | `C:\Users\yuki\src\pulse_3d\unity` |

### WSL → Windows 同期

```bash
# WSLから Windows クローンを最新化
git -C /mnt/c/Users/yuki/src/pulse_3d pull origin unity
```

---

## プロジェクト概要

Three.js 版「PULSE 3D」の Unity 移植。
キーボード（A/S/D/J/K/L）で 6グループ 12レーンのノーツを叩くリズムゲーム。

---

## ファイル構成

```
unity/
├── Assets/
│   ├── Scenes/
│   │   └── SampleScene.unity          # ゲームシーン（GameManager オブジェクト配置済み）
│   ├── Scripts/
│   │   ├── Data/
│   │   │   └── ChartData.cs           # データクラス群（ChartNote, ChartMeta, FumenRoot 等）
│   │   ├── Game/
│   │   │   ├── GameConstants.cs       # 定数（レーン数・速度・判定窓 等）
│   │   │   ├── InputHandler.cs        # キー入力（Input System）
│   │   │   ├── NoteController.cs      # ノーツ挙動・オブジェクトプール対応
│   │   │   └── RhythmGameManager.cs   # ゲーム全体管理（メインスクリプト）
│   │   └── Visual/
│   │       ├── HighwayBuilder.cs      # ハイウェイ・ライト生成
│   │       └── HitLinePulse.cs        # ヒットライン点滅
│   └── StreamingAssets/
│       ├── charts/
│       │   └── demo.json              # デモ譜面（Unity形式）
│       └── songs/
│           └── metadata.json          # 曲リスト（選曲画面で読み込む）
└── ProjectSettings/
    └── ProjectVersion.txt             # Unity 6000.4.4f1
```

---

## 実装済み機能

| 機能 | 詳細 |
|------|------|
| 選曲画面 | 起動時に `metadata.json` を読み込み ↑↓/Enter で選曲 |
| 3D ハイウェイ | 12レーン・グループ色・ヒットライン |
| ノーツ表示 | タップ（黄）・ロング（水色）。オブジェクトプールで GC 抑制 |
| 判定 | PERFECT / GOOD / MISS。ヒット窓は `GameConstants` で管理 |
| スコア | 100/50/0点 × コンボボーナス (1 + combo/4) |
| ロングノーツ | ヒット後ホールド、100ms ティックでスコア加算 |
| オートプレイ | 右上ボタンまたは Inspector で ON/OFF |
| BGM | `meta.audioFile` に指定、UnityWebRequest で非同期ロード |
| 背景動画 | `meta.videoFile` に指定、VideoPlayer (CameraFarPlane) で再生 |
| リザルト画面 | スコア・ランク(S/A/B/C)・PERFECT/GOOD/MISS/MAX COMBO 表示 |
| UI | プログレスバー・キーインジケーター・ジャッジメントフェード |
| fumen.json 対応 | Web版 `extract_notes.py` 出力をそのまま charts/ に置ける |

---

## 曲の追加方法

### 1. ファイルを配置

```
StreamingAssets/
├── charts/
│   └── mysong.json          # 譜面（Unity形式 or Web版 fumen.json）
└── songs/
    ├── mysong.ogg            # BGM（ogg / wav / mp3）
    └── mysong.mp4            # 背景動画（任意）
```

> m4a / mp4 を BGM に使いたい場合は ffmpeg で変換:
> ```bash
> ffmpeg -i mysong.m4a mysong.ogg
> ffmpeg -i mysong.mp4 -vn mysong.ogg   # 映像から音声のみ抽出
> ```

### 2. metadata.json を編集

```json
{
  "songs": [
    {
      "id":       "mysong",
      "title":    "My Song",
      "bpm":      128.0,
      "duration": 180000,
      "mode":     "rhythm",
      "chart":    "charts/mysong.json"
    }
  ]
}
```

`duration` はミリ秒単位。

### 3. 譜面 JSON フォーマット

**Unity形式（推奨）:**
```json
{
  "format": "pulse3d_v1",
  "version": 1,
  "meta": {
    "title": "My Song",
    "bpm": 128.0,
    "duration": 180000,
    "audioFile": "mysong.ogg",
    "videoFile": "mysong.mp4"
  },
  "notes": [
    { "t": 1000, "lanes": [0, 1], "isLong": false, "holdMs": 0 },
    { "t": 2000, "lanes": [4, 5], "isLong": true,  "holdMs": 500 }
  ]
}
```

**Web版 fumen.json（そのまま使用可）:**
```json
{
  "title": "My Song",
  "totalDuration": 180.0,
  "notes": [
    { "time": 1.0, "duration": 0.0, "lane": 4, "amplitude": 0.7 }
  ]
}
```
- `lane` 0-15 → Unity の 0-11 に `% 12` でマッピング
- `duration > 0.05` でロングノーツ扱い

---

## キーマッピング

| キー | グループ | レーン |
|------|---------|--------|
| A | 0 (Cyan)   | 0, 1  |
| S | 1 (Blue)   | 2, 3  |
| D | 2 (Purple) | 4, 5  |
| J | 3 (Pink)   | 6, 7  |
| K | 4 (Orange) | 8, 9  |
| L | 5 (Green)  | 10, 11 |

---

## GameConstants 主要値

```csharp
NUM_LANES    = 12
LANE_SPACING = 0.85f
NOTE_Z_SPAWN = -95f
NOTE_Z_HIT   = 4.2f
NOTE_Z_DEAD  = 9.0f
NOTE_SPEED   = 36f          // TRAVEL_TIME ≈ 2.76s
HIT_WINDOW_PERFECT ≈ 0.061s
HIT_WINDOW_GOOD    ≈ 0.117s
```

---

## RhythmGameManager — 状態遷移

```
起動
  └─ LoadMetadata()
       └─ GameState.Select（選曲画面）
            └─ StartLoadingSong()
                 └─ GameState.Loading
                      └─ StartGame()
                           └─ GameState.Playing
                                └─ EndGame()
                                     └─ GameState.Result
                                          ├─ RetrySong()  → Loading
                                          └─ ReturnToSelect() → Select
```

---

## 残課題・TODO

現時点で未実装の機能（GitHub Issues は全クローズ済み）:

- **ロングノーツの MISS 判定**: キーを離しすぎたタイミングのペナルティなし
- **Beat quantization**: fumen.json のタイミング精度（basic-pitch のズレ）
- **MIDIデバイス対応**
- **SE（ヒット音）**: 現在は BGM のみ、打鍵音なし
- **モバイル / ゲームパッド入力**

---

## Web版（Three.js）との対応表

| 要素 | Web版パス | Unity版 |
|------|-----------|---------|
| プレイヤー | `PULSE3D_play_auto.html` | `SampleScene.unity` |
| 譜面生成 | `extract_notes.py` | 出力をそのまま使用可 |
| 定数 | HTML内 JS定数 | `GameConstants.cs` |
| レーン色 | `LANE_COLORS_HEX` | `HighwayBuilder.GroupColors` |
| スコア式 | `base * (1 + floor(combo/4))` | 同一 |
| オートプレイ | `startAutoplay()` | `AutoPlayUpdate()` |
