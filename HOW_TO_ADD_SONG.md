# 新しい曲の譜面を作る手順

このドキュメントは Claude が新しい曲の譜面（fumen.json）を生成するための手順書です。

## 前提条件

- 作業ディレクトリ: `/home/yuki/src/auto-fumen-vocal/`
- Python 仮想環境 `.venv/` が存在する（`uv venv` で作成済み）
- `basic-pitch` 等のパッケージがインストール済み

## 入力ファイル

demucs 等で分離した以下の2ファイルが必要：

| ファイル | 内容 |
|---------|------|
| `vocals.wav` | ボーカルのみ（ピッチ解析に使用） |
| `no_vocals.wav` | 伴奏のみ（AUTOPLAY 時の BGM として使用） |

## 手順

### Step 1: 入力ファイルのパスを確認する

```bash
ls "<vocals.wavとno_vocals.wavが入っているディレクトリ>"
```

`vocals.wav` と `no_vocals.wav` の両方が存在することを確認する。

### Step 2: extract_notes.py の VOCAL_PATH を書き換える

`/home/yuki/src/auto-fumen-vocal/extract_notes.py` の以下の行を編集する：

```python
# 変更前（例）
VOCAL_PATH = Path("/home/yuki/src/vocal-splitter/separated/htdemucs/01 月夜見海月/vocals.wav")

# 変更後（新しい曲のパスに置き換える）
VOCAL_PATH = Path("<新しい曲の vocals.wav への絶対パス>")
```

また、出力 JSON のファイル名も曲ごとに変えておくと管理しやすい：

```python
# 例: 曲名を含むファイル名にする
OUTPUT_JSON = Path("/home/yuki/src/auto-fumen-vocal/<曲名>_fumen.json")
```

`title` フィールドも曲名に合わせて書き換える：

```python
fumen = {
    "title": "<曲名>",   # ← ここを変更
    ...
}
```

### Step 3: ピッチ解析を実行する

```bash
cd /home/yuki/src/auto-fumen-vocal
.venv/bin/python extract_notes.py
```

完了すると以下のような出力が表示される：

```
basic-pitch でピッチ解析中...
検出ノート数: XXX
fumen.json を出力しました: XXX ノート (スキップ: XX)
総再生時間: XX.Xs
使用レーン数: XX / 16
  Lane  X (XX  ): XX notes
  ...
```

処理時間の目安: 4分前後の曲で 1〜3 分程度（CPU による）。

### Step 4: 生成された fumen.json を確認する

```bash
.venv/bin/python -c "
import json
d = json.load(open('<OUTPUT_JSON のパス>'))
print('title:', d['title'])
print('notes:', len(d['notes']))
print('duration:', d['totalDuration'], 's')
print('first note:', d['notes'][0])
print('last  note:', d['notes'][-1])
"
```

`notes` が 0 件の場合はボーカルが検出できていない（ファイルパスや音量を確認）。

### Step 5: fumen.json を minify してブラウザ用に最適化する（任意）

`fumen_min.json` は現在 HTML には埋め込まれていないため、このステップは省略可能。
ただしファイルサイズを小さくしたい場合：

```bash
.venv/bin/python -c "
import json
d = json.load(open('<fumen.json のパス>'))
d['notes'] = [{'time':n['time'],'lane':n['lane'],'note':n['note'],'color':n['color']} for n in d['notes']]
open('<出力先パス>', 'w').write(json.dumps(d, ensure_ascii=False, separators=(',',':')))
print('done')
"
```

### Step 6: ブラウザで再生する

HTTP サーバーが停止している場合は再起動する：

```bash
cd /home/yuki/src/auto-fumen-vocal
lsof -ti:8080 > /dev/null && echo "稼働中" || (.venv/bin/python -m http.server 8080 &)
```

別マシンのブラウザで以下にアクセス：

```
http://192.168.111.144:8080/PULSE3D_play_auto.html
```

スタート画面で：
1. 「♩ 譜面 JSON を選択」→ 生成した fumen.json を選ぶ
2. 「♪ 伴奏 audio を選択」→ no_vocals.wav を選ぶ（省略可）
3. 「▶ AUTOPLAY」をクリック

## 調整パラメータ

`extract_notes.py` 内の以下の値を変えることで品質を調整できる：

| パラメータ | 場所 | 意味 | デフォルト |
|-----------|------|------|-----------|
| `amplitude < 0.3` | ノイズ除去条件 | この値より小さい音を除外（大きくするほど音が減る） | `0.3` |
| `MIDI_MIN / MIDI_MAX` | 音域設定 | 対象とする MIDI 音域（60=C4, 75=D#5） | `60 / 75` |

## ファイル構成（参考）

```
/home/yuki/src/auto-fumen-vocal/
├── PULSE3D_play.html          # 元の手動演奏 HTML（変更しない）
├── PULSE3D_play_auto.html     # 自動生成されたプレイヤー HTML
├── build_autoplay_html.py     # HTML 生成スクリプト
├── extract_notes.py           # 譜面生成スクリプト（曲ごとにパスを変える）
├── fumen.json                 # 生成された譜面（フルサイズ）
├── fumen_min.json             # 譜面（minified 版）
├── HOW_TO_ADD_SONG.md         # このファイル
└── .venv/                     # Python 仮想環境
```

## トラブルシューティング

**ノート数が極端に少ない（< 50）**
→ ボーカルトラックに音がほとんどない可能性。`ffprobe vocals.wav` で音量を確認する。

**特定の音域に集中しすぎる**
→ 曲のキーが MIDI 60〜75（C4〜D#5）から外れている可能性。`MIDI_MIN / MIDI_MAX` を調整する。

**再生タイミングがずれる**
→ BGM ファイルと fumen の元音源が一致しているか確認。demucs で分離した vocals.wav と no_vocals.wav はペアで使うこと。
