export function createRenderer(canvas) {
  const r = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
  r.setClearColor(0x000000, 0);
  r.setPixelRatio(Math.min(devicePixelRatio, 2));
  r.setSize(innerWidth, innerHeight);
  return r;
}

export function createScene(fogDensity = 0.008) {
  const s = new THREE.Scene();
  s.fog = new THREE.FogExp2(0x000000, fogDensity);
  return s;
}

export function createCamera(fov = 65) {
  const c = new THREE.PerspectiveCamera(fov, innerWidth / innerHeight, 0.1, 230);
  c.position.set(0, 5.5, 9);
  c.lookAt(0, 0, -8);
  return c;
}

export function setupResize(renderer, camera, extra) {
  window.addEventListener('resize', () => {
    renderer.setSize(innerWidth, innerHeight);
    camera.aspect = innerWidth / innerHeight;
    camera.updateProjectionMatrix();
    extra?.();
  });
}
