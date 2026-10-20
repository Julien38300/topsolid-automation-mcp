#!/usr/bin/env python
"""Vérification géométrique déterministe d'un STL exporté de TopSolid.

Usage :
  python verify_stl.py MODEL.stl [--render out_prefix] [--views iso,face,profil]

Produit :
  1. Nombre total de triangles, boîte englobante globale (BBox) et somme des volumes.
  2. Segmentation exacte en corps connexes (solides fermés par sommets partagés) :
     BBox, dimensions, centroïde et volume individuel de chaque pièce.
  3. Détection déterministe des collisions / interférences 3D (intersection des AABB
     entre solides distincts, car le volume STL brut d'un assemblage n'effectue pas
     d'union booléenne et masque les interpénétrations).
  4. Rendus PNG multi-vues corrigés :
     - face   : az=0°,   el=6°  (élévation frontale X-Z, regard suivant Y)
     - profil : az=-90°, el=6°  (coupe latérale Y-Z, regard suivant X)
     - iso    : az=-38°, el=18° (perspective 3/4 proche de la photo)
"""
import argparse
import struct
from collections import defaultdict

import numpy as np


def parse_stl(path):
    """Retourne (N, 3, 3) en unités du fichier (binaire ou ASCII)."""
    with open(path, "rb") as f:
        head = f.read(84)
        if len(head) >= 84 and not head[:5].lower().startswith(b"solid"):
            n = struct.unpack("<I", head[80:84])[0]
            f.seek(0, 2)
            size = f.tell()
            if size == 84 + n * 50:
                f.seek(84)
                raw = f.read(n * 50)
                arr = np.empty((n, 3, 3), dtype=float)
                for i in range(n):
                    vals = struct.unpack("<12f", raw[i * 50 + 12 : i * 50 + 60])
                    arr[i] = np.array(vals).reshape(3, 3)
                return arr
    verts = []
    with open(path, "r", errors="ignore") as f:
        tri = []
        for line in f:
            s = line.strip()
            if s.startswith("vertex"):
                tri.append([float(x) for x in s.split()[1:4]])
                if len(tri) == 3:
                    verts.append(tri)
                    tri = []
    return np.array(verts, dtype=float)


def bbox(verts):
    pts = verts.reshape(-1, 3)
    return pts.min(axis=0), pts.max(axis=0)


def signed_volume(tris):
    """Volume signé par théorème de la divergence, en unités³ du fichier."""
    a, b, c = tris[:, 0], tris[:, 1], tris[:, 2]
    vol = np.einsum("ij,ij->i", a, np.cross(b, c)).sum() / 6.0
    return abs(float(vol))


def segment_connected_solids(tris, weld_tol=0.05):
    """Segmente le maillage STL en solides fermés par arêtes orientées opposées (manifold)."""
    n = len(tris)
    if n == 0:
        return []
    parent = list(range(n))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    def vkey(p):
        return (
            int(round(p[0] / weld_tol)),
            int(round(p[1] / weld_tol)),
            int(round(p[2] / weld_tol)),
        )

    # Indexation des demi-arêtes orientées (u -> v)
    half_edges = defaultdict(list)
    tri_keys = []
    for ti in range(n):
        ks = [vkey(tris[ti][0]), vkey(tris[ti][1]), vkey(tris[ti][2])]
        tri_keys.append(ks)
        for k in range(3):
            u, v = ks[k], ks[(k + 1) % 3]
            half_edges[(u, v)].append(ti)

    # Appariement de chaque demi-arête (u, v) avec la demi-arête opposée (v, u) la plus proche dans le flux STL
    for ti in range(n):
        ks = tri_keys[ti]
        for k in range(3):
            u, v = ks[k], ks[(k + 1) % 3]
            opp = half_edges.get((v, u))
            if opp:
                best_tj = min(opp, key=lambda tj: abs(tj - ti))
                union(ti, best_tj)

    comp = defaultdict(list)
    for ti in range(n):
        comp[find(ti)].append(ti)

    solids = []
    for idx_list in comp.values():
        sub = tris[idx_list]
        mn, mx = bbox(sub)
        vol_cm3 = signed_volume(sub) / 1000.0
        cent = 0.5 * (mn + mx)
        solids.append(
            {
                "tri_indices": idx_list,
                "n_tris": len(idx_list),
                "min": mn,
                "max": mx,
                "dim": mx - mn,
                "centroid": cent,
                "vol_cm3": vol_cm3,
            }
        )
    solids.sort(key=lambda s: (round(s["centroid"][2], 1), round(s["centroid"][0], 1)))
    return solids


def detect_aabb_collisions(solids, min_overlap_mm=0.2):
    """Détecte les interpénétrations de boîtes englobantes 3D entre solides distincts."""
    collisions = []
    for i in range(len(solids)):
        for j in range(i + 1, len(solids)):
            s1, s2 = solids[i], solids[j]
            ox = min(s1["max"][0], s2["max"][0]) - max(s1["min"][0], s2["min"][0])
            oy = min(s1["max"][1], s2["max"][1]) - max(s1["min"][1], s2["min"][1])
            oz = min(s1["max"][2], s2["max"][2]) - max(s1["min"][2], s2["min"][2])
            if ox > min_overlap_mm and oy > min_overlap_mm and oz > min_overlap_mm:
                vol_cm3 = (ox * oy * oz) / 1000.0
                collisions.append((i + 1, j + 1, (ox, oy, oz), vol_cm3))
    return collisions


