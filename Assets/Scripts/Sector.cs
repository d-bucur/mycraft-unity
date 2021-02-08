using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class Sector : MonoBehaviour {
    public Vector2Int offset;  // TODO is used?
    public Dictionary<Vector3Int, BlockType> blocks = new Dictionary<Vector3Int, BlockType>();
}
