# PULSE 3D — version2 仕様書

## 概要

ブラウザで動作する3Dリズムゲーム。ビルドツール不要のピュアES Modulesで構成。Three.js r128をCDNからグローバルロード。

---

## ファイル構成

```
/
├── index.html                  # モード選択画面
├── piano.html                  # ピアノモード
├── rhythm.html                 # リズムモード（6ボタン12レーン）
│
├── js/
│   ├── core/
│   │   ├── renderer.js         # Three.js レンダラー・シーン・カメラ・リサイズ
│   │   ├── highway.js          # ハイウェイ3D生成（共通）
│   │   ├── particles.js        # パーティクル（2D/3D）
│   │   └── audio.js            # AudioContext・ピアノ音・リズム効果音
│   └── modes/
│       ├── piano.js            # ピアノモードロジック
│       └── rhythm.js           # リズムモードロジック
│
├── songs/
│   └── metadata.json           # 楽曲メタデータ
└── charts/
    └── starmine.json           # 譜面データ（pulse3d_v1形式）
```

---

## モード

### ピアノモード（`piano.html`）

**キーマッピング（1オクターブ＋2音）**

| 種別 | キー | 音 |
|------|------|----|
| 白鍵 | A S D F G H J K L | C4〜D5 |
| 黒鍵 | W E T Y U O P | C#4〜D#5 |

- **Shift キー**: オクターブ +1（OCT+1 インジケーター表示）
- 全16レーン（白鍵9・黒鍵7）

**スタート画面の操作**

| 操作 | 内容 |
|------|------|
| ▶ START | 全レーン有効でフリープレイ開始 |
| 譜面 JSON 選択 | ピアノ譜面をロード（アクティブレーンが譜面に合わせて絞り込まれる） |
| BGM audio 選択 | 任意。オートプレイ開始時に同期再生 |
| ▶ AUTOPLAY | 譜面ロード後に有効化。自動演奏 |

**譜面ロード時の動作**
- 使用レーンのみ表示するようハイウェイを動的再構築（`LANE_REMAP`）
- 未使用キーをグレーアウト（`.wkey.disabled` / `.bkey.disabled`）
- オートプレイ進捗バー・タイマー表示

**ピアノ譜面フォーマット**
```json
{
  "title": "曲名",
  "totalDuration": 120.0,
  "notes": [
    { "time": 1.23, "lane": 0, "note": "C4" }
  ]
}
```

---

### リズムモード（`rhythm.html`）

- **6ボタン12レーン**（A/S/D/J/K/L 各2レーン担当）
- ノーマルノーツ・ロングノーツ対応
- 判定: PERFECT / GOOD / MISS / HOLD
- BGMファイル選択（任意）
- **デフォルト譜面**: `songs/metadata.json` から `mode: "rhythm"` の曲を自動ロード
- 起動時に譜面JSONファイルを上書き選択可能
- START（手動）/ AUTO（自動演奏）

**リズム譜面フォーマット（pulse3d_v1）**
```json
{
  "format": "pulse3d_v1",
  "version": 1,
  "meta": { "title": "曲名", "bpm": 136.0, "duration": 110000 },
  "notes": [
    { "t": 1230, "lanes": [0, 1], "isLong": false, "holdMs": 0 }
  ]
}
```
- `t`: ミリ秒
- `lanes`: 0〜11のレーン番号配列
- 同一ノーツの最小間隔: 165ms（重複除去）

---

## コアモジュール

### `highway.js` — `buildHighway(scene, cfg)`

ハイウェイ（床・レーン帯・区切り線・ヒットライン・ポイントライト）を `THREE.Group` にまとめて生成。`scene.remove(group)` で一括破棄可能（ピアノのレーン再構築に使用）。

| オプション | 説明 |
|-----------|------|
| `numLanes` | レーン数 |
| `laneSpacing` | レーン間隔 |
| `laneColors` | レーン色（整数配列） |
| `baseLaneOpacity` | レーン基本不透明度（数値 or 配列） |
| `highwayLen` | 奥行き（default: 130） |
| `hitZ` | ヒットラインZ座標（default: 4.2） |
| `showSideBorders` | サイドボーダー表示（default: true） |
| `showHitLights` | ヒット時ポイントライト（default: true） |

### `renderer.js`
`createRenderer` / `createScene` / `createCamera` / `setupResize`

### `particles.js`
- `createParticleSystem3D(scene, hitZ)` — 3D上昇パーティクル
- `createParticleSystem2D(canvas)` — 2Dキャンバスパーティクル

### `audio.js`
- `getAudioCtx()` — AudioContext（遅延初期化）
- `playPianoNote(freq)` — sin+triangle+クリック音のピアノ合成
- `playRhythmSound(type)` — `'hit'` or `'tap'`

---

## 楽曲データ構造

### `songs/metadata.json`
```json
[
  {
    "id": "starmine",
    "title": "Starmine",
    "bpm": 136.0,
    "duration": 110000,
    "mode": "rhythm",
    "chart": "charts/starmine.json"
  }
]
```
- `mode`: `"rhythm"` | `"piano"`

---

## 技術仕様

- Three.js r128（CDN グローバル）
- ES Modules（`type="module"`）、ビルドツールなし
- デバッグ: `python3 -m http.server 8080`
- ブランチ: `version2`（リポジトリ: `showyou/pulse_3d`）
