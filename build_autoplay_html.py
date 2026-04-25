"""
PULSE3D_play.html をベースに PULSE3D_play_auto.html を生成する。

機能:
  - 譜面は外部 JSON ファイルをファイルピッカーで読み込む（曲ごとに差し替え可能）
  - BGM ファイル（no_vocals.wav など）もファイルピッカーで読み込む
  - 使用レーンのみ3Dハイウェイに表示（fumen ロード時に動的構築）
  - 未使用キーをグレーアウト＋無効化
  - ノーツ色を白鍵/黒鍵で区別
  - AUTOPLAY: ノーツ生成を TRAVEL_TIME 先行、音はヒット時刻に同期
"""
import json
from pathlib import Path

SRC_HTML = Path("PULSE3D_play.html")
OUT_HTML = Path("PULSE3D_play_auto.html")

html = SRC_HTML.read_text()

# ════════════════════════════════════════════════════════
# 1. CSS 追加
# ════════════════════════════════════════════════════════
EXTRA_CSS = """
/* 未使用キー */
.wkey.disabled{
  opacity:0.22;cursor:default;pointer-events:none;
  background:linear-gradient(to bottom,#1a2a38 0%,#223344 100%);
  box-shadow:none;border-color:rgba(40,80,110,0.3);
}
.bkey.disabled{
  opacity:0.18;cursor:default;pointer-events:none;
  background:linear-gradient(to bottom,#060c12 0%,#0a1018 100%);
  box-shadow:none;border-color:rgba(20,50,70,0.2);
}
/* オートプレイ進捗 */
#autoplay-bar{
  position:fixed;bottom:25vh;left:0;width:100%;height:3px;z-index:16;
  background:rgba(0,255,238,0.12);pointer-events:none;
}
#autoplay-bar-fill{
  height:100%;background:linear-gradient(to right,#0ff,#88f);
  width:0%;box-shadow:0 0 8px #0ff;
}
#autoplay-timer{
  position:fixed;bottom:calc(25vh + 8px);right:8px;
  font-family:'Share Tech Mono',monospace;font-size:12px;
  color:rgba(0,255,238,0.6);z-index:16;pointer-events:none;
  letter-spacing:2px;
}
/* ファイル選択セクション */
.file-section{
  display:flex;flex-direction:column;align-items:center;gap:5px;
  width:100%;max-width:420px;
}
.file-section-label{
  font-family:'Share Tech Mono',monospace;
  font-size:clamp(8px,1.1vw,11px);color:rgba(255,255,255,0.35);
  letter-spacing:3px;text-transform:uppercase;
}
.file-pick-btn{
  padding:7px 22px;font-family:'Orbitron',sans-serif;
  font-size:clamp(9px,1.3vw,13px);letter-spacing:3px;
  border:1px solid rgba(0,255,238,0.4);background:rgba(0,60,100,0.25);
  color:#88eeff;cursor:pointer;border-radius:3px;transition:all 0.2s;width:100%;
}
.file-pick-btn:hover{background:rgba(0,100,150,0.4);border-color:#0ff;}
.file-status{
  font-family:'Share Tech Mono',monospace;
  font-size:clamp(8px,1.1vw,11px);color:rgba(0,255,200,0.55);
  letter-spacing:1px;min-height:1em;
}
.ss-or{
  font-family:'Share Tech Mono',monospace;
  font-size:clamp(9px,1.2vw,13px);color:rgba(255,255,255,0.3);letter-spacing:3px;
}
#autoplay-btn{
  padding:14px 52px;font-family:'Orbitron',sans-serif;
  font-size:clamp(14px,2.2vw,22px);font-weight:700;letter-spacing:5px;
  border:2px solid #88f;background:transparent;color:#88f;cursor:pointer;
  border-radius:4px;transition:all 0.2s;
}
#autoplay-btn:disabled{opacity:0.3;cursor:default;}
#autoplay-btn:not(:disabled):hover{background:#88f;color:#000;box-shadow:0 0 28px #88f;}
"""
OCT_CSS = """
/* オクターブシフト表示 */
#oct-indicator{
  position:fixed;top:14px;right:20px;z-index:11;
  font-family:'Share Tech Mono',monospace;font-size:clamp(11px,1.8vw,18px);
  letter-spacing:3px;color:rgba(255,200,80,0.0);
  text-shadow:0 0 12px #fa0;transition:color 0.1s;pointer-events:none;
}
#oct-indicator.active{color:rgba(255,200,80,0.95);}
"""
html = html.replace("</style>", EXTRA_CSS + OCT_CSS + "\n</style>")

