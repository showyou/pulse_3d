# PULSE 3D PLAY MODE — Claude Code 引継ぎ仕様書

## プロジェクト概要

Three.js製3D音楽演奏アプリ。元のリズムゲーム「PULSE 3D」の派生版として開発。
プレイヤーがキーボード（またはタッチ）を押すと音が鳴り、対応するレーンからノーツが上から降ってくる演奏ビジュアライザー。

**配布形式**: 単一HTMLファイル（`PULSE3D_play.html`）  
**外部依存**: Three.js r128（CDN）、Google Fonts（Orbitron / Share Tech Mono）  
**動作環境**: モダンブラウザ（ローカルファイル直接起動可）

---

## ファイル構成

```
PULSE3D_play.html   # すべてのコード（HTML/CSS/JS）を含む単一ファイル
```

---

## 画面・レイアウト

| 項目 | 内容 |
|------|------|
| 向き | 横画面（Landscape）想定。縦持ち時は「↻ ROTATE DEVICE」警告表示（タッチデバイスのみ） |
| 背景 | 星空（Three.js Points）。元仕様の動画背景は未実装 |
| 視点 | 3D斜め見下ろし（Guitar Hero風）。`camera.position = (0, 5.5, 9)`、`lookAt(0, 0, -8)` |
| Canvas | `alpha:true` で透明（将来的な動画背景合成を考慮） |
| スキャンライン | CSSの `repeating-linear-gradient` でレトロ演出 |

---

## レーン構成

| 定数 | 値 | 説明 |
|------|----|------|
| `TOTAL_LANES` | 16 | 白鍵9レーン + 黒鍵7レーン |
| `LANE_W` | 0.52 | 1レーン幅（Three.js units） |
| `LANE_GAP` | 0.018 | レーン間隔 |
| `LANE_UNIT` | 0.538 | `LANE_W + LANE_GAP` |

レーンインデックスは音程順（低→高）に左から割り当てられており、白鍵・黒鍵がピアノ鍵盤の並び順と対応している。

---

## キーマッピング

### 白鍵（ASDの段）

| キー | 音名 | 周波数(Hz) | レーンindex |
|------|------|-----------|------------|
| A | C4 | 261.63 | 0 |
| S | D4 | 293.66 | 2 |
| D | E4 | 329.63 | 4 |
| F | F4 | 349.23 | 5 |
| G | G4 | 392.00 | 7 |
| H | A4 | 440.00 | 9 |
| J | B4 | 493.88 | 11 |
| K | C5 | 523.25 | 12 |
| L | D5 | 587.33 | 14 |

### 黒鍵（QWEの段）

ピアノ配列に従い、白鍵の間に配置（E-F間・B-C間はなし）。

| キー | 音名 | 周波数(Hz) | レーンindex | afterWhiteIdx |
|------|------|-----------|------------|---------------|
| W | C#4 | 277.18 | 1 | 1 |
| E | D#4 | 311.13 | 3 | 2 |
| T | F#4 | 369.99 | 6 | 4 |
| Y | G#4 | 415.30 | 8 | 5 |
| U | A#4 | 466.16 | 10 | 6 |
| O | C#5 | 554.37 | 13 | 8 |
| P | D#5 | 622.25 | 15 | 9 |

`afterWhiteIdx` は白鍵配列上のインデックス（0始まり）で「右隣の白鍵の番号」。黒鍵のDOM配置計算に使用。

---

## ノーツ仕様

| 定数 | 値 | 説明 |
|------|----|------|
| `NOTE_Z_SPAWN` | -30 | スポーン位置（奥） |
| `NOTE_Z_HIT` | 4.2 | ヒットライン位置（手前） |
| `NOTE_Z_DEAD` | 9.0 | 消滅位置 |
| `NOTE_SPEED` | 7.2 | 移動速度（units/秒） |
| `NOTE_H` | 0.2 | ノーツの厚み |

- キー押下で `NOTE_Z_SPAWN` から `spawnNote(laneIdx)` を呼びノーツ生成
- 毎フレーム `NOTE_SPEED * dt` だけ +Z方向（手前）へ移動
- `NOTE_Z_DEAD` 通過後に `scene.remove()` + 配列から削除
- 装飾: `EdgesGeometry`（白い縁取り）+ 上面ハイライトPlane

### レーン色（16色）

```js
const LANE_COLORS_HEX = [
  '#00ffee','#44ddff','#22bbff','#88aaff',  // lane 0-3
  '#aa66ff','#cc44ff','#ee22cc','#ff44aa',  // lane 4-7
  '#00ffbb','#44ffcc','#66ffdd','#88ffcc',  // lane 8-11
  '#aaffaa','#ccff77','#ffee44','#ffbb22',  // lane 12-15
];
```

---

## サウンド（Web Audio API）

`playPianoNote(freq)` 関数でピアノ風音色を合成。外部音源ファイル不要。

| コンポーネント | 詳細 |
|--------------|------|
| オシレーター1 | `sine` 波、基音（freq） |
| オシレーター2 | `triangle` 波、2倍音（freq×2） |
| エンベロープ1 | アタック8ms → 0.42、120ms後に0.14、1.1秒でフェードアウト |
| エンベロープ2 | アタック5ms → 0.10、380ms でフェードアウト |
| ハンマークリック | `square` 波 freq×3.5、18ms で即消え |
| フィルター | `lowpass` 4500Hz → 700Hz（0.6秒かけて閉じる） |

