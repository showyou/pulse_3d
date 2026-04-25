"""
vocals.wav から basic-pitch でノート検出し、
PULSE 3D のレーンにマッピングした fumen JSON を出力する。
"""
import sys
import json
import numpy as np
from pathlib import Path
from basic_pitch.inference import predict
from basic_pitch import ICASSP_2022_MODEL_PATH

VOCAL_PATH = Path("/home/yuki/src/vocal-splitter/separated/htdemucs/01 Star-mine/vocals.wav")
OUTPUT_JSON = Path("/home/yuki/src/auto-fumen-vocal/starmine_fumen.json")

# PULSE 3D のノート→レーン対応 (MIDI番号)
# C4=60, C#4=61, D4=62, ... D#5=75
MIDI_TO_LANE = {
    60: 0,   # C4
    61: 1,   # C#4
    62: 2,   # D4
    63: 3,   # D#4
    64: 4,   # E4
    65: 5,   # F4
    66: 6,   # F#4
    67: 7,   # G4
    68: 8,   # G#4
    69: 9,   # A4
    70: 10,  # A#4
    71: 11,  # B4
    72: 12,  # C5
    73: 13,  # C#5
    74: 14,  # D5
    75: 15,  # D#5
}

NOTE_NAMES = {
    60: "C4", 61: "C#4", 62: "D4", 63: "D#4", 64: "E4",
    65: "F4", 66: "F#4", 67: "G4", 68: "G#4", 69: "A4",
    70: "A#4", 71: "B4", 72: "C5", 73: "C#5", 74: "D5", 75: "D#5",
}

LANE_COLORS_HEX = [
    '#00ffee','#44ddff','#22bbff','#88aaff',
    '#aa66ff','#cc44ff','#ee22cc','#ff44aa',
    '#00ffbb','#44ffcc','#66ffdd','#88ffcc',
    '#aaffaa','#ccff77','#ffee44','#ffbb22',
]

# PULSE 3D の音域: MIDI 60-75 (C4-D#5)
MIDI_MIN = 60
MIDI_MAX = 75
MIDI_RANGE = MIDI_MAX - MIDI_MIN + 1  # 16

def midi_to_pulse_lane(midi_note: int):
    """MIDI番号をオクターブ折り返しでPULSE 3Dのレーンにマップ"""
    # まずオクターブを揃えて 60-75 に収める
    while midi_note < MIDI_MIN:
        midi_note += 12
    while midi_note > MIDI_MAX:
        midi_note -= 12
    return MIDI_TO_LANE.get(midi_note), midi_note

print("basic-pitch でピッチ解析中...")
model_output, midi_data, note_events = predict(str(VOCAL_PATH))

print(f"検出ノート数: {len(note_events)}")

# note_events: list of (start_time, end_time, pitch_midi, amplitude, ...)
fumen_notes = []
skipped = 0

for event in note_events:
    start_time   = float(event[0])
    end_time     = float(event[1])
    midi_pitch   = int(event[2])
    amplitude    = float(event[3])

    # 振幅が小さすぎるものはスキップ（ノイズ除去）
    if amplitude < 0.3:
        skipped += 1
        continue

    lane, snapped_midi = midi_to_pulse_lane(midi_pitch)
    if lane is None:
        skipped += 1
        continue

    fumen_notes.append({
        "time": round(start_time, 3),
        "duration": round(end_time - start_time, 3),
        "lane": lane,
        "note": NOTE_NAMES.get(snapped_midi, "?"),
        "color": LANE_COLORS_HEX[lane],
        "amplitude": round(amplitude, 3),
        "origMidi": midi_pitch,
    })

# 時刻順ソート
fumen_notes.sort(key=lambda n: n["time"])

fumen = {
    "title": "Star-mine",
    "source": "vocals.wav",
    "totalDuration": round(float(note_events[-1][1]) if note_events else 0, 2),
    "notes": fumen_notes,
}

OUTPUT_JSON.write_text(json.dumps(fumen, ensure_ascii=False, indent=2))
print(f"fumen.json を出力しました: {len(fumen_notes)} ノート (スキップ: {skipped})")
print(f"総再生時間: {fumen['totalDuration']:.1f}秒")

# 統計
if fumen_notes:
    lanes_used = set(n["lane"] for n in fumen_notes)
    print(f"使用レーン数: {len(lanes_used)} / 16")
    for lane in sorted(lanes_used):
        cnt = sum(1 for n in fumen_notes if n["lane"] == lane)
        note_name = list(NOTE_NAMES.values())[lane] if lane < len(NOTE_NAMES) else "?"
        print(f"  Lane {lane:2d} ({note_name:4s}): {cnt} notes")
