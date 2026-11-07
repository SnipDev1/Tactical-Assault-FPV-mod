#!/usr/bin/env python3
import xml.etree.ElementTree as ET
import subprocess, struct, os, shutil, tempfile
import numpy as np

def run_assimp_dump(input_fbx, output_xml):
    cmd = ["assimp", "dump", input_fbx, output_xml, "-cdefault"]
    subprocess.check_call(cmd)

def parse_mesh_from_xml(m):
    pos = np.array([float(x) for x in m.find("Positions").text.split()]).reshape((-1, 3))
    norm = np.array([float(x) for x in m.find("Normals").text.split()]).reshape((-1, 3))
    uv_elem = m.find("TextureCoords")
    if uv_elem is not None and uv_elem.text.strip():
        uv = np.array([float(x) for x in uv_elem.text.split()]).reshape((-1, 2))
    else:
        uv = np.zeros((len(pos), 2))
    
    faces = []
    for f in m.find("FaceList").findall("Face"):
        idx = [int(x) for x in f.text.split()]
        faces.append(idx)
    faces = np.array(faces)
    return pos, norm, uv, faces

def write_mesh(filename, vertices, normals, uvs, triangles):
    with open(filename, "wb") as f:
        f.write(b"MESH")
        f.write(struct.pack("<I", 1))
        f.write(struct.pack("<I", len(vertices)))
        f.write(struct.pack("<I", len(triangles)))
        for p, n, uv in zip(vertices, normals, uvs):
            f.write(struct.pack("<8f", float(p[0]), float(p[1]), float(p[2]),
                                       float(n[0]), float(n[1]), float(n[2]),
                                       float(uv[0]), float(uv[1])))
        for idx in triangles:
            f.write(struct.pack("<I", int(idx)))

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    repo_root = os.path.abspath(os.path.join(script_dir, ".."))
    src_dir = os.path.join(repo_root, "src")
    res_dir = os.path.join(src_dir, "TacticalAssaultFPV", "Resources")
    os.makedirs(res_dir, exist_ok=True)

    # Copy textures
    shutil.copyfile(os.path.join(src_dir, "test_bake.png"), os.path.join(res_dir, "drone_tex.png"))
    shutil.copyfile(os.path.join(src_dir, "bomb.png"), os.path.join(res_dir, "bomb_tex.png"))

    with tempfile.TemporaryDirectory() as tmpdir:
        drone_xml = os.path.join(tmpdir, "drone.assxml")
        bomb_xml = os.path.join(tmpdir, "bomb.assxml")

        run_assimp_dump(os.path.join(src_dir, "fpvdrone.fbx"), drone_xml)
        run_assimp_dump(os.path.join(src_dir, "bomb.fbx"), bomb_xml)

        # 1. Process FPV Drone
        drone_tree = ET.parse(drone_xml)
        drone_meshes = [parse_mesh_from_xml(m) for m in drone_tree.getroot().find(".//MeshList").findall("Mesh")]

        # Body (meshes 0..7)
        body_verts, body_norms, body_uvs, body_tris = [], [], [], []
        v_offset = 0
        for i in range(8):
            pos, norm, uv, faces = drone_meshes[i]
            p_trans = np.column_stack([pos[:, 0], pos[:, 2], -pos[:, 1]])
            n_trans = np.column_stack([norm[:, 0], norm[:, 2], -norm[:, 1]])
            f_cw = faces[:, [0, 2, 1]] + v_offset
            body_verts.append(p_trans)
            body_norms.append(n_trans)
            body_uvs.append(uv)
            body_tris.extend(f_cw.flatten())
            v_offset += len(pos)

        write_mesh(os.path.join(res_dir, "drone_body.mesh"),
                   np.vstack(body_verts), np.vstack(body_norms), np.vstack(body_uvs), body_tris)

        # Lamp (mesh 9)
        pos_l, norm_l, uv_l, faces_l = drone_meshes[9]
        p_l_trans = np.column_stack([pos_l[:, 0], pos_l[:, 2], -pos_l[:, 1]])
        n_l_trans = np.column_stack([norm_l[:, 0], norm_l[:, 2], -norm_l[:, 1]])
        write_mesh(os.path.join(res_dir, "drone_lamp.mesh"),
                   p_l_trans, n_l_trans, uv_l, faces_l[:, [0, 2, 1]].flatten())

        # Propeller (mesh 8)
        pos_p, norm_p, uv_p, faces_p = drone_meshes[8]
        scale_p = 23.955715 * 0.01
        p_p_trans = np.column_stack([pos_p[:, 0], pos_p[:, 2], -pos_p[:, 1]]) * scale_p
        n_p_trans = np.column_stack([norm_p[:, 0], norm_p[:, 2], -norm_p[:, 1]])
        write_mesh(os.path.join(res_dir, "drone_prop.mesh"),
                   p_p_trans, n_p_trans, uv_p, faces_p[:, [0, 2, 1]].flatten())

        # 2. Process Bomb
        bomb_tree = ET.parse(bomb_xml)
        bomb_meshes = [parse_mesh_from_xml(m) for m in bomb_tree.getroot().find(".//MeshList").findall("Mesh")]
        bomb_verts, bomb_norms, bomb_uvs, bomb_tris = [], [], [], []
        b_offset = 0
        b_scale = 34.177185 * 0.01
        b_pos = np.array([0.0, -3.823593 * 0.01, 1.103961 * 0.01])
        for i in range(2):
            pos_b, norm_b, uv_b, faces_b = bomb_meshes[i]
            p_b_trans = pos_b * b_scale + b_pos
            f_b_cw = faces_b[:, [0, 2, 1]] + b_offset
            bomb_verts.append(p_b_trans)
            bomb_norms.append(norm_b)
            bomb_uvs.append(uv_b)
            bomb_tris.extend(f_b_cw.flatten())
            b_offset += len(pos_b)

        write_mesh(os.path.join(res_dir, "drone_bomb.mesh"),
                   np.vstack(bomb_verts), np.vstack(bomb_norms), np.vstack(bomb_uvs), bomb_tris)

if __name__ == "__main__":
    main()
