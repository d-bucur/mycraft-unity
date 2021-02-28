using Unity.Collections;
using UnityEngine;

public struct MeshHelper {
    public NativeList<Vector3> vertices;
    public NativeList<Vector2> uvs;
    public NativeList<int> triangles;
    public NativeList<Vector3> normals;

    public MeshHelper(int predictedVertices) {
        vertices = new NativeList<Vector3>(predictedVertices, Allocator.Persistent);
        uvs = new NativeList<Vector2>(predictedVertices, Allocator.Persistent);
        triangles = new NativeList<int>(predictedVertices, Allocator.Persistent);
        normals = new NativeList<Vector3>(predictedVertices, Allocator.Persistent);
    }

    public void Clear() {
        triangles.Clear();
        vertices.Clear();
        uvs.Clear();
        normals.Clear();
    }

    public void Dispose() {
        triangles.Dispose();
        vertices.Dispose();
        uvs.Dispose();
        normals.Dispose();
    }

    public Mesh GetRenderMesh() {
        var mesh = new Mesh();
        mesh.SetVertices(vertices.AsArray(), 0, vertices.Length);
        mesh.SetIndices(triangles.AsArray(), MeshTopology.Triangles, 0);
        mesh.SetUVs(0, uvs.AsArray(), 0, uvs.Length);
        mesh.SetNormals(normals.AsArray(), 0, normals.Length);
        return mesh;
    }
}