# ════════════════════════════════════════════════════════
# 2. HUD にプログレスバーとタイマー追加
# ════════════════════════════════════════════════════════
html = html.replace(
    '<div id="keyboard-ui">',
    '<div id="autoplay-bar"><div id="autoplay-bar-fill"></div></div>\n'
    '<div id="autoplay-timer"></div>\n'
    '<div id="oct-indicator">OCT +1</div>\n'
    '<div id="keyboard-ui">'
)

# ════════════════════════════════════════════════════════
# 3. スタートスクリーンにファイルピッカーと AUTOPLAY ボタン
# ════════════════════════════════════════════════════════
html = html.replace(
    '<button id="start-btn">▶ START</button>',
    '<button id="start-btn">▶ START</button>\n'
    '  <div class="ss-or">— or —</div>\n'
    '  <div class="file-section">\n'
    '    <div class="file-section-label">譜面ファイル</div>\n'
    '    <input type="file" id="fumen-input" accept=".json" style="display:none">\n'
    '    <button class="file-pick-btn" id="fumen-file-btn">♩ 譜面 JSON を選択</button>\n'
    '    <div class="file-status" id="fumen-status">未選択</div>\n'
    '  </div>\n'
    '  <div class="file-section">\n'
    '    <div class="file-section-label">BGM ファイル（任意）</div>\n'
    '    <input type="file" id="bgm-input" accept="audio/*" style="display:none">\n'
    '    <button class="file-pick-btn" id="bgm-file-btn">♪ 伴奏 audio を選択</button>\n'
    '    <div class="file-status" id="bgm-status">未選択（なしでも再生可）</div>\n'
    '  </div>\n'
    '  <button id="autoplay-btn" disabled>▶ AUTOPLAY</button>'
)

# ════════════════════════════════════════════════════════
# 4. レーン色を白鍵/黒鍵の二色に変更
# ════════════════════════════════════════════════════════
html = html.replace(
    "const LANE_COLORS_HEX = [\n"
    "  '#00ffee','#44ddff','#22bbff','#88aaff',\n"
    "  '#aa66ff','#cc44ff','#ee22cc','#ff44aa',\n"
    "  '#00ffbb','#44ffcc','#66ffdd','#88ffcc',\n"
    "  '#aaffaa','#ccff77','#ffee44','#ffbb22',\n"
    "];",
    "// 白鍵: アイスブルー / 黒鍵: ネイビー\n"
    "const _BLACK_LANE_SET_COLOR = new Set(BLACK_KEYS.map(k => k.lane));\n"
    "const LANE_COLORS_HEX = Array.from({length:16}, (_,i) =>\n"
    "  _BLACK_LANE_SET_COLOR.has(i) ? '#1a3a5a' : '#c8e8ff'\n"
    ");"
)

# ════════════════════════════════════════════════════════
# 5. TOTAL_LANES の後に動的変数を let で宣言
#    （fumen ロード前は空で OK）
# ════════════════════════════════════════════════════════
html = html.replace(
    "const TOTAL_LANES = 16;\n",
    "const TOTAL_LANES = 16;\n"
    "\n"
    "// fumen ロード後に確定する動的変数\n"
    "let FUMEN          = null;\n"
    "let ACTIVE_LANE_SET = new Set();\n"
    "let ACTIVE_LANES    = [];\n"
    "let ACTIVE_TOTAL    = 0;\n"
    "let LANE_REMAP      = new Map();\n"
)

# ════════════════════════════════════════════════════════
# 6. totalHWWidth を定数から削除（buildHighway 内で計算）
# ════════════════════════════════════════════════════════
html = html.replace(
    "const totalHWWidth = TOTAL_LANES * LANE_UNIT;\n",
    ""
)

