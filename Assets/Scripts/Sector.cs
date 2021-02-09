using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class Sector : MonoBehaviour, IEnumerable<Vector3Int> {
    public Vector2Int offset;
    public ResizableArray<Vector3> vertices;
    public ResizableArray<int> triangles;
    
    private BlockType[] _blocks;
    
    private static int _xSize, _zSize;
    private static int _sectorSizeHeight;
    private static int _sectorSize;

    public static void SetSizes(int sectorSize, int sectorSizeHeight) {
        _sectorSize = sectorSize;
        _sectorSizeHeight = _zSize = sectorSizeHeight;
        _xSize = sectorSizeHeight * sectorSize;
    }

    public void Init() {
        _blocks = new BlockType[_sectorSize * _sectorSize * _sectorSizeHeight];
        var predictedVertices = _xSize * _sectorSize / 2;
        vertices = new ResizableArray<Vector3>(predictedVertices);
        triangles = new ResizableArray<int>((int)(predictedVertices * 1.5f));
    }

    public void AddBlock(Vector3Int pos, BlockType blockType) {
        _blocks[GetId(pos)] = blockType;
    }

    private int GetId(Vector3Int pos) {
        return pos.x * _xSize + pos.z * _zSize + pos.y;
    }

    public IEnumerator<Vector3Int> GetEnumerator() {
        for (var x = 0; x < _sectorSize; x++)
            for (var z = 0; z < _sectorSize; z++)
                for (var y = 0; y < _sectorSizeHeight; y++)
                    yield return new Vector3Int(x, y, z);
    }

    // TODO add new type for internal pos
    public Vector3Int InternalToWorldPos(Vector3Int pos) {
        pos.x += offset.x * _sectorSize;
        pos.z += offset.y * _sectorSize;
        pos.y -= _sectorSizeHeight / 2;
        return pos;
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public BlockType GetBlock(Vector3Int pos) {
        return _blocks[GetId(pos)];
    }

    public bool TryGetValue(Vector3Int pos, out BlockType value) {
        value = BlockType.Empty;
        if (
            pos.x < 0 || pos.x >= _sectorSize
            || pos.y < 0 || pos.y >= _sectorSizeHeight
            || pos.z < 0 || pos.z >= _sectorSize
        ) {
            //Debug.Log("Requested invalid pos " + pos);
            return false;
        }
        value = _blocks[GetId(pos)];
        return true;
    }
}
