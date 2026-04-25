# PULSE 3D Auto-Fumen Player — プロジェクト引継ぎ仕様書

最終更新: 2026-04-25  
作業ディレクトリ: `/home/yuki/src/auto-fumen-vocal/`  
Git remote: `https://github.com/showyou/pulse_3d.git` (origin)

---

## プロジェクト概要

Three.js 製の 3D リズムゲーム「PULSE 3D」に、ボーカル音声から自動で譜面を生成して再生する AUTOPLAY 機能を追加したプロジェクト。

- **入力**: demucs で分離した `vocals.wav` / `no_vocals.wav`
- **処理**: `basic-pitch` でピッチ解析 → 譜面 JSON 生成
- **出力**: `PULSE3D_play_auto.html`（単一ファイル）でブラウザ再生

---

## ファイル構成

```
/home/yuki/src/auto-fumen-vocal/
├── PULSE3D_play.html          # 元の手動演奏プレイヤー（変更しない）
├── PULSE3D_play_spec.md       # 元プレイヤーの仕様書
├── PULSE3D_play_auto.html     # ★ビルド成果物（ブラウザで開くファイル）
├── build_autoplay_html.py     # PULSE3D_play.html → _auto.html 生成スクリプト
├── extract_notes.py           # vocals.wav → fumen JSON 生成スクリプト
├── fumen.json                 # 月夜見海月 の譜面（669 notes, 120.75s）
├── fumen_min.json             # 月夜見海月 の譜面（minified、現在未使用）
├── starmine_fumen.json        # Star-mine の譜面（632 notes, 213.28s）
├── HOW_TO_ADD_SONG.md         # 新曲追加手順書（Claude 向け）
├── PROJECT_STATE.md           # このファイル
├── files.zip                  # 元の配布ファイル（参照用）
└── .venv/                     # Python 仮想環境（basic-pitch 等）
```

**音源ファイルの場所**:
```
/home/yuki/src/vocal-splitter/separated/htdemucs/
├── 01 月夜見海月/
│   ├── vocals.wav     (233s, 44100Hz stereo)
│   └── no_vocals.wav
└── 01 Star-mine/
    ├── vocals.wav     (239s, 44100Hz stereo)
    └── no_vocals.wav
```

---

## アーキテクチャ

### ビルドパイプライン

```
PULSE3D_play.html
       ↓ build_autoplay_html.py（文字列パッチ適用）
PULSE3D_play_auto.html
```

`build_autoplay_html.py` は元 HTML に対して以下のパッチを順に適用する:

1. CSS追加（未使用キー `.disabled`、オートプレイUI、OCTインジケーター）
2. HUD にプログレスバー・タイマー・OCTインジケーター追加
3. スタート画面にファイルピッカー（譜面JSON・BGM）と AUTOPLAYボタン追加
4. `LANE_COLORS_HEX` を白鍵（`#c8e8ff`）/ 黒鍵（`#1a3a5a`）の2色に変更
5. `TOTAL_LANES = 16` 直後に動的変数（`FUMEN`, `ACTIVE_LANES` 等）を `let` で宣言
6. `totalHWWidth` の定数宣言を削除（`buildHighway()` 内で計算）
7. ハイウェイ構築コードを `buildHighway()` 関数に包む
   - レーンループを `ACTIVE_LANES` ベースに変更
   - 区切り線を `ACTIVE_TOTAL` ベースに変更
   - `const hitLine3D` → `hitLine3D`（外部 `let` に代入）
8. `getLaneX()` を `LANE_REMAP` 経由のリマップ方式に変更
9. `glowTimers` を `new Map()` に変更
10. animate ループのグロー更新を `ACTIVE_LANES` 反復に変更
11. `buildPianoUI()` で未使用レーンに `.disabled` クラス付与
12. `triggerKey()` に未使用レーン弾き＋オクターブシフト対応を追加
    - Shift キー押下中: `freq * 2`（1オクターブ上）、音名の数字 +1
    - keydown/keyup で `octShift` フラグ管理
13. `AUTOPLAY_JS`（ファイルローダー・オートプレイロジック）を挿入
14. `AUTOPLAY_TICK`（animate ループ内スケジューラ）を挿入

**重要**: パッチは適用順序に依存する。各パッチに `assert` チェックを入れており、
サイレント失敗が起きると即エラーになる。

### PULSE3D_play_auto.html の JS 構造

