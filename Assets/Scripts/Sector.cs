using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Sector {
    public Vector2Int offset;
    public Dictionary<Vector3Int, BlockType> blocks;
    public IList<GameObject> GameObjects;

    public Sector(Vector2Int offset) : this() {
        this.offset = offset;
        blocks = new Dictionary<Vector3Int, BlockType>();
        GameObjects = new List<GameObject>();
    }
}