# ════════════════════════════════════════════════════════
# 7. ハイウェイ構築コードを buildHighway() 関数にまとめる
#    hwGroup, LANE_W 等の定数は外に残す
# ════════════════════════════════════════════════════════
# laneGlowMats は外で let 宣言してから buildHighway 内でリセット
html = html.replace(
    "// 各レーン帯\nconst laneGlowMats = [];",
    "// 各レーン帯\nlaneGlowMats = new Map();"
)
html = html.replace(
    "  laneGlowMats.push({mat, base: isBlack ? 0.055 : 0.08});",
    "  laneGlowMats.set(i, {mat, base: isBlack ? 0.055 : 0.08});"
)

HIGHWAY_OPEN = "// 床\nconst floorMesh"
HIGHWAY_OPEN_REPLACEMENT = "function buildHighway(){\n  const totalHWWidth = ACTIVE_TOTAL * LANE_UNIT;\n  // 古いメッシュを削除\n  while(hwGroup.children.length>0) hwGroup.remove(hwGroup.children[0]);\n  laneGlowMats = new Map();\n\n  // 床\n  const floorMesh"

html = html.replace(HIGHWAY_OPEN, HIGHWAY_OPEN_REPLACEMENT)

# hitLine3D の後（hwGroup.add(hitLine3D);）で関数を閉じる
html = html.replace(
    "hwGroup.add(hitLine3D);\n\n// 星",
    "hwGroup.add(hitLine3D);\n}\n\nlet laneGlowMats = new Map();\nlet hitLine3D = null;\n\n// 星"
)

# hitLine3D を const から let に（関数内で再代入するため）
html = html.replace(
    "  const hitLine3D = new THREE.Mesh(\n"
    "    new THREE.PlaneGeometry(totalHWWidth + 0.6, 0.07),",
    "  hitLine3D = new THREE.Mesh(\n"
    "    new THREE.PlaneGeometry(totalHWWidth + 0.6, 0.07),"
)
html = html.replace(
    "  const floorMesh = new THREE.Mesh(\n"
    "    new THREE.PlaneGeometry(totalHWWidth + 0.6, HIGHWAY_LEN),",
    "  const floorMesh = new THREE.Mesh(\n"
    "    new THREE.PlaneGeometry(totalHWWidth + 0.6, HIGHWAY_LEN),"
)

# ════════════════════════════════════════════════════════
# 8. getLaneX を LANE_REMAP 経由に
# ════════════════════════════════════════════════════════
html = html.replace(
    "function getLaneX(laneIdx){\n"
    "  return (laneIdx - (TOTAL_LANES-1)/2) * LANE_UNIT;\n"
    "}",
    "function getLaneX(laneIdx){\n"
    "  const di = LANE_REMAP.has(laneIdx) ? LANE_REMAP.get(laneIdx) : laneIdx;\n"
    "  return (di - (ACTIVE_TOTAL-1)/2) * LANE_UNIT;\n"
    "}"
)

# ════════════════════════════════════════════════════════
# 9. glowTimers を let + Map に変更
# ════════════════════════════════════════════════════════
html = html.replace(
    "const glowTimers = new Array(TOTAL_LANES).fill(0);\n"
    "function triggerGlow(laneIdx){ glowTimers[laneIdx]=1.0; }",
    "let glowTimers = new Map();\n"
    "function triggerGlow(laneIdx){ glowTimers.set(laneIdx,1.0); }"
)

# animate 内のグロー更新ループ
html = html.replace(
    "  // レーングロー\n"
    "  for(let i=0;i<TOTAL_LANES;i++){\n"
    "    if(glowTimers[i]>0){\n"
    "      glowTimers[i]-=dt*3.5;\n"
    "      laneGlowMats[i].mat.opacity = laneGlowMats[i].base + Math.max(0,glowTimers[i])*0.38;\n"
    "    } else {\n"
    "      laneGlowMats[i].mat.opacity = laneGlowMats[i].base;\n"
    "    }\n"
    "  }",
    "  // レーングロー\n"
    "  for(const i of ACTIVE_LANES){\n"
    "    const gm = laneGlowMats.get(i);\n"
    "    if(!gm) continue;\n"
    "    let gt = glowTimers.get(i)||0;\n"
    "    if(gt>0){\n"
    "      gt = Math.max(0, gt-dt*3.5);\n"
    "      glowTimers.set(i, gt);\n"
    "      gm.mat.opacity = gm.base + gt*0.38;\n"
    "    } else {\n"
    "      gm.mat.opacity = gm.base;\n"
    "    }\n"
    "  }"
)

