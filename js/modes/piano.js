import { buildHighway }               from '../core/highway.js';
import { createParticleSystem2D }     from '../core/particles.js';
import { getAudioCtx, playPianoNote } from '../core/audio.js';

// ─── キー定義 ────────────────────────────────
const WHITE_KEYS = [
  { key:'a', note:'C4',  freq:261.63, lane:0  },
  { key:'s', note:'D4',  freq:293.66, lane:2  },
  { key:'d', note:'E4',  freq:329.63, lane:4  },
  { key:'f', note:'F4',  freq:349.23, lane:5  },
  { key:'g', note:'G4',  freq:392.00, lane:7  },
  { key:'h', note:'A4',  freq:440.00, lane:9  },
  { key:'j', note:'B4',  freq:493.88, lane:11 },
  { key:'k', note:'C5',  freq:523.25, lane:12 },
  { key:'l', note:'D5',  freq:587.33, lane:14 },
];
const BLACK_KEYS = [
  { key:'w', note:'C#4', freq:277.18, afterWhiteIdx:1, lane:1  },
  { key:'e', note:'D#4', freq:311.13, afterWhiteIdx:2, lane:3  },
  { key:'t', note:'F#4', freq:369.99, afterWhiteIdx:4, lane:6  },
  { key:'y', note:'G#4', freq:415.30, afterWhiteIdx:5, lane:8  },
  { key:'u', note:'A#4', freq:466.16, afterWhiteIdx:6, lane:10 },
  { key:'o', note:'C#5', freq:554.37, afterWhiteIdx:8, lane:13 },
  { key:'p', note:'D#5', freq:622.25, afterWhiteIdx:9, lane:15 },
];
const ALL_KEYS = {};
WHITE_KEYS.forEach(k => ALL_KEYS[k.key] = k);
BLACK_KEYS.forEach(k => ALL_KEYS[k.key] = k);

const LANE_TO_KEY = {};
WHITE_KEYS.forEach(k => LANE_TO_KEY[k.lane] = k);
BLACK_KEYS.forEach(k => LANE_TO_KEY[k.lane] = k);

// ─── 定数 ────────────────────────────────────
const TOTAL_LANES  = 16;
const LANE_W       = 0.52;
const LANE_GAP     = 0.018;
const LANE_UNIT    = LANE_W + LANE_GAP;
const BLACK_LANE_SET = new Set(BLACK_KEYS.map(k => k.lane));
const LANE_COLORS_HEX = Array.from({ length: TOTAL_LANES }, (_, i) =>
  BLACK_LANE_SET.has(i) ? '#1a3a5a' : '#c8e8ff'
);
const LANE_COLORS_INT = LANE_COLORS_HEX.map(c => parseInt(c.replace('#', ''), 16));

const NOTE_Z_SPAWN  = -30;
const NOTE_Z_HIT    =  4.2;
const NOTE_Z_DEAD   =  9.0;
const NOTE_SPEED    =  7.2;
const NOTE_H        =  0.2;
const TRAVEL_TIME   = (NOTE_Z_HIT - NOTE_Z_SPAWN) / NOTE_SPEED;

