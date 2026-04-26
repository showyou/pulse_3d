# auto-fumen-vocal

ボーカル音声から自動で譜面を生成し、3D ハイウェイ形式で AUTOPLAY 再生するツールです。

## ファイル構成

| ファイル | 説明 |
|---------|------|
| `PULSE3D_play.html` | 手動演奏モードのプレイヤー HTML（直接編集しない） |
| `PULSE3D_play_auto.html` | AUTOPLAY 対応プレイヤー HTML（`build_autoplay_html.py` で生成） |
| `build_autoplay_html.py` | `PULSE3D_play.html` をベースに `PULSE3D_play_auto.html` を生成するスクリプト |
| `extract_notes.py` | `vocals.wav` を basic-pitch でピッチ解析し、PULSE 3D 形式の譜面 JSON を出力するスクリプト |
| `HOW_TO_ADD_SONG.md` | 新しい曲の譜面を追加するための詳細手順書 |

## 必要なもの

- Python 仮想環境（`.venv/`、`uv venv` で作成済み）
- basic-pitch（`.venv/` にインストール済み）
- demucs 等で分離した `vocals.wav`（ピッチ解析用）と `no_vocals.wav`（BGM 用）

## 実行手順

### 1. 譜面 JSON を生成する

`extract_notes.py` の `VOCAL_PATH` と `OUTPUT_JSON` を編集して曲のパスを指定する。

```python
VOCAL_PATH = Path("/path/to/vocals.wav")
OUTPUT_JSON = Path("/home/yuki/src/auto-fumen-vocal/曲名_fumen.json")
```

実行：

```bash
cd /home/yuki/src/auto-fumen-vocal
.venv/bin/python extract_notes.py
```

### 2. HTTP サーバーを起動する

```bash
.venv/bin/python -m http.server 8080
```

### 3. ブラウザで再生する

```
http://localhost:8080/PULSE3D_play_auto.html
```

1. 「♩ 譜面 JSON を選択」→ 生成した `*_fumen.json` を選ぶ
2. 「♪ 伴奏 audio を選択」→ `no_vocals.wav` を選ぶ（省略可）
3. 「▶ AUTOPLAY」をクリック

### 4. プレイヤー HTML を再生成する（変更した場合のみ）

`PULSE3D_play.html` を編集した後、AUTOPLAY 版に反映するには：

```bash
.venv/bin/python build_autoplay_html.py
```

## 詳細

新しい曲の追加手順、調整パラメータ、トラブルシューティングは [HOW_TO_ADD_SONG.md](HOW_TO_ADD_SONG.md) を参照。