# ════════════════════════════════════════════════════════
# 10. animate 内の hitLine3D を null ガード
#     ※ AUTOPLAY_TICK 挿入より先に行うと検索文字列が変わるため
#     　 AUTOPLAY_TICK 側で一緒に対処する（このステップは何もしない）
# ════════════════════════════════════════════════════════

# ════════════════════════════════════════════════════════
# 11. buildPianoUI で未使用キーに disabled クラス付与
# ════════════════════════════════════════════════════════
html = html.replace(
    "    div.className='wkey';\n"
    "    div.innerHTML=`<span class=\"klabel\">${k.key.toUpperCase()}</span><span class=\"knote\">${k.note}</span>`;\n"
    "    whiteRow.appendChild(div);\n"
    "    div.addEventListener('pointerdown',e=>{e.preventDefault();triggerKey(k.key);});\n"
    "    div.addEventListener('pointerup',  e=>{e.preventDefault();releaseKey(k.key);});\n"
    "    div.addEventListener('pointerleave',e=>{e.preventDefault();releaseKey(k.key);});\n"
    "    keyDomMap[k.key]=div;",
    "    const wDisabled = !ACTIVE_LANE_SET.has(k.lane);\n"
    "    div.className = wDisabled ? 'wkey disabled' : 'wkey';\n"
    "    div.innerHTML=`<span class=\"klabel\">${k.key.toUpperCase()}</span><span class=\"knote\">${k.note}</span>`;\n"
    "    whiteRow.appendChild(div);\n"
    "    if(!wDisabled){\n"
    "      div.addEventListener('pointerdown',e=>{e.preventDefault();triggerKey(k.key);});\n"
    "      div.addEventListener('pointerup',  e=>{e.preventDefault();releaseKey(k.key);});\n"
    "      div.addEventListener('pointerleave',e=>{e.preventDefault();releaseKey(k.key);});\n"
    "    }\n"
    "    keyDomMap[k.key]=div;"
)
html = html.replace(
    "    div.className='bkey';\n"
    "    div.innerHTML=`<span class=\"klabel\">${k.key.toUpperCase()}</span><span class=\"knote\">${k.note}</span>`;\n"
    "    // afterWhiteIdx番目の白鍵右端を中心に配置\n"
    "    const centerPct = k.afterWhiteIdx * wPct;\n"
    "    div.style.left  = `calc(${centerPct}% - ${bwPct/2}%)`;\n"
    "    div.style.width  = `${bwPct}%`;\n"
    "    div.style.minWidth='22px';\n"
    "    blackRow.appendChild(div);\n"
    "    div.addEventListener('pointerdown',e=>{e.preventDefault();triggerKey(k.key);});\n"
    "    div.addEventListener('pointerup',  e=>{e.preventDefault();releaseKey(k.key);});\n"
    "    div.addEventListener('pointerleave',e=>{e.preventDefault();releaseKey(k.key);});\n"
    "    keyDomMap[k.key]=div;",
    "    const bDisabled = !ACTIVE_LANE_SET.has(k.lane);\n"
    "    div.className = bDisabled ? 'bkey disabled' : 'bkey';\n"
    "    div.innerHTML=`<span class=\"klabel\">${k.key.toUpperCase()}</span><span class=\"knote\">${k.note}</span>`;\n"
    "    const centerPct = k.afterWhiteIdx * wPct;\n"
    "    div.style.left  = `calc(${centerPct}% - ${bwPct/2}%)`;\n"
    "    div.style.width  = `${bwPct}%`;\n"
    "    div.style.minWidth='22px';\n"
    "    blackRow.appendChild(div);\n"
    "    if(!bDisabled){\n"
    "      div.addEventListener('pointerdown',e=>{e.preventDefault();triggerKey(k.key);});\n"
    "      div.addEventListener('pointerup',  e=>{e.preventDefault();releaseKey(k.key);});\n"
    "      div.addEventListener('pointerleave',e=>{e.preventDefault();releaseKey(k.key);});\n"
    "    }\n"
    "    keyDomMap[k.key]=div;"
)

