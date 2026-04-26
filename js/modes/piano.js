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

// ─── 定数 ────────────────────────────────────
const TOTAL_LANES  = 16;
const LANE_W       = 0.52;
const LANE_GAP     = 0.018;
const LANE_UNIT    = LANE_W + LANE_GAP;
const LANE_COLORS_HEX = [
  '#00ffee','#44ddff','#22bbff','#88aaff',
  '#aa66ff','#cc44ff','#ee22cc','#ff44aa',
  '#00ffbb','#44ffcc','#66ffdd','#88ffcc',
  '#aaffaa','#ccff77','#ffee44','#ffbb22',
];
const LANE_COLORS_INT = LANE_COLORS_HEX.map(c => parseInt(c.replace('#',''), 16));
const NOTE_Z_SPAWN = -30;
const NOTE_Z_HIT   =  4.2;
const NOTE_Z_DEAD  =  9.0;
const NOTE_SPEED   =  7.2;
const NOTE_H       = 0.2;

// ─── ファクトリ ──────────────────────────────
export function createPianoMode(scene, camera, pfxCanvas) {
  // 黒鍵レーンは opacity を少し落とす
  const baseLaneOpacities = Array.from({ length: TOTAL_LANES }, (_, i) =>
    BLACK_KEYS.some(b => b.lane === i) ? 0.055 : 0.08
  );

  const { laneGlowMats, hitLine3D, getLaneX } = buildHighway(scene, {
    numLanes:        TOTAL_LANES,
    laneSpacing:     LANE_UNIT,
    laneGlowWidth:   LANE_W - 0.04,
    laneColors:      LANE_COLORS_INT,
    baseLaneOpacity: baseLaneOpacities,
    highwayLen:      44,
    hitZ:            NOTE_Z_HIT,
    showSideBorders: false,
    showHitLights:   false,
  });

  // 星
  const starPos = new Float32Array(900 * 3);
  for (let i = 0; i < 900; i++) {
    starPos[i*3]   = (Math.random()-0.5)*130;
    starPos[i*3+1] = Math.random()*50 + 2;
    starPos[i*3+2] = (Math.random()-0.5)*130;
  }
  const starsGeo = new THREE.BufferGeometry();
  starsGeo.setAttribute('position', new THREE.BufferAttribute(starPos, 3));
  scene.add(new THREE.Points(starsGeo,
    new THREE.PointsMaterial({ color:0xaaddff, size:0.18, transparent:true, opacity:0.65 })
  ));

  // 2D パーティクル
  const fx2d = createParticleSystem2D(pfxCanvas);

  // アクティブノーツ
  const activeNotes = [];

  function spawnNote(laneIdx) {
    const x   = getLaneX(laneIdx);
    const col = LANE_COLORS_INT[laneIdx];
    const geo = new THREE.BoxGeometry(LANE_W - 0.05, NOTE_H, 0.52);
    const mat = new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: 0.93 });
    const mesh = new THREE.Mesh(geo, mat);
    mesh.add(new THREE.LineSegments(
      new THREE.EdgesGeometry(geo),
      new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.65 })
    ));
    const hl = new THREE.Mesh(
      new THREE.PlaneGeometry(LANE_W - 0.1, 0.07),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.55, side: THREE.DoubleSide })
    );
    hl.position.set(0, NOTE_H/2 + 0.001, 0);
    mesh.add(hl);
    mesh.position.set(x, NOTE_H/2, NOTE_Z_SPAWN);
    scene.add(mesh);
    activeNotes.push({ mesh });
  }

  // HUD
  const scoreEl    = document.getElementById('score-val');
  const comboNumEl = document.getElementById('combo-num');
  const comboLblEl = document.getElementById('combo-label');
  const judgeEl    = document.getElementById('judge-text');
  const notePopEl  = document.getElementById('note-name-pop');
  let judgeTimer   = 0, notePopTimer = 0;
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

  // レーングロータイマー
  const glowTimers = new Array(TOTAL_LANES).fill(0);
  function triggerGlow(i) { glowTimers[i] = 1.0; }

  // スクリーン座標（パーティクル用）
  function getLaneScreenX(i) {
    const v = new THREE.Vector3(getLaneX(i), 0, NOTE_Z_HIT);
    v.project(camera);
    return (v.x * 0.5 + 0.5) * innerWidth;
  }
  function getHitLineScreenY() {
    const v = new THREE.Vector3(0, 0, NOTE_Z_HIT);
    v.project(camera);
    return (v.y * -0.5 + 0.5) * innerHeight;
  }

  // ピアノ UI
  const keyDomMap = {};
  function buildPianoUI() {
    const whiteRow = document.getElementById('white-row');
    const blackRow = document.getElementById('black-row');
    whiteRow.innerHTML = ''; blackRow.innerHTML = '';
    const wPct  = 100 / WHITE_KEYS.length;
    const bwPct = wPct * 0.62;

    WHITE_KEYS.forEach(k => {
      const div = document.createElement('div');
      div.className = 'wkey';
      div.innerHTML = `<span class="klabel">${k.key.toUpperCase()}</span><span class="knote">${k.note}</span>`;
      whiteRow.appendChild(div);
      div.addEventListener('pointerdown',  e => { e.preventDefault(); triggerKey(k.key); });
      div.addEventListener('pointerup',    e => { e.preventDefault(); releaseKey(k.key); });
      div.addEventListener('pointerleave', e => { e.preventDefault(); releaseKey(k.key); });
      keyDomMap[k.key] = div;
    });

    BLACK_KEYS.forEach(k => {
      const div = document.createElement('div');
      div.className = 'bkey';
      div.innerHTML = `<span class="klabel">${k.key.toUpperCase()}</span><span class="knote">${k.note}</span>`;
      div.style.left     = `calc(${k.afterWhiteIdx * wPct}% - ${bwPct/2}%)`;
      div.style.width    = `${bwPct}%`;
      div.style.minWidth = '22px';
      blackRow.appendChild(div);
      div.addEventListener('pointerdown',  e => { e.preventDefault(); triggerKey(k.key); });
      div.addEventListener('pointerup',    e => { e.preventDefault(); releaseKey(k.key); });
      div.addEventListener('pointerleave', e => { e.preventDefault(); releaseKey(k.key); });
      keyDomMap[k.key] = div;
    });
  }

  // 入力
  const pressedKeys = new Set();
  let gameStarted = false;

  function triggerKey(key) {
    if (pressedKeys.has(key)) return;
    pressedKeys.add(key);
    keyDomMap[key]?.classList.add('active');
    if (!gameStarted) return;
    const kd = ALL_KEYS[key];
    if (!kd) return;
    playPianoNote(kd.freq);
    spawnNote(kd.lane);
    triggerGlow(kd.lane);
    bumpCombo();
    addScore(300 * Math.max(1, Math.floor(combo / 4)));
    showJudge('PERFECT!', '#00ffee');
    showNotePop(kd.note, LANE_COLORS_HEX[kd.lane]);
    fx2d.spawn(getLaneScreenX(kd.lane), getHitLineScreenY(), LANE_COLORS_HEX[kd.lane]);
  }

  function releaseKey(key) {
    pressedKeys.delete(key);
    keyDomMap[key]?.classList.remove('active');
  }

  function handleKeyDown(key) { triggerKey(key.toLowerCase()); }
  function handleKeyUp(key)   { releaseKey(key.toLowerCase()); }

  function start() {
    gameStarted = true;
    document.getElementById('start-screen').style.display = 'none';
  }

  // pfxCanvas リサイズ
  function resizePfx() {
    pfxCanvas.width  = innerWidth;
    pfxCanvas.height = innerHeight;
  }

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
    for (let i = 0; i < TOTAL_LANES; i++) {
      if (glowTimers[i] > 0) {
        glowTimers[i] -= dt * 3.5;
        laneGlowMats[i].mat.opacity = laneGlowMats[i].base + Math.max(0, glowTimers[i]) * 0.38;
      } else {
        laneGlowMats[i].mat.opacity = laneGlowMats[i].base;
      }
    }

    // HUD フェード
    if (judgeTimer  > 0) { judgeTimer  -= dt; if (judgeTimer  <= 0) judgeEl.style.opacity  = '0'; }
    if (notePopTimer > 0) { notePopTimer -= dt; if (notePopTimer <= 0) notePopEl.style.opacity = '0'; }

    // カメラドリフト
    if (gameStarted) {
      elapsed += dt;
      camera.position.x = Math.sin(elapsed * 0.21) * 0.16;
      camera.position.y = 5.5 + Math.sin(elapsed * 0.34) * 0.07;
      camera.lookAt(0, 0, -8);
    }

    hitLine3D.material.opacity = 0.7 + Math.sin(Date.now() * 0.006) * 0.22;
    fx2d.update();
  }

  buildPianoUI();
  return { start, update, handleKeyDown, handleKeyUp, buildPianoUI, resizePfx };
}
