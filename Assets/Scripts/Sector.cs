using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class Sector : MonoBehaviour, IEnumerable<Vector3Int> {
    public Vector2Int offset;
    
    private MeshHelper[] _meshHelpers = new MeshHelper[2];
    
    private BlockType[] _blocks;
    private static Dictionary<Vector2Int, Sector> _sectors;
    
    private static int _xSize, _zSize;
    public static int sectorSizeHeight;
    public static int sectorSize;
    
    private const float _s = 0.5f;
    private static readonly Vector3 _rub = new Vector3(_s, _s, -_s);
    private static readonly Vector3 _lub = new Vector3(-_s, _s, -_s);
    private static readonly Vector3 _luf = new Vector3(-_s, _s, _s);
    private static readonly Vector3 _ruf = new Vector3(_s, _s, _s);
    private static readonly Vector3 _rdb = new Vector3(_s, -_s, -_s);
    private static readonly Vector3 _ldb = new Vector3(-_s, -_s, -_s);
    private static readonly Vector3 _ldf = new Vector3(-_s, -_s, _s);
    private static readonly Vector3 _rdf = new Vector3(_s, -_s, _s);

    private enum Direction {
        UP, DOWN, RIGHT, LEFT, FORWARD, BACK
    }

    public static void SetSizes(int sectorSize, int sectorSizeHeight) {
        Sector.sectorSize = sectorSize;
        Sector.sectorSizeHeight = _zSize = sectorSizeHeight;
        _xSize = sectorSizeHeight * sectorSize;
        _sectors = WorldGenerator.Instance._sectors;
    }

    public void Init() {
        _blocks = new BlockType[sectorSize * sectorSize * sectorSizeHeight];
        var predictedVertices = _xSize * sectorSize / 2;
        for (int i = 0; i < _meshHelpers.Length; i++) {
            _meshHelpers[i] = new MeshHelper(predictedVertices);
        }
    }

    public void AddBlock(in Vector3Int pos, BlockType blockType) {
        _blocks[GetId(pos)] = blockType;
    }

    private int GetId(in Vector3Int pos) {
        return pos.x * _xSize + pos.z * _zSize + pos.y;
    }

    public IEnumerator<Vector3Int> GetEnumerator() {
        for (var x = 0; x < sectorSize; x++)
            for (var z = 0; z < sectorSize; z++)
                for (var y = 0; y < sectorSizeHeight; y++)
                    yield return new Vector3Int(x, y, z);
    }

    // TODO add new type for internal pos
    public Vector3Int InternalToWorldPos(Vector3Int pos) {
        pos.x += offset.x * sectorSize;
        pos.z += offset.y * sectorSize;
        pos.y -= sectorSizeHeight / 2;
        return pos;
    }

    /** Note that this does not ensure that the position given is actually inside this sector, this
     * should be checked outside */
    public Vector3Int WorldToInternalPos(Vector3Int pos) {
        return new Vector3Int(
            pos.x - offset.x * sectorSize,
            pos.y,
            pos.z - offset.y * sectorSize);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    private BlockType GetBlock(in Vector3Int pos) {
        return _blocks[GetId(pos)];
    }

    /** Moves through sector map if position is out of current bounds */
    private BlockType SafeGetBlock(Vector3Int pos) {
        Vector2Int sectorPos = offset;
        var differentSector = false;
        if (pos.x < 0) {
            sectorPos.x--;
            pos.x += sectorSize;
            differentSector = true;
        } 
        else if (pos.x >= sectorSize) {
            sectorPos.x++;
            pos.x -= sectorSize;
            differentSector = true;
        }
        if (pos.z < 0) {
            sectorPos.y--;
            pos.z += sectorSize;
            differentSector = true;
        }
        else if (pos.z >= sectorSize) {
            sectorPos.y++;
            pos.z -= sectorSize;
            differentSector = true;
        }

        if (!differentSector)
            return GetBlock(pos);
        
        if (!_sectors.ContainsKey(sectorPos)) {
            // TODO should not happen. Remove when external sectors are created
            // Debug.LogError(String.Format("Trying to read pos {0} of nonexistent sector {1}", pos, sectorPos));
            return BlockType.Empty;
        }
        return _sectors[sectorPos].GetBlock(pos);
    }

    public bool TryGetValue(Vector3Int pos, out BlockType value) {
        // TODO remove?
        value = BlockType.Empty;
        if (
            pos.x < 0 || pos.x >= sectorSize
            || pos.y < 0 || pos.y >= sectorSizeHeight
            || pos.z < 0 || pos.z >= sectorSize
        ) {
            //Debug.Log("Requested invalid pos " + pos);
            return false;
        }
        value = _blocks[GetId(pos)];
        return true;
    }

    public void FillMesh() {
        foreach (var helper in _meshHelpers)
            helper.Clear();
        
        SweepMeshFaces();

        var solidsMesh = _meshHelpers[0].MakeMesh();
        GetComponent<MeshFilter>().mesh = solidsMesh;
        GetComponent<MeshCollider>().sharedMesh = solidsMesh;
        var transparentsMesh = _meshHelpers[1].MakeMesh();
        transform.GetChild(0).GetComponent<MeshFilter>().mesh = transparentsMesh;
        // Debug.Log(String.Format("Generated sector {2} with {0} vertices, {1} triangles",
        //     vertices.Count, triangles.Count / 3, offset));
    }

    private void SweepMeshFaces() {
        BlockType? ConstructFace(in Vector3Int currentPos, BlockType? previousType, in Vector3Int lastPosition,
            Direction currentDirection, Direction previousDirection) {
            var currentType = SafeGetBlock(currentPos);
            if (previousType != null) {
                var prevGroup = Block.GetGroup(previousType.Value);
                var currentGroup = Block.GetGroup(currentType);
                if (currentType == BlockType.Empty && previousType == BlockType.Water) {
                    // draw water surface from both sides
                    AddFace(lastPosition, currentDirection, previousType.Value, 1);
                    AddFace(currentPos, previousDirection, previousType.Value, 1);
                }
                if (currentGroup != prevGroup) {
                    // draw solid surface from solid into empty
                    if (currentGroup == BlockGroup.Transparent)
                        AddFace(lastPosition, currentDirection, previousType.Value, 0);
                    else
                        AddFace(currentPos, previousDirection, currentType, 0);
                }
            }
            previousType = currentType;
            return previousType;
        }

        BlockType? lastType = null;
        Vector3Int lastPos = Vector3Int.zero;

        // Sweep up
        for (var x = 0; x < sectorSize; x++) {
            for (var z = 0; z < sectorSize; z++) {
                for (var y = 0; y < sectorSizeHeight; y++) {
                    var currentPos = new Vector3Int(x, y, z);
                    lastType = ConstructFace(currentPos, lastType, lastPos, Direction.UP, Direction.DOWN);
                    lastPos = currentPos;
                }
                lastType = null;
            }
        }

        // Sweep forward
        for (var x = 0; x < sectorSize; x++) {
            for (var y = 0; y < sectorSizeHeight; y++) {
                for (var z = -1; z <= sectorSize; z++) {
                    var currentPos = new Vector3Int(x, y, z);
                    lastType = ConstructFace(currentPos, lastType, lastPos, Direction.FORWARD, Direction.BACK);
                    lastPos = currentPos;
                }
                lastType = null;
            }
        }

        // Sweep right
        for (var y = 0; y < sectorSizeHeight; y++) {
            for (var z = 0; z < sectorSize; z++) {
                for (var x = -1; x <= sectorSize; x++) {
                    var currentPos = new Vector3Int(x, y, z);
                    lastType = ConstructFace(currentPos, lastType, lastPos, Direction.RIGHT, Direction.LEFT);
                    lastPos = currentPos;
                }
                lastType = null;
            }
        }
    }

    private void AddFace(in Vector3 center, Direction dir, BlockType type, int meshId) {
        var uvPos = (int)type;
        // TODO inline method?
        switch (dir) {
            case Direction.UP:
                AddFaceInternal(_rub, _lub, _luf, _ruf, center, 0, uvPos, meshId);
                break;
            case Direction.DOWN:
                AddFaceInternal(_rdf, _ldf, _ldb, _rdb, center, 2, uvPos, meshId);
                break;
            case Direction.RIGHT:
                AddFaceInternal(_rdb, _rub, _ruf, _rdf, center, 1, uvPos, meshId);
                break;
            case Direction.LEFT:
                AddFaceInternal(_ldf, _luf, _lub, _ldb, center, 1, uvPos, meshId);
                break;
            case Direction.FORWARD:
                AddFaceInternal(_rdf, _ruf, _luf, _ldf, center, 1, uvPos, meshId);
                break;
            case Direction.BACK:
                AddFaceInternal(_ldb, _lub, _rub, _rdb, center, 1, uvPos, meshId);
                break;
        }
    }

    private const int _uvMapSize = 4;
    private const float _uvDelta = 1f / _uvMapSize;
    private void AddFaceInternal(in Vector3 a, in Vector3 b, in Vector3 c, in Vector3 d, in Vector3 center, int uvX,
        int uvY, int meshId) {
        var vertices = _meshHelpers[meshId].vertices;
        var uvs = _meshHelpers[meshId].uvs;
        var triangles = _meshHelpers[meshId].triangles;
        
        var i = vertices.Count;
        vertices.Add(center + a);
        vertices.Add(center + b);
        vertices.Add(center + c);
        vertices.Add(center + d);
        
        uvs.Add(new Vector2(uvX * _uvDelta, uvY * _uvDelta));
        uvs.Add(new Vector2(uvX * _uvDelta, uvY * _uvDelta + _uvDelta));
        uvs.Add(new Vector2(uvX * _uvDelta + _uvDelta, uvY * _uvDelta + _uvDelta));
        uvs.Add(new Vector2(uvX * _uvDelta + _uvDelta, uvY * _uvDelta));
        
        triangles.Add(i);
        triangles.Add(i+1);
        triangles.Add(i+3);
        
        triangles.Add(i+1);
        triangles.Add(i+2);
        triangles.Add(i+3);
    }
}