# ════════════════════════════════════════════════════════
# 12. triggerKey: 未使用レーン無効化 + オクターブシフト対応
# ════════════════════════════════════════════════════════
html = html.replace(
    "  const kd = ALL_KEYS[key];\n"
    "  if(!kd) return;\n"
    "\n"
    "  playPianoNote(kd.freq);\n"
    "  spawnNote(kd.lane);\n"
    "  triggerGlow(kd.lane);\n"
    "\n"
    "  // HUD\n"
    "  bumpCombo();\n"
    "  const mult = Math.max(1, Math.floor(combo/4));\n"
    "  addScore(300*mult);\n"
    "  showJudge('PERFECT!','#00ffee');\n"
    "  showNotePop(kd.note, LANE_COLORS_HEX[kd.lane]);\n"
    "\n"
    "  // パーティクル\n"
    "  spawnParticles(getLaneScreenX(kd.lane), getHitLineScreenY(), LANE_COLORS_HEX[kd.lane]);",

    "  const kd = ALL_KEYS[key];\n"
    "  if(!kd) return;\n"
    "  if(!ACTIVE_LANE_SET.has(kd.lane)) return;\n"
    "\n"
    "  const freq     = octShift ? kd.freq * 2 : kd.freq;\n"
    "  const noteName = octShift ? kd.note.replace(/(\\d)$/, n => +n+1) : kd.note;\n"
    "  playPianoNote(freq);\n"
    "  spawnNote(kd.lane);\n"
    "  triggerGlow(kd.lane);\n"
    "\n"
    "  // HUD\n"
    "  bumpCombo();\n"
    "  const mult = Math.max(1, Math.floor(combo/4));\n"
    "  addScore(300*mult);\n"
    "  showJudge('PERFECT!','#00ffee');\n"
    "  showNotePop(noteName, LANE_COLORS_HEX[kd.lane]);\n"
    "\n"
    "  // パーティクル\n"
    "  spawnParticles(getLaneScreenX(kd.lane), getHitLineScreenY(), LANE_COLORS_HEX[kd.lane]);"
)

# ════════════════════════════════════════════════════════
# 12b. keydown/keyup で Shift を追跡
# ════════════════════════════════════════════════════════
html = html.replace(
    "window.addEventListener('keydown',e=>{\n"
    "  if(e.repeat) return;\n"
    "  triggerKey(e.key.toLowerCase());\n"
    "});\n"
    "window.addEventListener('keyup',e=>{\n"
    "  releaseKey(e.key.toLowerCase());\n"
    "});",

    "let octShift = false;\n"
    "const octIndicator = document.getElementById('oct-indicator');\n"
    "window.addEventListener('keydown',e=>{\n"
    "  if(e.key==='Shift'){\n"
    "    octShift=true; octIndicator.classList.add('active'); return;\n"
    "  }\n"
    "  if(e.repeat) return;\n"
    "  triggerKey(e.key.toLowerCase());\n"
    "});\n"
    "window.addEventListener('keyup',e=>{\n"
    "  if(e.key==='Shift'){\n"
    "    octShift=false; octIndicator.classList.remove('active'); return;\n"
    "  }\n"
    "  releaseKey(e.key.toLowerCase());\n"
    "});"
)

