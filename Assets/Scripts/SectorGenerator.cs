using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/SectorGenerator")]
public class SectorGenerator : ScriptableObject {
    private static float _s = 0.5f;
    private static Vector3 _rub = new Vector3(_s, _s, -_s);
    private static Vector3 _lub = new Vector3(-_s, _s, -_s);
    private static Vector3 _luf = new Vector3(-_s, _s, _s);
    private static Vector3 _ruf = new Vector3(_s, _s, _s);
    private static Vector3 _rdb = new Vector3(_s, -_s, -_s);
    private static Vector3 _ldb = new Vector3(-_s, -_s, -_s);
    private static Vector3 _ldf = new Vector3(-_s, -_s, _s);
    private static Vector3 _rdf = new Vector3(_s, -_s, _s);

    private static Vector3Int[] _neighbors = {
        Vector3Int.up, Vector3Int.right, Vector3Int.left, Vector3Int.forward, Vector3Int.back, Vector3Int.down
    };

    public void FillSectorMesh(Sector sector) {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        // Debug.Log("Blocks in sector analyzed: " + sector.blocks.Count);
        foreach (var block in sector.blocks) {
            if (block.Value == BlockType.Empty) continue;
            var pos = block.Key;
            for (var i = 0; i < _neighbors.Length; i++) {
                var npos = pos + _neighbors[i];
                if (sector.blocks.TryGetValue(npos, out var neighborType) && neighborType != BlockType.Empty)
                    continue;
                AddFace(vertices, triangles, pos, _neighbors[i]);
            }
        }

        var mesh = new Mesh {vertices = vertices.ToArray(), triangles = triangles.ToArray()};
        mesh.RecalculateNormals();
        sector.GetComponent<MeshFilter>().mesh = mesh;
        sector.GetComponent<MeshCollider>().sharedMesh = mesh;
        Debug.Log(String.Format("Generated sector {2} with {0} vertices and {1} triangles", vertices.Count, triangles.Count / 3, sector.offset));
    }
    
    private void AddFace(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3Int dir) {
        // TODO should inline method
        if (dir == Vector3Int.up)
            AddFaceInternal(vertices, triangles, _rub, _lub, _luf, _ruf, center);
        else if (dir == Vector3Int.down)
            AddFaceInternal(vertices, triangles, _rdb, _rdf, _ldf, _ldb, center);
        else if (dir == Vector3Int.right)
            AddFaceInternal(vertices, triangles, _rub, _ruf, _rdf, _rdb, center);
        else if (dir == Vector3Int.left)
            AddFaceInternal(vertices, triangles, _lub, _ldb, _ldf, _luf, center);
        else if (dir == Vector3Int.forward)
            AddFaceInternal(vertices, triangles, _ruf, _luf, _ldf, _rdf, center);
        else if (dir == Vector3Int.back)
            AddFaceInternal(vertices, triangles, _rub, _rdb, _ldb, _lub, center);
    }

    private static void AddFaceInternal(List<Vector3> vertices, List<int> triangles,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 center
    ) {
        var i = vertices.Count;
        vertices.Add(center + a);
        vertices.Add(center + b);
        vertices.Add(center + c);
        vertices.Add(center + d);
        
        triangles.Add(i);
        triangles.Add(i+1);
        triangles.Add(i+3);
        
        triangles.Add(i+1);
        triangles.Add(i+2);
        triangles.Add(i+3);
    }
}
