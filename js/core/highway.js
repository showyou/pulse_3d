/**
 * buildHighway — 共通ハイウェイ生成
 *
 * cfg:
 *   numLanes        レーン数
 *   laneSpacing     レーン中心間距離
 *   laneGlowWidth   グロー帯の幅
 *   laneColors      [numLanes] の 0xRRGGBB 整数配列
 *   baseLaneOpacity 単一数値 or 配列（レーンごと）  default: 0.08
 *   highwayLen      default: 130
 *   hitZ            ヒットラインZ  default: 4.2
 *   showSideBorders サイドボーダー  default: true
 *   showHitLights   ヒット時ポイントライト  default: true
 *
 * 戻り値に group を含む。scene.remove(group) でまとめて破棄可能。
 */
export function buildHighway(scene, cfg) {
  const {
    numLanes,
    laneSpacing,
    laneGlowWidth,
    laneColors,
    baseLaneOpacity = 0.08,
    highwayLen = 130,
    hitZ = 4.2,
    showSideBorders = true,
    showHitLights = true,
  } = cfg;

  const hwWidth  = numLanes * laneSpacing + 0.4;
  const getLaneX = i => (i - (numLanes - 1) / 2) * laneSpacing;
  const baseOp   = i => Array.isArray(baseLaneOpacity)
    ? (baseLaneOpacity[i] ?? 0.08)
    : baseLaneOpacity;

  // すべての geometry/light を group にまとめる（scene.remove(group) で一括削除可）
  const group = new THREE.Group();
  scene.add(group);

  // 床
  const floor = new THREE.Mesh(
    new THREE.PlaneGeometry(hwWidth, highwayLen),
    new THREE.MeshStandardMaterial({
      color: 0x030810, roughness: .9, metalness: .1,
      transparent: true, opacity: .45,
    })
  );
  floor.rotation.x = -Math.PI / 2;
  floor.position.set(0, -.01, -highwayLen / 2 + 10);
  group.add(floor);

  // 区切り線
  for (let i = 0; i <= numLanes; i++) {
    const x = -hwWidth / 2 + i * laneSpacing;
    const m = new THREE.Mesh(
      new THREE.PlaneGeometry(.025, highwayLen),
      new THREE.MeshBasicMaterial({ color: 0x4488cc, transparent: true, opacity: .18 })
    );
    m.rotation.x = -Math.PI / 2;
    m.position.set(x, .001, -highwayLen / 2 + 10);
    group.add(m);
  }

  // レーングロー帯
  const laneGlowMats = [];
  for (let i = 0; i < numLanes; i++) {
    const bo  = baseOp(i);
    const mat = new THREE.MeshBasicMaterial({ color: laneColors[i], transparent: true, opacity: bo });
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(laneGlowWidth, highwayLen), mat);
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.set(getLaneX(i), .002, -highwayLen / 2 + 10);
    group.add(mesh);
    laneGlowMats.push({ mat, base: bo });
  }

  // サイドボーダー
  if (showSideBorders) {
    for (const x of [-hwWidth / 2, hwWidth / 2]) {
      const m = new THREE.Mesh(
        new THREE.PlaneGeometry(.08, highwayLen),
        new THREE.MeshBasicMaterial({ color: 0x00c8ff, transparent: true, opacity: .35 })
      );
      m.rotation.x = -Math.PI / 2;
      m.position.set(x, .003, -highwayLen / 2 + 10);
      group.add(m);
    }
  }

  // ヒットライン（3D）
  const hitLine3D = new THREE.Mesh(
    new THREE.PlaneGeometry(hwWidth + 0.6, 0.07),
    new THREE.MeshBasicMaterial({ color: 0xff2233, transparent: true, opacity: 0.88 })
  );
  hitLine3D.rotation.x = -Math.PI / 2;
  hitLine3D.position.set(0, 0.02, hitZ);
  group.add(hitLine3D);

  // ヒットポイントライト（レーンごと）
  const hitLights = [];
  if (showHitLights) {
    for (let i = 0; i < numLanes; i++) {
      const light = new THREE.PointLight(laneColors[i], 0, 9);
      light.position.set(getLaneX(i), .8, hitZ);
      group.add(light);
      hitLights.push(light);
    }
  }

  return { laneGlowMats, hitLine3D, hitLights, getLaneX, hwWidth, group };
}