# ════════════════════════════════════════════════════════
# 13. オートプレイ・ファイル読み込みロジックを初期化前に追加
# ════════════════════════════════════════════════════════
AUTOPLAY_JS = """
// ============================================================
// レーン → キーデータ逆引き
// ============================================================
const LANE_TO_KEY = {};
WHITE_KEYS.forEach(k => LANE_TO_KEY[k.lane] = k);
BLACK_KEYS.forEach(k => LANE_TO_KEY[k.lane] = k);

// ============================================================
// 譜面 (fumen) ロード
// ============================================================
function loadFumenData(data) {
  FUMEN          = data;
  ACTIVE_LANE_SET = new Set(FUMEN.notes.map(n => n.lane));
  ACTIVE_LANES    = [...ACTIVE_LANE_SET].sort((a,b)=>a-b);
  ACTIVE_TOTAL    = ACTIVE_LANES.length;
  LANE_REMAP      = new Map(ACTIVE_LANES.map((orig,idx)=>[orig,idx]));
  glowTimers      = new Map(ACTIVE_LANES.map(l=>[l,0]));

  // 古いノーツを消す
  for(const n of activeNotes) scene.remove(n.mesh);
  activeNotes.length = 0;

  buildHighway();
  buildPianoUI();

  document.getElementById('fumen-status').textContent =
    '✓ ' + (FUMEN.title || 'fumen') + '  (' + FUMEN.notes.length + ' notes)';
  document.getElementById('autoplay-btn').disabled = false;
}

document.getElementById('fumen-file-btn').addEventListener('click', () => {
  document.getElementById('fumen-input').click();
});
document.getElementById('fumen-input').addEventListener('change', async (e) => {
  const file = e.target.files[0];
  if(!file) return;
  document.getElementById('fumen-status').textContent = '読み込み中...';
  try {
    const text = await file.text();
    loadFumenData(JSON.parse(text));
  } catch(err) {
    document.getElementById('fumen-status').textContent = '⚠ ' + err.message;
  }
});

// ============================================================
// BGM ファイル読み込み
// ============================================================
let bgmBuffer = null;
let bgmSource = null;

document.getElementById('bgm-file-btn').addEventListener('click', () => {
  document.getElementById('bgm-input').click();
});
document.getElementById('bgm-input').addEventListener('change', async (e) => {
  const file = e.target.files[0];
  if(!file) return;
  const statusEl = document.getElementById('bgm-status');
  statusEl.textContent = '読み込み中...';
  try {
    const ctx = getAudioCtx();
    const buf = await file.arrayBuffer();
    bgmBuffer = await ctx.decodeAudioData(buf);
    statusEl.textContent = '✓ ' + file.name;
  } catch(err) {
    statusEl.textContent = '⚠ ' + err.message;
  }
});

// ============================================================
// オートプレイ
// ============================================================
const TRAVEL_TIME = (NOTE_Z_HIT - NOTE_Z_SPAWN) / NOTE_SPEED; // ~4.75s

let autoplayMode  = false;
let autoplayStart = null;
let autoSpawnIdx  = 0;
let autoAudioIdx  = 0;

const autoBarFill = document.getElementById('autoplay-bar-fill');
const autoTimerEl = document.getElementById('autoplay-timer');

function fmtTime(sec){
  const m = Math.floor(sec/60);
  const s = Math.floor(sec%60).toString().padStart(2,'0');
  return m+':'+s;
}

function spawnNoteAtZ(laneIdx, zPos){
  const x     = getLaneX(laneIdx);
  const color = LANE_COLORS_INT[laneIdx];
  const geo   = new THREE.BoxGeometry(LANE_W - 0.05, NOTE_H, 0.52);
  const mat   = new THREE.MeshBasicMaterial({color, transparent:true, opacity:0.93});
  const mesh  = new THREE.Mesh(geo, mat);
  mesh.add(new THREE.LineSegments(
    new THREE.EdgesGeometry(geo),
    new THREE.LineBasicMaterial({color:0xffffff, transparent:true, opacity:0.65})
  ));
  const hl = new THREE.Mesh(
    new THREE.PlaneGeometry(LANE_W-0.1, 0.07),
    new THREE.MeshBasicMaterial({color:0xffffff, transparent:true, opacity:0.55, side:THREE.DoubleSide})
  );
  hl.position.set(0, NOTE_H/2+0.001, 0);
  mesh.add(hl);
  mesh.position.set(x, NOTE_H/2, zPos);
  scene.add(mesh);
  activeNotes.push({mesh, alive:true});
}

function hitAutoNoteEffects(note){
  const kd = LANE_TO_KEY[note.lane];
  if(kd) playPianoNote(kd.freq);
  triggerGlow(note.lane);
  bumpCombo();
  const mult = Math.max(1, Math.floor(combo/4));
  addScore(300*mult);
  showJudge('PERFECT!','#00ffee');
  showNotePop(note.note, LANE_COLORS_HEX[note.lane]);
  spawnParticles(getLaneScreenX(note.lane), getHitLineScreenY(), LANE_COLORS_HEX[note.lane]);
  if(kd && keyDomMap[kd.key]){
    keyDomMap[kd.key].classList.add('active');
    setTimeout(()=>keyDomMap[kd.key].classList.remove('active'),120);
  }
}

function startAutoplay(){
  if(!FUMEN) return;
  const ctx = getAudioCtx();
  gameStarted   = true;
  autoplayMode  = true;
  autoplayStart = performance.now();
  autoSpawnIdx  = 0;
  autoAudioIdx  = 0;
  document.getElementById('start-screen').style.display='none';

  if(bgmBuffer){
    if(bgmSource){ try{ bgmSource.stop(); }catch(e){} }
    bgmSource = ctx.createBufferSource();
    bgmSource.buffer = bgmBuffer;
    bgmSource.connect(ctx.destination);
    bgmSource.start(ctx.currentTime);
  }

  // 開始時点で既に飛行中のはずのノーツを正しいZ位置から生成
  while(autoSpawnIdx < FUMEN.notes.length &&
        FUMEN.notes[autoSpawnIdx].time < TRAVEL_TIME){
    const note = FUMEN.notes[autoSpawnIdx];
    spawnNoteAtZ(note.lane, NOTE_Z_HIT - NOTE_SPEED * note.time);
    autoSpawnIdx++;
  }
}

document.getElementById('autoplay-btn').addEventListener('click', startAutoplay);
"""