```
キーマッピング定数 (WHITE_KEYS, BLACK_KEYS, ALL_KEYS, TOTAL_LANES=16)
  ↓
動的変数 (let FUMEN=null, ACTIVE_LANES=[], ACTIVE_TOTAL=0, LANE_REMAP, ...)
  ↓
LANE_COLORS_HEX (白鍵/黒鍵2色)
  ↓
Three.js セットアップ (renderer, scene, camera)
  ↓
const hwGroup (ハイウェイグループ、一度だけ scene に add)
  ↓
function buildHighway()  ← fumen ロード時に呼ばれる
  - 古いメッシュを削除してから再構築
  - ACTIVE_LANES ベースのレーン幅・位置
  ↓
let laneGlowMats = new Map()  ← buildHighway() が再代入
let hitLine3D = null           ← buildHighway() が再代入
  ↓
星・ノーツ定数 (NOTE_Z_SPAWN=-30, NOTE_Z_HIT=4.2, NOTE_SPEED=7.2, ...)
  ↓
function spawnNote() / particles / Audio / HUD / triggerGlow()
  ↓
function buildPianoUI()  ← fumen ロード時・リサイズ時に呼ばれる
  ↓
triggerKey() / releaseKey() / keydown/keyup (Shift = octShift)
  ↓
START ボタンイベント
  ↓
animate() ループ
  - ノーツ移動・消滅
  - レーングロー更新 (laneGlowMats Map を ACTIVE_LANES で反復)
  - HUD フェード
  - [AUTOPLAY_TICK] オートプレイスケジューラ  ← ここに差し込み
  - ヒットライン点滅 (if(hitLine3D) でガード)
  - パーティクル更新
  - renderer.render()
  ↓
LANE_TO_KEY 逆引き Map
  ↓
loadFumenData(data)  ← ファイルピッカーから呼ばれる
  - FUMEN/ACTIVE_LANES/ACTIVE_TOTAL/LANE_REMAP/glowTimers を更新
  - buildHighway() + buildPianoUI() を再実行
  ↓
BGMファイルピッカー・オートプレイロジック
  - TRAVEL_TIME = (NOTE_Z_HIT - NOTE_Z_SPAWN) / NOTE_SPEED ≈ 4.75s
  - startAutoplay(): BGMソース開始 + 序盤ノーツを正しいZ位置から事前生成
  - animate tick: ② audioIdx でヒット時刻に音・エフェクト
                  ① spawnIdx で TRAVEL_TIME 先行してノーツ視覚生成
  ↓
buildPianoUI(); animate();  ← 初期化（fumen なしで起動）
```

### オートプレイのタイミング設計

```
t = note.time - TRAVEL_TIME  →  spawnNoteAtZ(lane, NOTE_Z_SPAWN)  視覚のみ
t = note.time                →  hitAutoNoteEffects()               音・エフェクト
```

序盤ノーツ（`note.time < TRAVEL_TIME`）は startAutoplay() 内で
`z = NOTE_Z_HIT - NOTE_SPEED * note.time` の位置から事前生成。

---

## 主要機能

| 機能 | 状態 |
|------|------|
| 手動演奏（キーボード） | ✅ |
| Shift キーで1オクターブ上 | ✅ 右上に OCT+1 インジケーター |
| 譜面JSON ファイルピッカー | ✅ |
| BGM ファイルピッカー | ✅ BGM なしでも動作 |
| AUTOPLAY | ✅ 視覚と音が同期 |
| プログレスバー・タイマー | ✅ |
| 使用レーンのみ表示 | ✅ fumen ロード後に動的構築 |
| 未使用キーをグレーアウト | ✅ disabled クラス + 入力無効 |
| 白鍵/黒鍵で色分け | ✅ |

---

## HTTPサーバー

```bash
cd /home/yuki/src/auto-fumen-vocal
.venv/bin/python -m http.server 8080 &
```

別マシンからのアクセス: `http://192.168.111.144:8080/PULSE3D_play_auto.html`

停止: `kill $(lsof -ti:8080)`

---

## fumen.json のフォーマット

```json
{
  "title": "曲名",
  "source": "vocals.wav",
  "totalDuration": 120.75,
  "notes": [
    {
      "time": 0.348,
      "duration": 0.151,
      "lane": 7,
      "note": "G4",
      "color": "#ff44aa",
      "amplitude": 0.61,
      "origMidi": 79
    }
  ]
}
```

- `lane`: 0〜15（C4〜D#5）。`extract_notes.py` が MIDI をオクターブ折り返しでマッピング
- `color`: `LANE_COLORS_HEX` で上書きされるため HTML 側では参照しない
- `amplitude < 0.3` のノートはスキップ（ノイズ除去）

---

## Gitワークフロー

- **変更のたびに commit すること**
- push: `git push origin master` → `github.com/showyou/pulse_3d`
- issue 管理は GitHub Issues を使ってよい

```bash
git log --oneline
# 2a0c784 Fix buildHighway: ACTIVE_LANES loop, hitLine3D scope, dividers
# fd16c8b Fix AUTOPLAY_TICK missing from animate loop
# 126d544 Add Shift key octave-up, fix laneGlowMats.push bug, externalize fumen
# eea19f8 Initial commit: PULSE 3D auto-fumen player
```

---

## 既知の問題・TODO

### 未解決
- **譜面タイミング精度**: `basic-pitch` の onset 検出がビートグリッドと無関係なため、
  曲によっては音符位置が体感とずれる。Beat quantization（librosa でBPM検出→16分音符スナップ）で改善できる見込み。
- **Star-mine の totalDuration**: 213s だが実際の音源は 239s。後半に検出ノートなし。

### 未実装（元仕様より）
- ロングノーツ
- リザルト画面
- MISS 判定
- MIDIデバイス対応

---

## 新曲追加手順（要約）

詳細は `HOW_TO_ADD_SONG.md` 参照。

```bash
# 1. extract_notes.py の VOCAL_PATH と title を編集
# 2. 実行
.venv/bin/python extract_notes.py
# 3. 生成された *_fumen.json をブラウザで選択して再生
```
