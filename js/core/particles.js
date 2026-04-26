// ────────────────────────────────────────────
// 3D パーティクルシステム（リズムモード用）
// ────────────────────────────────────────────
export function createParticleSystem3D(scene, hitZ = 4.2) {
  const particles  = [];
  const ascendList = [];

  function spawn(x, color, count = 18) {
    for (let i = 0; i < count; i++) {
      const a  = Math.random() * Math.PI * 2;
      const sp = .045 + Math.random() * .085;
      const mat = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 1 });
      const p   = new THREE.Mesh(new THREE.SphereGeometry(.03 + Math.random() * .06, 5, 5), mat);
      p.position.set(x, .18, hitZ);
      p._vx = Math.cos(a) * sp;
      p._vy = .05 + Math.random() * .08;
      p._vz = (Math.random() - .5) * .05;
      p._life = 1;
      scene.add(p);
      particles.push(p);
    }
  }

  // ノーツヒット時の「昇天」エフェクト
  function spawnAscend(srcMesh, spanW, col) {
    const g = new THREE.Group();
    g.position.copy(srcMesh.position);

    const body = new THREE.Mesh(
      new THREE.BoxGeometry(spanW, .18, .5),
      new THREE.MeshStandardMaterial({
        color: col, emissive: col, emissiveIntensity: 1.2,
        transparent: true, opacity: .85, roughness: .1, metalness: .5,
      })
    );
    g.add(body);

    g.add(new THREE.LineSegments(
      new THREE.EdgesGeometry(new THREE.BoxGeometry(spanW, .18, .5)),
      new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: .9 })
    ));

    for (let i = 0; i < 3; i++) {
      const streak = new THREE.Mesh(
        new THREE.BoxGeometry(.04 + Math.random() * .06, .8 + Math.random() * .6, .04),
        new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: .7 })
      );
      streak.position.set((Math.random() - .5) * spanW * .8, .5 + Math.random() * .3, 0);
      g.add(streak);
    }

    scene.add(g);
    ascendList.push({ g, _vy: 0.04 + Math.random() * 0.02, _life: 1.0, _decay: 0.045 });
  }

  function update(delta) {
    for (let i = particles.length - 1; i >= 0; i--) {
      const p = particles[i];
      p._life -= delta * 2.2;
      p._vy   -= delta * .2;
      p.position.x += p._vx;
      p.position.y += p._vy;
      p.position.z += p._vz;
      p.material.opacity = Math.max(0, p._life);
      if (p._life <= 0) { scene.remove(p); particles.splice(i, 1); }
    }

    for (let i = ascendList.length - 1; i >= 0; i--) {
      const a = ascendList[i];
      a._life -= a._decay;
      a.g.position.y += a._vy * (1 + (1 - a._life) * 2);
      a.g.position.z -= 0.01;
      a.g.children.forEach(c => {
        if (c.material) c.material.opacity = Math.max(0, a._life * 0.9);
      });
      if (a._life <= 0) { scene.remove(a.g); ascendList.splice(i, 1); }
    }
  }

  return { spawn, spawnAscend, update };
}

// ────────────────────────────────────────────
// 2D パーティクルシステム（ピアノモード用）
// ────────────────────────────────────────────
export function createParticleSystem2D(canvas) {
  const particles = [];
  const ctx = canvas.getContext('2d');

  function spawn(sx, sy, hexColor) {
    for (let i = 0; i < 16; i++) {
      const angle = Math.random() * Math.PI * 2;
      const spd   = 2 + Math.random() * 5;
      particles.push({
        x: sx, y: sy,
        vx: Math.cos(angle) * spd,
        vy: Math.sin(angle) * spd - 2.5,
        r: 2.5 + Math.random() * 4,
        alpha: 1,
        color: hexColor,
        decay: 0.016 + Math.random() * 0.014,
      });
    }
  }

  function update() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    for (let i = particles.length - 1; i >= 0; i--) {
      const p = particles[i];
      p.x += p.vx; p.y += p.vy; p.vy += 0.13;
      p.alpha -= p.decay;
      if (p.alpha <= 0) { particles.splice(i, 1); continue; }
      ctx.globalAlpha = p.alpha;
      ctx.fillStyle   = p.color;
      ctx.beginPath();
      ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  return { spawn, update };
}
