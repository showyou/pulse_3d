# PULSE 3D — 譜面・楽曲データ仕様

## ファイル配置

```
StreamingAssets/
├── songs/
│   ├── metadata.json       # 楽曲リスト（git管轄外）
│   ├── *.ogg / *.mp3       # 音声ファイル
│   └── *.mp4               # 背景動画ファイル（省略可）
└── charts/
    └── *.json              # 譜面ファイル
```

---

## metadata.json — 楽曲リスト

楽曲選択画面に表示される曲の一覧。git管轄外のため各環境でローカル管理する。

```json
{
  "songs": [
    {
      "id":       "starmine",
      "title":    "Star-mine",
      "bpm":      136.0,
      "duration": 236100,
      "mode":     "rhythm",
      "chart":    "charts/starmine.json"
    }
  ]
}
```

| フィールド | 型 | 説明 |
|---|---|---|
| `id` | string | 曲の一意識別子。採譜ファイルの命名にも使われる（例: `starmine_001.json`） |
| `title` | string | 表示タイトル |
| `bpm` | float | BPM（表示用。ゲームロジックでは使用しない） |
| `duration` | int | 曲の長さ（ミリ秒）。この時間に達するとフェードアウトしてリザルト画面へ |
| `mode` | string | 現在は `"rhythm"` 固定 |
| `chart` | string | 譜面ファイルへのパス（`StreamingAssets/` 起点の相対パス） |

---

## 譜面JSON — pulse3d_v1 フォーマット

```json
{
  "format":  "pulse3d_v1",
  "version": 1,
  "meta": {
    "title":     "Star-mine",
    "bpm":       136.0,
    "duration":  236100,
    "audioFile": "starmine.ogg",
    "videoFile": "starmine.mp4"
  },
  "notes": [
    { "t": 1000, "lanes": [0, 1], "isLong": false, "holdMs": 0, "slideEndGroup": -1, "isHeld": false },
    { "t": 2000, "lanes": [4, 5], "isLong": true,  "holdMs": 800, "slideEndGroup": -1, "isHeld": false }
  ]
}
```

### meta オブジェクト

| フィールド | 型 | 説明 |
|---|---|---|
| `title` | string | 曲タイトル |
| `bpm` | float | BPM（表示用） |
| `duration` | int | 曲の長さ（ミリ秒） |
| `audioFile` | string | 音声ファイル名（`StreamingAssets/songs/` 以下）。空文字なら無音 |
| `videoFile` | string | 背景動画ファイル名（`StreamingAssets/songs/` 以下）。省略可 |

> **音声と動画の優先順位:** `audioFile` と `videoFile` が両方指定された場合、音声は `audioFile` から再生し、動画の音声トラックはミュートされる。`audioFile` が空で `videoFile` のみの場合は動画の音声をそのまま使用する。

### note オブジェクト

| フィールド | 型 | 省略時デフォルト | 説明 |
|---|---|---|---|
| `t` | int | 必須 | ヒット時刻（ミリ秒） |
| `lanes` | int[] | 必須 | 占有レーン番号の配列（下記レーン表参照） |
| `isLong` | bool | false | ロングノーツかどうか |
| `holdMs` | int | 0 | ロングノーツの押し続け時間（ミリ秒）。`isLong: true` のときのみ有効 |
| `slideEndGroup` | int | -1 | スライドノーツの終点グループ（0〜5）。`-1` は通常ノーツ。`slidePoints` が有効な場合は無視される |
| `isHeld` | bool | false | 押しっぱなしノーツかどうか（下記参照） |
| `slidePoints` | SlidePoint[] | null | マルチウェイポイントスライドの経路（2点以上で有効） |

#### SlidePoint オブジェクト

| フィールド | 型 | 説明 |
|---|---|---|
| `offsetMs` | int | ヒット時刻からの相対時間（ミリ秒）。先頭は通常 `0` |
| `group` | int | 通過グループ番号（0〜5） |

---

## ノーツ種別

### タップノーツ（通常）
```json
{ "t": 1000, "lanes": [0, 1], "isLong": false, "holdMs": 0, "slideEndGroup": -1, "isHeld": false }
```
- 色: ピンク
- 対応グループのキーを叩く