def render(tris, path, view="iso", size=(1600, 1000)):
    from PIL import Image, ImageDraw

    # Repère TopSolid du garde-corps :
    #   X = axe longitudinal (largeur travées)
    #   Y = profondeur (face avant vers -Y)
    #   Z = hauteur verticale
    angles = {
        "face": (0.0, 5.0),
        "profil": (-90.0, 5.0),
        "iso": (-32.0, 18.0),
        "dessus": (0.0, 88.0),
    }
    az, el = angles.get(view, (-32.0, 18.0))
    a, e = np.radians(az), np.radians(el)
    ca, sa = np.cos(a), np.sin(a)
    ce, se = np.cos(e), np.sin(e)

    def proj(p):
        x, y, z = p
        u = x * ca - y * sa
        v = -(x * sa * se + y * ca * se + z * ce)
        d = x * sa * ce + y * ca * ce - z * se
        return u, v, d

    img = Image.new("RGB", size, (245, 243, 238))
    draw = ImageDraw.Draw(img)
    items = []
    light = np.array([-0.35, -0.65, 0.67])
    light = light / np.linalg.norm(light)

    for t in tris:
        pts = [proj(p) for p in t]
        depth = sum(p[2] for p in pts) / 3.0
        cz = float(t[:, 2].mean())
        cy = float(t[:, 1].mean())
        zmax_t = float(t[:, 2].max())
        # Colorisation réaliste : socle béton vs goujons inox vs acier noir RAL 9005
        if zmax_t <= 300.5 and cy > -126.0:
            base_col = (195, 190, 180)
            outline_col = (155, 150, 140)
        elif cy < -136.0 and cz < 260.0:
            base_col = (205, 210, 215)
            outline_col = (120, 125, 130)
        else:
            base_col = (34, 37, 42)
            outline_col = (20, 22, 25)

        n = np.cross(t[1] - t[0], t[2] - t[0])
        norm = np.linalg.norm(n)
        shade = max(0.35, abs(float(np.dot(n / norm, light)))) if norm > 1e-12 else 0.6
        col = tuple(min(255, int(c * (0.45 + 0.75 * shade))) for c in base_col)
        items.append((depth, pts, col, outline_col))

    items.sort(key=lambda it: -it[0])
    flat = [p[:2] for _, pts, _, _ in items for p in pts]
    umin, umax = min(f[0] for f in flat), max(f[0] for f in flat)
    vmin, vmax = min(f[1] for f in flat), max(f[1] for f in flat)
    W, H = size
    margin = 70
    s = min(
        (W - 2 * margin) / max(umax - umin, 1e-9),
        (H - 2 * margin) / max(vmax - vmin, 1e-9),
    )
    u_off = (W - (umax - umin) * s) * 0.5
    v_off = (H - (vmax - vmin) * s) * 0.5
    for _, pts, col, outline_col in items:
        px = [(u_off + (p[0] - umin) * s, v_off + (p[1] - vmin) * s) for p in pts]
        draw.polygon(px, fill=col, outline=outline_col)
    img.save(path)
    return path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("stl")
    ap.add_argument("--weld-tol", type=float, default=0.05)
    ap.add_argument("--render", help="préfixe des PNG de sortie")
    ap.add_argument("--views", default="iso,face,profil")
    args = ap.parse_args()

    tris = parse_stl(args.stl)
    mn, mx = bbox(tris)
    vol = signed_volume(tris)
    dim = mx - mn
    print(f"triangles  : {len(tris)}")
    print("bbox min   : " + ", ".join(f"{v:.1f}" for v in mn))
    print("bbox max   : " + ", ".join(f"{v:.1f}" for v in mx))
    print("dimensions : " + ", ".join(f"{v:.1f}" for v in dim))
    print(f"somme vol  : {vol / 1000.0:.1f} cm3 (somme linéaire des maillages)")

    solids = segment_connected_solids(tris, weld_tol=args.weld_tol)
    print(f"\nsolides connexes détectés : {len(solids)}")
    for idx, s in enumerate(solids, 1):
        d = s["dim"]
        c = s["centroid"]
        print(
            f"  #{idx:02d} : {s['n_tris']:3d} tris | dim ({d[0]:6.1f} x {d[1]:5.1f} x {d[2]:5.1f}) mm "
            f"| centre ({c[0]:6.1f}, {c[1]:6.1f}, {c[2]:6.1f}) | vol={s['vol_cm3']:8.1f} cm3"
        )

    collisions = detect_aabb_collisions(solids)
    print(f"\ncollisions AABB détectées (>0.2 mm) : {len(collisions)}")
    for i, j, ov, v_cm3 in collisions:
        print(
            f"  [COLLISION] Solide #{i:02d} n Solide #{j:02d} : "
            f"overlap ({ov[0]:.1f} x {ov[1]:.1f} x {ov[2]:.1f}) mm = {v_cm3:.2f} cm3"
        )

    if args.render:
        for view in [v.strip() for v in args.views.split(",") if v.strip()]:
            p = render(tris, f"{args.render}_{view}.png", view=view)
            print(f"rendu {view:6s} : {p}")


if __name__ == "__main__":
    main()