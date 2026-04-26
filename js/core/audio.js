let audioCtx = null;

export function getAudioCtx() {
  if (!audioCtx) audioCtx = new (window.AudioContext || window.webkitAudioContext)();
  if (audioCtx.state === 'suspended') audioCtx.resume();
  return audioCtx;
}

// ピアノモード用：実音シンセ
export function playPianoNote(freq) {
  const ctx = getAudioCtx();
  const now = ctx.currentTime;

  const o1 = ctx.createOscillator(), o2 = ctx.createOscillator();
  o1.type = 'sine';     o1.frequency.value = freq;
  o2.type = 'triangle'; o2.frequency.value = freq * 2.0;

  const g1 = ctx.createGain(), g2 = ctx.createGain();
  g1.gain.setValueAtTime(0, now);
  g1.gain.linearRampToValueAtTime(0.42, now + 0.008);
  g1.gain.exponentialRampToValueAtTime(0.14, now + 0.14);
  g1.gain.exponentialRampToValueAtTime(0.001, now + 1.1);

  g2.gain.setValueAtTime(0, now);
  g2.gain.linearRampToValueAtTime(0.10, now + 0.005);
  g2.gain.exponentialRampToValueAtTime(0.001, now + 0.38);

  const click = ctx.createOscillator();
  click.type = 'square'; click.frequency.value = freq * 3.5;
  const cg = ctx.createGain();
  cg.gain.setValueAtTime(0.07, now);
  cg.gain.exponentialRampToValueAtTime(0.001, now + 0.018);
  click.connect(cg); cg.connect(ctx.destination);
  click.start(now); click.stop(now + 0.02);

  const filt = ctx.createBiquadFilter();
  filt.type = 'lowpass';
  filt.frequency.setValueAtTime(4500, now);
  filt.frequency.exponentialRampToValueAtTime(700, now + 0.6);

  o1.connect(g1); o2.connect(g2);
  g1.connect(filt); g2.connect(filt);
  filt.connect(ctx.destination);
  o1.start(now); o2.start(now);
  o1.stop(now + 1.2); o2.stop(now + 0.5);
}

// リズムモード用：打鍵 SE
// type: 'hit'（ノーツヒット）| 'tap'（空打ち）
export function playRhythmSound(type) {
  try {
    const ctx = getAudioCtx();
    const now = ctx.currentTime;

    if (type === 'hit') {
      const clickBuf = ctx.createBuffer(1, Math.floor(ctx.sampleRate * 0.008), ctx.sampleRate);
      const cd = clickBuf.getChannelData(0);
      for (let i = 0; i < cd.length; i++)
        cd[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / cd.length, 1.2);
      const click = ctx.createBufferSource(); click.buffer = clickBuf;
      const cg = ctx.createGain(); cg.gain.setValueAtTime(0.55, now);
      click.connect(cg); cg.connect(ctx.destination); click.start(now);

      const osc = ctx.createOscillator();
      osc.type = 'triangle';
      osc.frequency.setValueAtTime(520, now);
      osc.frequency.exponentialRampToValueAtTime(180, now + 0.065);
      const og = ctx.createGain();
      og.gain.setValueAtTime(0.22, now);
      og.gain.exponentialRampToValueAtTime(0.001, now + 0.10);
      osc.connect(og); og.connect(ctx.destination);
      osc.start(now); osc.stop(now + 0.10);

      const nb = ctx.createBuffer(1, Math.floor(ctx.sampleRate * 0.05), ctx.sampleRate);
      const nd = nb.getChannelData(0);
      for (let i = 0; i < nd.length; i++)
        nd[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / nd.length, 2.8) * 0.25;
      const ns = ctx.createBufferSource(); ns.buffer = nb;
      const nf = ctx.createBiquadFilter(); nf.type = 'lowpass'; nf.frequency.value = 2200;
      const ng = ctx.createGain();
      ng.gain.setValueAtTime(0.4, now);
      ng.gain.exponentialRampToValueAtTime(0.001, now + 0.05);
      ns.connect(nf); nf.connect(ng); ng.connect(ctx.destination); ns.start(now);
    } else {
      const buf = ctx.createBuffer(1, Math.floor(ctx.sampleRate * 0.05), ctx.sampleRate);
      const d = buf.getChannelData(0);
      for (let i = 0; i < d.length; i++)
        d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / d.length, 2.5) * 0.4;
      const src = ctx.createBufferSource(); src.buffer = buf;
      const filter = ctx.createBiquadFilter(); filter.type = 'highpass'; filter.frequency.value = 3500;
      const gain = ctx.createGain();
      gain.gain.setValueAtTime(0.9, now);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.05);
      src.connect(filter); filter.connect(gain); gain.connect(ctx.destination);
      src.start();
    }
  } catch (e) {}
}