### ロングノーツ
```json
{ "t": 2000, "lanes": [0, 1], "isLong": true, "holdMs": 800, "slideEndGroup": -1, "isHeld": false }
```
- 色: 水色
- 叩いてから `holdMs` ミリ秒間押し続ける
- ヒット窓を逃してもボディが残っている間は途中から押してもOK（Good判定）

### スライドノーツ（タップ）
```json
{ "t": 3000, "lanes": [0, 1], "isLong": false, "holdMs": 0, "slideEndGroup": 3, "isHeld": false }
```
- 色: オレンジ
- `slideEndGroup` で指定したグループを500ms以内に押すことで成立
- 始点と終点を異なるキーグループで配置する

### スライドロングノーツ
```json
{ "t": 3000, "lanes": [0, 1], "isLong": true, "holdMs": 1200, "slideEndGroup": 3, "isHeld": false }
```
- 色: ティール（緑寄りの水色）
- `isLong: true` かつ `slideEndGroup >= 0` の組み合わせ
- 始点グループを押してホールドしながら、終点グループへレーンが移動するロングノーツ
- ボディが斜めに描画され、ヘッドが `holdMs` をかけて終点X座標に移動する
- 判定は始点グループでのホールド継続（終点グループの押下は不要）

### マルチウェイポイントスライドノーツ
```json
{
  "t": 3000, "lanes": [0, 1], "isLong": true, "holdMs": 0, "slideEndGroup": -1, "isHeld": false,
  "slidePoints": [
    { "offsetMs": 0,    "group": 0 },
    { "offsetMs": 800,  "group": 3 },
    { "offsetMs": 1600, "group": 1 }
  ]
}
```
- 色: ティール（緑寄りの水色）
- `slidePoints` に **2点以上** を指定すると有効になる（`slideEndGroup` は無視される）
- 各ウェイポイントはヒット時刻からの相対時間 `offsetMs` と移動先グループ `group` で表す
- 先頭の `slidePoints[0]` はヒット時刻・始点グループ（通常 `offsetMs:0` で始点レーンのグループ）
- 末尾の `offsetMs` が `holdMs` の代わりとなり、ボディ全体の持続時間を決定する
- ヘッドはウェイポイント間を線形補間しながら水平移動する（蛇行・折り返しが可能）
- ボディは区間ごとに斜めセグメントで描画される
- 判定は始点グループでのホールド継続のみ（中間・終点グループの押下は不要）

### 押しっぱなしノーツ（isHeld）
```json
{ "t": 4000, "lanes": [2, 3], "isLong": false, "holdMs": 0, "slideEndGroup": -1, "isHeld": true }
```
- 色: 黄色
- ノーツがヒットラインに到達した時点で対応グループを押しっぱなしにしていれば自動でPerfect判定

---

## レーン・グループ対応表

レーンは左から右へ 0〜11 の12レーン。2レーンで1グループを構成し、キー1つに対応する。

| グループ | キー | レーン | 色 |
|---|---|---|---|
| 0 | A | 0, 1 | シアン |
| 1 | S | 2, 3 | ブルー |
| 2 | D | 4, 5 | パープル |
| 3 | J | 6, 7 | ピンク |
| 4 | K | 8, 9 | オレンジ |
| 5 | L | 10, 11 | グリーン |

`lanes` には任意のレーン番号を複数指定でき、複数グループにまたがるワイドノーツも可能。

---

## 判定窓

| 判定 | 時間（片側） |
|---|---|
| Perfect | `HIT_WINDOW_PERFECT`（`GameConstants.cs` 参照） |
| Good | `HIT_WINDOW_GOOD`（`GameConstants.cs` 参照） |
| Miss | Good窓を超えた場合（ロングノーツはボディ終了まで猶予あり） |

---

## 採譜ファイルの命名規則

採譜モード（EDIT: ON）で保存されるファイルは `charts/{id}_001.json` から始まる連番形式。既存ファイルは上書きされない。

```
charts/starmine_001.json
charts/starmine_002.json
...
```
