using Unity.Collections;
using UnityEngine;

public struct MeshHelper {
    public NativeList<Vector3> vertices;
    public NativeList<Vector2> uvs;
    public NativeList<int> triangles;

    public MeshHelper(int predictedVertices) {
        vertices = new NativeList<Vector3>(predictedVertices, Allocator.Persistent);
        uvs = new NativeList<Vector2>(predictedVertices, Allocator.Persistent);
        triangles = new NativeList<int>(predictedVertices, Allocator.Persistent);
    }

    public void Clear() {
        triangles.Clear();
        vertices.Clear();
        uvs.Clear();
    }

    public void Dispose() {
        triangles.Dispose();
        vertices.Dispose();
        uvs.Dispose();
    }

    public Mesh GetRenderMesh() {
        var mesh = new Mesh();
        mesh.SetVertices(vertices.AsArray(), 0, vertices.Length);
        mesh.SetIndices(triangles.AsArray(), MeshTopology.Triangles, 0);
        // mesh.SetTriangles(triangles.AsArray(), 0, triangles.Length, 0);
        mesh.SetUVs(0, uvs.AsArray(), 0, uvs.Length);
        mesh.RecalculateNormals();
        return mesh;
    }
    public Mesh MakeCollisionMesh() {
        var mesh = new Mesh();
        // use different meshes by copying
        mesh.SetVertices(vertices.AsArray(), 0, vertices.Length);
        mesh.SetIndices(triangles.AsArray(), MeshTopology.Triangles, 0);
        // mesh.RecalculateNormals();
        return mesh;
    }
}