---

## ピアノUI（DOM）

画面下部に実ピアノと同じ2段構造で表示。

```
#keyboard-ui
  #black-row    ← 黒鍵（position:absolute で各位置に配置）
  #white-row    ← 白鍵（display:flex で均等分割）
```

### 白鍵配置
`flex:1` で9等分。押下時に `.active` クラス付与 → シアングロー。

### 黒鍵配置
`position:absolute` で `#black-row` 内に絶対配置。

```js
const wPct  = 100 / WHITE_KEYS.length;   // 1白鍵幅(%)
const bwPct = wPct * 0.62;               // 黒鍵幅(%)
const centerPct = k.afterWhiteIdx * wPct; // 配置中心(%)
div.style.left  = `calc(${centerPct}% - ${bwPct/2}%)`;
div.style.width = `${bwPct}%`;
```

タッチ対応: `pointerdown` / `pointerup` / `pointerleave` で `triggerKey` / `releaseKey` を呼ぶ。

---

## HUD

| 要素 | DOM ID | 位置 | 詳細 |
|------|--------|------|------|
| スコア | `#score-val` | 左上 | `toLocaleString()` 表示、押下時に一瞬scale拡大 |
| スコアラベル | `#score-label` | スコア下 | 固定文字「SCORE」 |
| コンボ数 | `#combo-num` | 右中央 | コンボ0時は非表示 |
| コンボラベル | `#combo-label` | コンボ下 | 初期`display:none`、初回押下で表示 |
| 判定文字 | `#judge-text` | 上部中央 | 現状常に「PERFECT!」（0.65秒でフェード） |
| 音名ポップ | `#note-name-pop` | 判定文字の下 | 押した音名（例:「C#4」）が0.5秒表示 |

---

## スコアリング

```js
// コンボ倍率
const mult = Math.max(1, Math.floor(combo / 4));
// 加算
addScore(300 * mult);
```

現在すべての入力が PERFECT 扱い（自由演奏モードのため判定なし）。

---

## エフェクト

### パーティクル（2D Canvas `#pfx`）
- キー押下時に `spawnParticles(screenX, screenY, color)` で16個生成
- 放射状に飛散、重力（vy += 0.13/frame）、フェードアウト
- スクリーン座標は Three.js の `.project(camera)` で変換

### レーングロー（Three.js）
- `glowTimers[laneIdx] = 1.0` セット → `dt*3.5` ずつ減衰
- 対応レーンの `MeshBasicMaterial.opacity` を一時的に増幅

### カメラドリフト
```js
camera.position.x = Math.sin(elapsed * 0.21) * 0.16;
camera.position.y = 5.5 + Math.sin(elapsed * 0.34) * 0.07;
```

### ヒットライン点滅
```js
hitLine3D.material.opacity = 0.7 + Math.sin(Date.now() * 0.006) * 0.22;
```

---

## メインループ（`animate()`）

```
requestAnimationFrame ループ
  ├─ ノーツ移動（+Z方向）・消滅チェック
  ├─ レーングロー減衰
  ├─ HUD タイマーフェード（judgeTimer, notePopTimer）
  ├─ カメラドリフト更新
  ├─ ヒットライン点滅
  ├─ updateParticles()（2D Canvas描画）
  └─ renderer.render(scene, camera)
```

dt は `Math.min((now - lastTime) / 1000, 0.05)` でフレームレートに依存しない。

---

## 未実装・既知の制限

| 項目 | 状態 |
|------|------|
| 動画背景 | 未実装（alpha:true の準備のみ） |
| MISS判定 | なし（自由演奏モードのため） |
| ロングノーツ | 未実装 |
| オートプレイ | 未実装 |
| リザルト画面 | 未実装 |
| 録音・MIDIエクスポート | 未実装 |
| タッチ多点同時押し | pointerdown単体は対応、マルチタッチ同時は未検証 |

---

## 拡張アイデア（優先度順）

1. **音域拡張**: `WHITE_KEYS` / `BLACK_KEYS` 配列に音を追加するだけでレーンが増える設計
2. **オクターブ切替**: Shift/Ctrl でオクターブを上下させる
3. **録音モード**: 演奏を時系列で記録 → ビートマップ自動生成
4. **動画背景**: `<video>` タグ追加 + ファイル選択UI
5. **MIDIデバイス対応**: Web MIDI API で外部キーボード入力
6. **リバーブ/エフェクト**: `ConvolverNode` や `DelayNode` の追加

---

## 開発時の注意点

- Three.js は **r128固定**（`EdgesGeometry`のAPIがr128準拠）
- `AudioContext` はユーザー操作後に生成（START ボタンクリック時）
- `buildPianoUI()` はリサイズ時に再呼び出しされる（innerHTML リセット + DOM再生成）
- レーンインデックスは音程順の連番（白鍵・黒鍵が混在）。ピアノのオクターブ構造とは独立
- `afterWhiteIdx` は白鍵配列（0〜8）上のインデックスであり、MIDI番号等とは無関係