// ─── ファクトリ ──────────────────────────────
export function createPianoMode(scene, camera, pfxCanvas) {
  // fumen ロード後に確定する動的変数
  let FUMEN          = null;
  let ACTIVE_LANE_SET = new Set(Array.from({ length: TOTAL_LANES }, (_, i) => i));
  let ACTIVE_LANES    = Array.from({ length: TOTAL_LANES }, (_, i) => i);
  let ACTIVE_TOTAL    = TOTAL_LANES;
  let LANE_REMAP      = new Map(ACTIVE_LANES.map((orig, idx) => [orig, idx]));

  function getLaneX(laneIdx) {
    const di = LANE_REMAP.has(laneIdx) ? LANE_REMAP.get(laneIdx) : 0;
    return (di - (ACTIVE_TOTAL - 1) / 2) * LANE_UNIT;
  }

  // ─── ハイウェイ ──────────────────────────────
  let hwResult = null;
  let laneGlowMats = [];
  let hitLine3D    = null;
  let glowTimers   = [];

  function rebuildHighway() {
    if (hwResult) scene.remove(hwResult.group);

    const activeLaneColors  = ACTIVE_LANES.map(i => LANE_COLORS_INT[i]);
    const activeLaneOpacity = ACTIVE_LANES.map(i => BLACK_LANE_SET.has(i) ? 0.055 : 0.08);

    hwResult = buildHighway(scene, {
      numLanes:        ACTIVE_TOTAL,
      laneSpacing:     LANE_UNIT,
      laneGlowWidth:   LANE_W - 0.04,
      laneColors:      activeLaneColors,
      baseLaneOpacity: activeLaneOpacity,
      highwayLen:      44,
      hitZ:            NOTE_Z_HIT,
      showSideBorders: false,
      showHitLights:   false,
    });

    laneGlowMats = hwResult.laneGlowMats;
    hitLine3D    = hwResult.hitLine3D;
    glowTimers   = new Array(ACTIVE_TOTAL).fill(0);
  }

  rebuildHighway();

  // 星
  const starPos = new Float32Array(900 * 3);
  for (let i = 0; i < 900; i++) {
    starPos[i*3]   = (Math.random() - 0.5) * 130;
    starPos[i*3+1] = Math.random() * 50 + 2;
    starPos[i*3+2] = (Math.random() - 0.5) * 130;
  }
  const starsGeo = new THREE.BufferGeometry();
  starsGeo.setAttribute('position', new THREE.BufferAttribute(starPos, 3));
  scene.add(new THREE.Points(starsGeo,
    new THREE.PointsMaterial({ color: 0xaaddff, size: 0.18, transparent: true, opacity: 0.65 })
  ));

  // ─── ノーツ ─────────────────────────────────
  const activeNotes = [];

  function spawnNoteAtZ(laneIdx, zPos) {
    const x     = getLaneX(laneIdx);
    const color = LANE_COLORS_INT[laneIdx];
    const geo   = new THREE.BoxGeometry(LANE_W - 0.05, NOTE_H, 0.52);
    const mat   = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.93 });
    const mesh  = new THREE.Mesh(geo, mat);
    mesh.add(new THREE.LineSegments(
      new THREE.EdgesGeometry(geo),
      new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.65 })
    ));
    const hl = new THREE.Mesh(
      new THREE.PlaneGeometry(LANE_W - 0.1, 0.07),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.55 })
    );
    hl.position.set(0, NOTE_H / 2 + 0.001, 0);
    mesh.add(hl);
    mesh.position.set(x, NOTE_H / 2, zPos);
    scene.add(mesh);
    activeNotes.push({ mesh });
  }

  function spawnNote(laneIdx) { spawnNoteAtZ(laneIdx, NOTE_Z_SPAWN); }

  // ─── 2D パーティクル ─────────────────────────
  const fx2d = createParticleSystem2D(pfxCanvas);

  function getLaneScreenX(laneIdx) {
    const v = new THREE.Vector3(getLaneX(laneIdx), 0, NOTE_Z_HIT);
    v.project(camera);
    return (v.x * 0.5 + 0.5) * innerWidth;
  }
  function getHitLineScreenY() {
    const v = new THREE.Vector3(0, 0, NOTE_Z_HIT);
    v.project(camera);
    return (v.y * -0.5 + 0.5) * innerHeight;
  }

  // ─── HUD ─────────────────────────────────────
  const scoreEl    = document.getElementById('score-val');
  const comboNumEl = document.getElementById('combo-num');
  const comboLblEl = document.getElementById('combo-label');
  const judgeEl    = document.getElementById('judge-text');
  const notePopEl  = document.getElementById('note-name-pop');
  const autoBarFill = document.getElementById('autoplay-bar-fill');
  const autoTimerEl = document.getElementById('autoplay-timer');
  const octIndicator = document.getElementById('oct-indicator');
  let judgeTimer = 0, notePopTimer = 0;
  let score = 0, combo = 0;

  function showJudge(txt, col) {
    judgeEl.textContent = txt; judgeEl.style.color = col; judgeEl.style.opacity = '1';
    judgeTimer = 0.65;
  }
  function showNotePop(name, col) {
    notePopEl.textContent = name; notePopEl.style.color = col; notePopEl.style.opacity = '1';
    notePopTimer = 0.5;
  }
  function addScore(pts) {
    score += pts;
    scoreEl.textContent = score.toLocaleString();
    scoreEl.style.transform = 'scale(1.18)';
    setTimeout(() => scoreEl.style.transform = '', 80);
  }
  function bumpCombo() {
    combo++;
    comboNumEl.textContent = combo;
    comboLblEl.style.display = 'block';
    comboNumEl.style.transform = 'scale(1.3)';
    setTimeout(() => comboNumEl.style.transform = '', 80);
  }

  function fmtTime(sec) {
    const m = Math.floor(sec / 60);
    const s = Math.floor(sec % 60).toString().padStart(2, '0');
    return m + ':' + s;
  }

  // ─── レーングロー ─────────────────────────────
  function triggerGlow(originalLaneIdx) {
    const di = LANE_REMAP.get(originalLaneIdx);
    if (di !== undefined) glowTimers[di] = 1.0;
  }

  // ─── ピアノ UI ───────────────────────────────
  const keyDomMap = {};

  function buildPianoUI() {
    const whiteRow = document.getElementById('white-row');
    const blackRow = document.getElementById('black-row');
    whiteRow.innerHTML = '';
    blackRow.innerHTML = '';
    const wPct  = 100 / WHITE_KEYS.length;
    const bwPct = wPct * 0.62;

    WHITE_KEYS.forEach(k => {
      const disabled = !ACTIVE_LANE_SET.has(k.lane);
      const div = document.createElement('div');
      div.className = disabled ? 'wkey disabled' : 'wkey';
      div.innerHTML = `<span class="klabel">${k.key.toUpperCase()}</span><span class="knote">${k.note}</span>`;
      whiteRow.appendChild(div);
      if (!disabled) {
        div.addEventListener('pointerdown',  e => { e.preventDefault(); triggerKey(k.key); });
        div.addEventListener('pointerup',    e => { e.preventDefault(); releaseKey(k.key); });
        div.addEventListener('pointerleave', e => { e.preventDefault(); releaseKey(k.key); });
      }
      keyDomMap[k.key] = div;
    });

    BLACK_KEYS.forEach(k => {
      const disabled = !ACTIVE_LANE_SET.has(k.lane);
      const div = document.createElement('div');
      div.className = disabled ? 'bkey disabled' : 'bkey';
      div.innerHTML = `<span class="klabel">${k.key.toUpperCase()}</span><span class="knote">${k.note}</span>`;
      div.style.left     = `calc(${k.afterWhiteIdx * wPct}% - ${bwPct / 2}%)`;
      div.style.width    = `${bwPct}%`;
      div.style.minWidth = '22px';
      blackRow.appendChild(div);
      if (!disabled) {
        div.addEventListener('pointerdown',  e => { e.preventDefault(); triggerKey(k.key); });
        div.addEventListener('pointerup',    e => { e.preventDefault(); releaseKey(k.key); });
        div.addEventListener('pointerleave', e => { e.preventDefault(); releaseKey(k.key); });
      }
      keyDomMap[k.key] = div;
    });
  }

  // ─── 入力 ────────────────────────────────────
  const pressedKeys = new Set();
  let gameStarted = false;
  let octShift    = false;

  function triggerKey(key) {
    if (pressedKeys.has(key)) return;
    pressedKeys.add(key);
    keyDomMap[key]?.classList.add('active');
    if (!gameStarted) return;

    const kd = ALL_KEYS[key];
    if (!kd || !ACTIVE_LANE_SET.has(kd.lane)) return;

    const freq     = octShift ? kd.freq * 2 : kd.freq;
    const noteName = octShift ? kd.note.replace(/(\d)$/, n => +n + 1) : kd.note;
    playPianoNote(freq);
    spawnNote(kd.lane);
    triggerGlow(kd.lane);
    bumpCombo();
    addScore(300 * Math.max(1, Math.floor(combo / 4)));
    showJudge('PERFECT!', '#00ffee');
    showNotePop(noteName, LANE_COLORS_HEX[kd.lane]);
    fx2d.spawn(getLaneScreenX(kd.lane), getHitLineScreenY(), LANE_COLORS_HEX[kd.lane]);
  }

  function releaseKey(key) {
    pressedKeys.delete(key);
    keyDomMap[key]?.classList.remove('active');
  }

  function handleKeyDown(key) {
    if (key === 'Shift') {
      octShift = true;
      octIndicator?.classList.add('active');
      return;
    }
    triggerKey(key.toLowerCase());
  }

  function handleKeyUp(key) {
    if (key === 'Shift') {
      octShift = false;
      octIndicator?.classList.remove('active');
      return;
    }
    releaseKey(key.toLowerCase());
  }

  // ─── 譜面ロード ──────────────────────────────
  function loadFumenData(data) {
    FUMEN          = data;
    ACTIVE_LANE_SET = new Set(FUMEN.notes.map(n => n.lane));
    ACTIVE_LANES    = [...ACTIVE_LANE_SET].sort((a, b) => a - b);
    ACTIVE_TOTAL    = ACTIVE_LANES.length;
    LANE_REMAP      = new Map(ACTIVE_LANES.map((orig, idx) => [orig, idx]));

    for (const n of activeNotes) scene.remove(n.mesh);
    activeNotes.length = 0;

    rebuildHighway();
    buildPianoUI();

    const statusEl = document.getElementById('fumen-status');
    if (statusEl) statusEl.textContent =
      '✓ ' + (FUMEN.title || 'fumen') + '  (' + FUMEN.notes.length + ' notes)';
    const autoBtn = document.getElementById('autoplay-btn');
    if (autoBtn) autoBtn.disabled = false;
  }

  // ─── BGM ─────────────────────────────────────
  let bgmBuffer = null;
  let bgmSource = null;

  function setBgmBuffer(buf) { bgmBuffer = buf; }

  function playBgm() {
    if (!bgmBuffer) return;
    const ctx = getAudioCtx();
    if (bgmSource) { try { bgmSource.stop(); } catch (e) {} }
    bgmSource = ctx.createBufferSource();
    bgmSource.buffer = bgmBuffer;
    bgmSource.connect(ctx.destination);
    bgmSource.start(ctx.currentTime);
  }

  // ─── オートプレイ ─────────────────────────────
  let autoplayMode  = false;
  let autoplayStart = null;
  let autoSpawnIdx  = 0;
  let autoAudioIdx  = 0;

  function hitAutoNoteEffects(note) {
    const kd = LANE_TO_KEY[note.lane];
    if (kd) playPianoNote(kd.freq);
    triggerGlow(note.lane);
    bumpCombo();
    addScore(300 * Math.max(1, Math.floor(combo / 4)));
    showJudge('PERFECT!', '#00ffee');
    showNotePop(note.note, LANE_COLORS_HEX[note.lane]);
    fx2d.spawn(getLaneScreenX(note.lane), getHitLineScreenY(), LANE_COLORS_HEX[note.lane]);
    if (kd && keyDomMap[kd.key]) {
      keyDomMap[kd.key].classList.add('active');
      setTimeout(() => keyDomMap[kd.key].classList.remove('active'), 120);
    }
  }

  function startAutoplay() {
    if (!FUMEN) return;
    getAudioCtx();
    gameStarted   = true;
    autoplayMode  = true;
    autoplayStart = performance.now();
    autoSpawnIdx  = 0;
    autoAudioIdx  = 0;
    document.getElementById('start-screen').style.display = 'none';
    playBgm();

    // 開始時点で既に飛行中のノーツを正しいZ位置から生成
    while (autoSpawnIdx < FUMEN.notes.length &&
           FUMEN.notes[autoSpawnIdx].time < TRAVEL_TIME) {
      const note = FUMEN.notes[autoSpawnIdx];
      spawnNoteAtZ(note.lane, NOTE_Z_HIT - NOTE_SPEED * note.time);
      autoSpawnIdx++;
    }
  }

  function start() {
    getAudioCtx();
    gameStarted = true;
    document.getElementById('start-screen').style.display = 'none';
  }

  // ─── pfxCanvas リサイズ ───────────────────────
  function resizePfx() {
    pfxCanvas.width  = innerWidth;
    pfxCanvas.height = innerHeight;
  }

  // ─── メインループ ─────────────────────────────
  let elapsed = 0;

  function update(dt) {
    // ノーツ移動
    for (let i = activeNotes.length - 1; i >= 0; i--) {
      const n = activeNotes[i];
      n.mesh.position.z += NOTE_SPEED * dt;
      if (n.mesh.position.z > NOTE_Z_DEAD) {
        scene.remove(n.mesh);
        activeNotes.splice(i, 1);
      }
    }

    // レーングロー
    for (let di = 0; di < ACTIVE_TOTAL; di++) {
      if (glowTimers[di] > 0) {
        glowTimers[di] -= dt * 3.5;
        laneGlowMats[di].mat.opacity = laneGlowMats[di].base + Math.max(0, glowTimers[di]) * 0.38;
      } else {
        laneGlowMats[di].mat.opacity = laneGlowMats[di].base;
      }
    }

    // HUD フェード
    if (judgeTimer  > 0) { judgeTimer  -= dt; if (judgeTimer  <= 0) judgeEl.style.opacity  = '0'; }
    if (notePopTimer > 0) { notePopTimer -= dt; if (notePopTimer <= 0) notePopEl.style.opacity = '0'; }

    // オートプレイスケジューラ
    if (autoplayMode && autoplayStart !== null && FUMEN) {
      const apElapsed = (performance.now() - autoplayStart) / 1000;

      while (autoSpawnIdx < FUMEN.notes.length &&
             FUMEN.notes[autoSpawnIdx].time - TRAVEL_TIME <= apElapsed) {
        spawnNoteAtZ(FUMEN.notes[autoSpawnIdx].lane, NOTE_Z_SPAWN);
        autoSpawnIdx++;
      }
      while (autoAudioIdx < FUMEN.notes.length &&
             FUMEN.notes[autoAudioIdx].time <= apElapsed) {
        hitAutoNoteEffects(FUMEN.notes[autoAudioIdx]);
        autoAudioIdx++;
      }

      const pct = Math.min(100, apElapsed / FUMEN.totalDuration * 100);
      if (autoBarFill) autoBarFill.style.width = pct + '%';
      if (autoTimerEl) autoTimerEl.textContent =
        fmtTime(apElapsed) + ' / ' + fmtTime(FUMEN.totalDuration);

      if (autoAudioIdx >= FUMEN.notes.length && apElapsed > FUMEN.totalDuration) {
        autoplayMode = false;
        if (autoBarFill) autoBarFill.style.width = '100%';
      }
    }

    // カメラドリフト
    if (gameStarted) {
      elapsed += dt;
      camera.position.x = Math.sin(elapsed * 0.21) * 0.16;
      camera.position.y = 5.5 + Math.sin(elapsed * 0.34) * 0.07;
      camera.lookAt(0, 0, -8);
    }

    if (hitLine3D) hitLine3D.material.opacity = 0.7 + Math.sin(Date.now() * 0.006) * 0.22;
    fx2d.update();
  }

  buildPianoUI();
  return {
    start, startAutoplay, update,
    handleKeyDown, handleKeyUp,
    loadFumenData, setBgmBuffer,
    buildPianoUI, resizePfx,
  };
}
