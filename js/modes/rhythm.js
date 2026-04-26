import { buildHighway }              from '../core/highway.js';
import { createParticleSystem3D }    from '../core/particles.js';
import { getAudioCtx, playRhythmSound } from '../core/audio.js';

// ─── 定数 ───────────────────────────────────
const NUM_LANES    = 12;
const LANE_COLORS  = [
  0x00c8ff, 0x0074ff, 0x8a2be2, 0xcc44ff, 0xff2d78, 0xff6600,
  0xff9500, 0xd4ff00, 0x88ff00, 0x00ff88, 0x00e0c8, 0x00aaff,
];
const KEYS_MAP     = { a:[0,1], s:[2,3], d:[4,5], j:[6,7], k:[8,9], l:[10,11] };
const KEY_LABELS   = ['A','S','D','J','K','L'];
const LANE_SPACING = 0.85;
const NOTE_Z_SPAWN = -95;
const NOTE_Z_HIT   =  4.2;
const NOTE_SPEED   = 36;
const HIT_Z_PERF   =  2.2;
const HIT_Z_GOOD   =  4.2;
const LONG_TICK_MS = 100;
const NOTE_COLOR   = 0xff55aa;
const LONG_COLOR   = 0x44ffcc;
const MIN_GAP      = 165;

// ─── ファクトリ ─────────────────────────────
export function createRhythmMode(scene, camera) {
  // ハイウェイ構築
  const { laneGlowMats, hitLine3D, hitLights, getLaneX } = buildHighway(scene, {
    numLanes:       NUM_LANES,
    laneSpacing:    LANE_SPACING,
    laneGlowWidth:  LANE_SPACING * 0.88,
    laneColors:     LANE_COLORS,
    highwayLen:     130,
    hitZ:           NOTE_Z_HIT,
    showSideBorders: true,
    showHitLights:   true,
  });

  // ライト
  scene.add(new THREE.AmbientLight(0x081020, 1.3));
  const sun = new THREE.DirectionalLight(0xffffff, .45);
  sun.position.set(0, 12, 6);
  scene.add(sun);

  // パーティクル
  const fx = createParticleSystem3D(scene, NOTE_Z_HIT);

  // キーラベル DOM 構築
  const keylabelsEl = document.getElementById('keylabels');
  KEY_LABELS.forEach((label, i) => {
    const d = document.createElement('div');
    d.className = 'klabel'; d.id = 'kl' + i; d.textContent = label;
    keylabelsEl.appendChild(d);
  });

  // DOM 参照
  const overlayEl    = document.getElementById('overlay');
  const judgmentEl   = document.getElementById('judgment');
  const scoreEl      = document.getElementById('score');
  const accEl        = document.getElementById('acc');
  const comboEl      = document.getElementById('combo');
  const comboWrap    = document.getElementById('combo-wrap');
  const progEl       = document.getElementById('prog');
  const judgeLineEl  = document.getElementById('judge-line');

  // ゲーム状態
  let gameActive = false, autoPlay = false, songStart = 0, songDuration = 110000;
  let score = 0, combo = 0, maxCombo = 0, totalNotes = 0, hitNotes = 0;
  let beatmap = [], beatmapIdx = 0, judgeTimer = null;
  const pressedLanes = new Array(NUM_LANES).fill(false);
  const activeNotes  = [];

  // ─── ノーツ生成 ───────────────────────────
  function getNormalNote(lanes) {
    const xs      = lanes.map(l => getLaneX(l));
    const centerX = (Math.min(...xs) + Math.max(...xs)) / 2;
    const minW    = LANE_SPACING * 3.0 * 0.88;
    const spanW   = Math.max((Math.max(...xs) - Math.min(...xs)) + LANE_SPACING * 0.88, minW);
    const H = .18, D = .5;
    const g = new THREE.Group();

    g.add(new THREE.Mesh(
      new THREE.BoxGeometry(spanW, H, D),
      new THREE.MeshStandardMaterial({
        color: NOTE_COLOR, emissive: NOTE_COLOR, emissiveIntensity: .7,
        roughness: .2, metalness: .6, transparent: true, opacity: .82,
      })
    ));
    g.add(new THREE.LineSegments(
      new THREE.EdgesGeometry(new THREE.BoxGeometry(spanW, H, D)),
      new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: .9 })
    ));
    const topBar = new THREE.Mesh(
      new THREE.BoxGeometry(spanW * .96, .03, D * .92),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: .55 })
    );
    topBar.position.y = H / 2 + .015;
    g.add(topBar);

    g.position.set(centerX, .16, NOTE_Z_SPAWN);
    g._spanW = spanW;
    scene.add(g);
    return g;
  }

  function releaseNormal(m) { scene.remove(m); }

  function makeLongNote(lanes, lenUnits) {
    const xs      = lanes.map(l => getLaneX(l));
    const centerX = (Math.min(...xs) + Math.max(...xs)) / 2;
    const minWL   = LANE_SPACING * 3.0 * 0.88;
    const W       = Math.max((Math.max(...xs) - Math.min(...xs)) + LANE_SPACING * 0.88, minWL);
    const g       = new THREE.Group();

    const headMesh = new THREE.Mesh(
      new THREE.BoxGeometry(W, .2, .52),
      new THREE.MeshStandardMaterial({
        color: LONG_COLOR, emissive: LONG_COLOR, emissiveIntensity: .8,
        roughness: .15, metalness: .55, transparent: true, opacity: .85,
      })
    );
    g.add(headMesh);
    g.add(new THREE.LineSegments(
      new THREE.EdgesGeometry(new THREE.BoxGeometry(W, .2, .52)),
      new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: .9 })
    ));
    const headTop = new THREE.Mesh(
      new THREE.BoxGeometry(W * .96, .03, .48),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: .5 })
    );
    headTop.position.set(0, .115, 0);
    g.add(headTop);

    const bodyMat  = new THREE.MeshStandardMaterial({
      color: LONG_COLOR, emissive: LONG_COLOR, emissiveIntensity: .5,
      roughness: .35, metalness: .4, transparent: true, opacity: .7,
    });
    const bodyMesh = new THREE.Mesh(new THREE.BoxGeometry(W * .7, .12, lenUnits), bodyMat);
    bodyMesh.position.z = -lenUnits / 2;
    g.add(bodyMesh);

    const tailMesh = new THREE.Mesh(
      new THREE.BoxGeometry(W, .16, .35),
      new THREE.MeshStandardMaterial({
        color: LONG_COLOR, emissive: LONG_COLOR, emissiveIntensity: .75,
        roughness: .2, metalness: .5,
      })
    );
    tailMesh.position.z = -lenUnits;
    g.add(tailMesh);

    g.position.set(centerX, .16, NOTE_Z_SPAWN);
    scene.add(g);
    return { group: g, headMesh, bodyMesh, bodyMat, tailMesh, lenUnits, centerX };
  }

  function releaseLong(parts) {
    scene.remove(parts.group);
    parts.group.children.forEach(c => {
      c.geometry?.dispose();
      c.material?.dispose();
    });
  }

  // ─── 星（動画なし時の背景） ────────────────
  function setupFallbackBg() {
    const n = 2800, pos = new Float32Array(n * 3);
    for (let i = 0; i < n * 3; i++) pos[i] = (Math.random() - .5) * 230;
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    scene.add(new THREE.Points(geo,
      new THREE.PointsMaterial({ color: 0xffffff, size: .18, transparent: true, opacity: .5 })
    ));
  }

  // ─── 譜面ロード ──────────────────────────
  let chartNotes = [], chartDuration = 110000;

  function loadChart(chart) {
    chartDuration = chart.meta?.duration ?? chart.notes.at(-1).t + 5000;
    // レーン競合フィルタ
    const laneEndTime = new Array(NUM_LANES).fill(0);
    const bm = [];
    for (const e of chart.notes) {
      if (e.lanes.every(l => laneEndTime[l] <= e.t)) {
        bm.push(e);
        e.lanes.forEach(l => { laneEndTime[l] = e.t + (e.holdMs || 0) + MIN_GAP; });
      }
    }
    chartNotes = bm.sort((a, b) => a.t - b.t);
  }

  // ─── HUD ────────────────────────────────
  function showJudge(text, color) {
    const hex = '#' + new THREE.Color(color).getHexString();
    judgmentEl.textContent  = text;
    judgmentEl.style.color  = hex;
    judgmentEl.style.textShadow = `0 0 18px ${hex}`;
    judgmentEl.style.opacity = 1;
    if (judgeTimer) clearTimeout(judgeTimer);
    judgeTimer = setTimeout(() => judgmentEl.style.opacity = 0, text === 'HOLD' ? 80 : 360);
  }

  function updateHUD() {
    scoreEl.textContent = String(score).padStart(6, '0');
    comboWrap.style.display = combo > 0 ? 'block' : 'none';
    comboEl.textContent = combo;
    const a = totalNotes > 0 ? (hitNotes / totalNotes * 100).toFixed(1) : '—';
    accEl.textContent = 'ACC ' + a + '%';
  }

  function flashScore() {
    scoreEl.style.transform = 'scale(1.12)';
    setTimeout(() => scoreEl.style.transform = '', 80);
  }

  function flashButton(i) {
    playRhythmSound('tap');
    hitLights[i].intensity = 5;
    laneGlowMats[i].mat.opacity = .22;
    const kl = document.getElementById('kl' + Math.floor(i / 2));
    kl?.classList.add('active');
    setTimeout(() => {
      hitLights[i].intensity = 0;
      laneGlowMats[i].mat.opacity = laneGlowMats[i].base;
      kl?.classList.remove('active');
    }, 110);
  }

  // ─── ヒット判定 ──────────────────────────
  function tryHit(lane) {
    if (!gameActive) return;
    flashButton(lane);

    // ロングノーツの頭判定
    for (const n of activeNotes) {
      if (n.type !== 'long' || n.state !== 'incoming' || !n.lanes.includes(lane)) continue;
      const dist = Math.abs(n.parts.group.position.z - NOTE_Z_HIT);
      if (dist <= HIT_Z_GOOD) {
        n.state = 'holding'; n.tickAcc = 0;
        hitNotes++;
        const perf = dist <= HIT_Z_PERF;
        combo++; if (combo > maxCombo) maxCombo = combo;
        score += (perf ? 300 : 100) * Math.max(1, Math.floor(combo / 4));
        playRhythmSound('hit');
        showJudge(perf ? 'PERFECT!' : 'GOOD', perf ? LANE_COLORS[lane] : 0x999999);
        updateHUD(); flashScore();
        fx.spawn(getLaneX(lane), LANE_COLORS[lane], 10);
        return;
      }
    }

    // 通常ノーツ
    let best = null, bestDist = Infinity;
    for (const n of activeNotes) {
      if (n.type !== 'normal' || !n.lanes.includes(lane)) continue;
      const d = Math.abs(n.mesh.position.z - NOTE_Z_HIT);
      if (d < bestDist) { bestDist = d; best = n; }
    }
    if (!best || bestDist > HIT_Z_GOOD) return;
    activeNotes.splice(activeNotes.indexOf(best), 1);
    fx.spawnAscend(best.mesh, best.mesh._spanW ?? LANE_SPACING * 3 * 0.88, NOTE_COLOR);
    releaseNormal(best.mesh);
    fx.spawn(getLaneX(lane), NOTE_COLOR);
    hitNotes++;
    const perf = bestDist <= HIT_Z_PERF;
    combo++; if (combo > maxCombo) maxCombo = combo;
    score += (perf ? 300 : 100) * Math.max(1, Math.floor(combo / 4));
    playRhythmSound('hit');
    showJudge(perf ? 'PERFECT!' : 'GOOD', perf ? LANE_COLORS[lane] : 0x999999);
    updateHUD(); flashScore();
  }

  // ─── ロングノーツ更新 ─────────────────────
  function updateLongNotes(delta) {
    for (let i = activeNotes.length - 1; i >= 0; i--) {
      const n = activeNotes[i];
      if (n.type !== 'long') continue;
      const g     = n.parts.group;
      g.position.z += NOTE_SPEED * delta;
      const headZ  = g.position.z;
      const tailZ  = headZ - n.parts.lenUnits;
      const laneIdx = n.lanes[0];

      if (n.state === 'incoming') {
        if (headZ >= NOTE_Z_HIT - HIT_Z_GOOD && n.lanes.some(l => pressedLanes[l])) {
          n.state = 'holding'; n.tickAcc = 0;
          hitNotes++;
          combo++; if (combo > maxCombo) maxCombo = combo;
          score += 100 * Math.max(1, Math.floor(combo / 4));
          showJudge('GOOD', 0x999999);
          updateHUD(); flashScore();
        }
        if (tailZ > NOTE_Z_HIT + HIT_Z_GOOD + 1) {
          releaseLong(n.parts); activeNotes.splice(i, 1);
          combo = 0; showJudge('MISS', 0xff8866); updateHUD();
        }

      } else if (n.state === 'holding') {
        hitLights[laneIdx].intensity = 3.5;
        laneGlowMats[laneIdx].mat.opacity = .16;

        n.tickAcc = (n.tickAcc || 0) + delta * 1000;
        if (n.tickAcc >= LONG_TICK_MS) {
          n.tickAcc -= LONG_TICK_MS;
          combo++; if (combo > maxCombo) maxCombo = combo;
          score += 10 * Math.max(1, Math.floor(combo / 4));
          showJudge('HOLD', LANE_COLORS[laneIdx]);
          updateHUD(); flashScore();
          fx.spawn(getLaneX(laneIdx), LANE_COLORS[laneIdx], 3);
        }

        if (headZ > NOTE_Z_HIT) {
          const consumed  = headZ - NOTE_Z_HIT;
          const remaining = Math.max(0, n.parts.lenUnits - consumed);
          if (remaining > 0.05) {
            n.parts.bodyMesh.scale.z    = remaining / n.parts.lenUnits;
            n.parts.bodyMesh.position.z = -(consumed + remaining / 2);
          }
          n.parts.headMesh.visible = headZ < NOTE_Z_HIT + 0.6;

          if (tailZ >= NOTE_Z_HIT - 0.3) {
            releaseLong(n.parts); activeNotes.splice(i, 1);
            combo++; if (combo > maxCombo) maxCombo = combo;
            score += 300 * Math.max(1, Math.floor(combo / 4));
            hitNotes++;
            playRhythmSound('hit');
            showJudge('PERFECT!', LANE_COLORS[laneIdx]);
            updateHUD(); flashScore();
            fx.spawn(getLaneX(laneIdx), LANE_COLORS[laneIdx], 24);
            hitLights[laneIdx].intensity = 0;
            laneGlowMats[laneIdx].mat.opacity = laneGlowMats[laneIdx].base;
            continue;
          }
        }

      } else {
        releaseLong(n.parts); activeNotes.splice(i, 1);
      }
    }
  }

  // ─── 終了 ────────────────────────────────
  function endGame() {
    gameActive = false;
    judgeLineEl.style.display = 'none';
    const acc = totalNotes > 0 ? (hitNotes / totalNotes * 100).toFixed(1) : '0.0';
    overlayEl.innerHTML = `
      <h1>RESULT</h1><p>COMPLETE</p>
      <div id="result-table">
        SCORE &nbsp;&nbsp; ${String(score).padStart(6, '0')}<br>
        MAX COMBO &nbsp;&nbsp; ${maxCombo}<br>
        ACCURACY &nbsp;&nbsp; ${acc}%<br>
        NOTES HIT &nbsp;&nbsp; ${hitNotes} / ${totalNotes}
      </div>
      <button class="ov-btn" onclick="location.reload()">RETRY</button>
    `;
    overlayEl.classList.remove('hidden');
  }

  // ─── 開始 ────────────────────────────────
  function start(auto = false) {
    overlayEl.classList.add('hidden');
    getAudioCtx();
    autoPlay  = auto;
    gameActive = true;
    judgeLineEl.style.display = 'block';
    score = 0; combo = 0; maxCombo = 0; totalNotes = 0; hitNotes = 0;
    beatmap    = [...chartNotes];
    beatmapIdx = 0;
    songStart  = performance.now();
    updateHUD();
    setTimeout(endGame, chartDuration);
  }

  // ─── フレーム更新 ────────────────────────
  function update(delta) {
    if (!gameActive) return;
    const elapsed = performance.now() - songStart;

    progEl.style.width = Math.min(100, elapsed / chartDuration * 100) + '%';

    // スポーン
    const travelMs = (NOTE_Z_HIT - NOTE_Z_SPAWN) / NOTE_SPEED * 1000;
    while (beatmapIdx < beatmap.length && elapsed < chartDuration - 3000) {
      const e = beatmap[beatmapIdx];
      if (elapsed >= e.t - travelMs) {
        if (e.isLong) {
          const lenUnits = e.holdMs / 1000 * NOTE_SPEED;
          const parts    = makeLongNote(e.lanes, lenUnits);
          activeNotes.push({ type:'long', lanes:e.lanes, parts, state:'incoming', tickAcc:0 });
          totalNotes++;
        } else {
          const mesh = getNormalNote(e.lanes);
          activeNotes.push({ type:'normal', lanes:e.lanes, mesh });
          totalNotes++;
        }
        beatmapIdx++;
      } else break;
    }

    // オートプレイ
    if (autoPlay) {
      for (const n of [...activeNotes]) {
        if (n.type === 'normal') {
          if (Math.abs(n.mesh.position.z - NOTE_Z_HIT) <= HIT_Z_PERF)
            n.lanes.forEach(l => tryHit(l));
        } else if (n.type === 'long' && n.state === 'incoming') {
          if (Math.abs(n.parts.group.position.z - NOTE_Z_HIT) <= HIT_Z_PERF)
            n.lanes.forEach(l => tryHit(l));
        }
      }
    }

    updateLongNotes(delta);
    fx.update(delta);

    // 通常ノーツ移動 + MISS
    for (let i = activeNotes.length - 1; i >= 0; i--) {
      const n = activeNotes[i];
      if (n.type !== 'normal') continue;
      n.mesh.position.z += NOTE_SPEED * delta;
      if (n.mesh.position.z > NOTE_Z_HIT + HIT_Z_GOOD + 1.2) {
        releaseNormal(n.mesh); activeNotes.splice(i, 1);
        combo = 0; showJudge('MISS', 0xff8866); updateHUD();
      }
    }

    // カメラドリフト
    camera.position.x = Math.sin(elapsed * .00022) * .14;
    camera.position.y = 5.5 + Math.sin(elapsed * .0006) * .04;

    // レーングローパルス
    const t2 = performance.now() / 1000;
    laneGlowMats.forEach((g, i) => {
      const pulse = (Math.sin(t2 * 2.2 + i * .4) * .5 + .5) * .018 + .055;
      if (!pressedLanes[i]) g.mat.opacity = Math.max(g.mat.opacity * .87, pulse);
    });

    hitLine3D.material.opacity = 0.7 + Math.sin(Date.now() * 0.006) * 0.22;
  }

  // ─── 入力 ────────────────────────────────
  function handleKeyDown(key) {
    const lanes = KEYS_MAP[key.toLowerCase()];
    if (!lanes) return;
    const keyIdx = Object.keys(KEYS_MAP).indexOf(key.toLowerCase());
    document.getElementById('kl' + keyIdx)?.classList.add('active');
    lanes.forEach(l => { pressedLanes[l] = true; tryHit(l); });
  }

  function handleKeyUp(key) {
    const lanes = KEYS_MAP[key.toLowerCase()];
    if (!lanes) return;
    const keyIdx = Object.keys(KEYS_MAP).indexOf(key.toLowerCase());
    document.getElementById('kl' + keyIdx)?.classList.remove('active');
    lanes.forEach(l => { pressedLanes[l] = false; });
  }

  return { loadChart, start, update, handleKeyDown, handleKeyUp, setupFallbackBg };
}
