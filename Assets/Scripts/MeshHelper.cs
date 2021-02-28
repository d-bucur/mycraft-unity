using UnityEngine;

public class MeshHelper {
    // TODO use NativeArrays directly
    public ResizableArray<Vector3> vertices;
    public ResizableArray<Vector2> uvs;
    public ResizableArray<int> triangles;

    public MeshHelper(int predictedVertices) {
        vertices = new ResizableArray<Vector3>(predictedVertices);
        uvs = new ResizableArray<Vector2>(predictedVertices);
        triangles = new ResizableArray<int>(predictedVertices);
    }

    public void Clear() {
        triangles.Clear();
        vertices.Clear();
        uvs.Clear();
    }

    public Mesh GetRenderMesh() {
        var mesh = new Mesh();
        mesh.SetVertices(vertices.GetArrayRef(), 0, vertices.Count);
        mesh.SetTriangles(triangles.GetArrayRef(), 0, triangles.Count, 0);
        mesh.SetUVs(0, uvs.GetArrayRef(), 0, uvs.Count);
        mesh.RecalculateNormals();
        return mesh;
    }
    public Mesh MakeCollisionMesh() {
        var mesh = new Mesh();
        mesh.SetVertices(vertices.CloneArray(), 0, vertices.Count);
        mesh.SetTriangles(triangles.CloneArray(), 0, triangles.Count, 0);
        mesh.RecalculateNormals();
        return mesh;
    }
}