AUTOPLAY_TICK = """  // オートプレイスケジューラ
  if(autoplayMode && autoplayStart !== null && FUMEN){
    const apElapsed = (performance.now()-autoplayStart)/1000;

    // ① TRAVEL_TIME 先行してノーツをスポーン
    while(autoSpawnIdx < FUMEN.notes.length &&
          FUMEN.notes[autoSpawnIdx].time - TRAVEL_TIME <= apElapsed){
      spawnNoteAtZ(FUMEN.notes[autoSpawnIdx].lane, NOTE_Z_SPAWN);
      autoSpawnIdx++;
    }
    // ② ヒット時刻に音・エフェクト
    while(autoAudioIdx < FUMEN.notes.length &&
          FUMEN.notes[autoAudioIdx].time <= apElapsed){
      hitAutoNoteEffects(FUMEN.notes[autoAudioIdx]);
      autoAudioIdx++;
    }

    const pct = Math.min(100, apElapsed/FUMEN.totalDuration*100);
    autoBarFill.style.width = pct+'%';
    autoTimerEl.textContent = fmtTime(apElapsed)+' / '+fmtTime(FUMEN.totalDuration);
    if(autoAudioIdx >= FUMEN.notes.length && apElapsed > FUMEN.totalDuration){
      autoplayMode = false;
      autoBarFill.style.width = '100%';
    }
  }

"""

html = html.replace(
    "// ============================================================\n"
    "// 初期化\n"
    "// ============================================================\n"
    "buildPianoUI();\n"
    "animate();",

    AUTOPLAY_JS +
    "// ============================================================\n"
    "// 初期化\n"
    "// ============================================================\n"
    "buildPianoUI();\n"
    "animate();"
)

html = html.replace(
    "  // ヒットライン点滅\n"
    "  hitLine3D.material.opacity = 0.7 + Math.sin(Date.now()*0.006)*0.22;",
    AUTOPLAY_TICK +
    "  // ヒットライン点滅\n"
    "  if(hitLine3D) hitLine3D.material.opacity = 0.7 + Math.sin(Date.now()*0.006)*0.22;"
)
assert "// オートプレイスケジューラ" in html, "AUTOPLAY_TICK の挿入に失敗しました！"

# ════════════════════════════════════════════════════════
# 検証
# ════════════════════════════════════════════════════════
OUT_HTML.write_text(html)
size = OUT_HTML.stat().st_size
print(f"生成完了: {OUT_HTML}  ({size//1024} KB)")

# パッチ適用確認
checks = [
    ("buildHighway()", "buildHighway 関数"),
    ("loadFumenData(", "loadFumenData 関数"),
    ("fumen-file-btn", "fumen ファイルピッカー"),
    ("bgm-file-btn", "BGM ファイルピッカー"),
    ("TRAVEL_TIME", "TRAVEL_TIME 定数"),
    ("_BLACK_LANE_SET_COLOR", "白鍵/黒鍵カラー"),
    ("spawnNoteAtZ", "spawnNoteAtZ 関数"),
]
for needle, label in checks:
    if needle in html:
        print(f"  ✓ {label}")
    else:
        print(f"  ✗ {label}  ← 見つかりません!")
