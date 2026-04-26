"""
drums.wav + bass.wav から pulse3d_v1 形式の12レーン譜面を生成する。

レーン割り当て:
  ドラム onset (強) → レーン 0,1  (A担当)
  ドラム onset (弱) → レーン 2,3  (S担当)
  ドラム onset (中) → レーン 4,5  (D担当)
  ベース onset (強) → レーン 6,7  (J担当)
  ベース onset (弱) → レーン 8,9  (K担当)
  ベース onset (中) → レーン 10,11 (L担当)
"""

import json, sys
import numpy as np
import librosa

DRUMS_PATH = "/home/yuki/src/vocal-splitter/separated/htdemucs/01 Star-mine/drums.wav"
BASS_PATH  = "/home/yuki/src/vocal-splitter/separated/htdemucs/01 Star-mine/bass.wav"
OUT_PATH   = "/home/yuki/src/auto-fumen-vocal/charts/starmine.json"

BPM_HINT   = 136.0
MIN_GAP_MS = 165      # 同レーンの最小間隔
MAX_NOTES  = 1200     # 上限（多すぎる場合に間引き）

def detect_onsets(path, sr=22050):
    y, sr = librosa.load(path, sr=sr, mono=True)
    onset_frames = librosa.onset.onset_detect(
        y=y, sr=sr,
        hop_length=512,
        backtrack=True,
        units='frames'
    )
    onset_times = librosa.frames_to_time(onset_frames, sr=sr, hop_length=512)

    # onset強度を計算してランク付けに使う
    onset_env = librosa.onset.onset_strength(y=y, sr=sr, hop_length=512)
    strengths = onset_env[onset_frames]
    return onset_times, strengths

def classify_strength(strengths):
    """強度を3段階 (0=強, 1=弱, 2=中) に分類"""
    p33 = np.percentile(strengths, 33)
    p66 = np.percentile(strengths, 66)
    labels = []
    for s in strengths:
        if s >= p66:
            labels.append(0)   # 強
        elif s < p33:
            labels.append(1)   # 弱
        else:
            labels.append(2)   # 中
    return labels

def apply_min_gap(notes, min_gap_ms):
    """同レーンペアごとに最小間隔フィルタを適用"""
    last_t = {}
    result = []
    for n in sorted(notes, key=lambda x: x['t']):
        key = tuple(sorted(n['lanes']))
        t = n['t']
        if last_t.get(key, -9999) + min_gap_ms <= t:
            result.append(n)
            last_t[key] = t
    return result

def build_notes(times, strengths, lane_map):
    """onset → noteリスト。lane_map[strength_label] = [lane_a, lane_b]"""
    notes = []
    labels = classify_strength(strengths)
    for t, label in zip(times, labels):
        t_ms = int(round(t * 1000))
        notes.append({
            "t": t_ms,
            "lanes": lane_map[label],
            "isLong": False,
            "holdMs": 0,
        })
    return notes

print("Loading drums...")
d_times, d_strengths = detect_onsets(DRUMS_PATH)
print(f"  {len(d_times)} drum onsets")

print("Loading bass...")
b_times, b_strengths = detect_onsets(BASS_PATH)
print(f"  {len(b_times)} bass onsets")

drum_lane_map = {0: [0, 1], 1: [2, 3], 2: [4, 5]}
bass_lane_map = {0: [6, 7], 1: [8, 9], 2: [10, 11]}

drum_notes = build_notes(d_times, d_strengths, drum_lane_map)
bass_notes = build_notes(b_times, b_strengths, bass_lane_map)

all_notes = drum_notes + bass_notes
all_notes = apply_min_gap(all_notes, MIN_GAP_MS)
all_notes.sort(key=lambda n: n['t'])

# 多すぎる場合は強いものを優先して間引き
if len(all_notes) > MAX_NOTES:
    all_notes = all_notes[:MAX_NOTES]

# 曲の長さ（ドラム末尾onset + 余白）
total_ms = int(d_times[-1] * 1000) + 4000 if len(d_times) else 120000

chart = {
    "format": "pulse3d_v1",
    "version": 1,
    "meta": {
        "title": "Star-mine",
        "bpm": BPM_HINT,
        "duration": total_ms,
    },
    "notes": all_notes,
}

with open(OUT_PATH, "w", encoding="utf-8") as f:
    json.dump(chart, f, ensure_ascii=False, separators=(',', ':'))

print(f"\n完了: {len(all_notes)} ノーツ → {OUT_PATH}")
print(f"曲長: {total_ms/1000:.1f}s")
