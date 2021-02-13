using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class Sector : MonoBehaviour, IEnumerable<Vector3Int> {
    public Vector2Int offset;
    
    private readonly MeshHelper[] _meshHelpers = new MeshHelper[2];
    
    private BlockType[] _blocks;
    private bool _isMeshGenerated = false;
    private bool _isGridGenerated = false;
    public bool IsGridGenerated => _isGridGenerated;

    public static int sectorSize;
    public static int sectorSizeHeight;
    private static int _xSize, _zSize;
    private const int _uvMapSize = 4;

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

    private BlockType GetBlock(in Vector3Int pos) {
        return _blocks[GetId(pos)];
    }

    public void FinishGeneratingGrid() {
        _isGridGenerated = true;
        _isMeshGenerated = false;
    }

    private static int GetId(in Vector3Int pos) {
        return pos.x * _xSize + pos.z * _zSize + pos.y;
    }

    public IEnumerator<Vector3Int> GetEnumerator() {
        for (var x = 0; x < sectorSize; x++)
            for (var z = 0; z < sectorSize; z++)
                for (var y = 0; y < sectorSizeHeight; y++)
                    yield return new Vector3Int(x, y, z);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    /** Moves through sector map if position is out of current bounds */
    private BlockType SafeGetBlock(Vector3Int pos) {
        var differentSector = false;
        int sectorPosX = 0;
        int sectorPosY = 0;
        var posX = pos.x;
        if (posX < 0) {
            sectorPosX--;
            pos.x += sectorSize;
            differentSector = true;
        } 
        else if (posX >= sectorSize) {
            sectorPosX++;
            pos.x -= sectorSize;
            differentSector = true;
        }
        var posZ = pos.z;
        if (posZ < 0) {
            sectorPosY--;
            pos.z += sectorSize;
            differentSector = true;
        }
        else if (posZ >= sectorSize) {
            sectorPosY++;
            pos.z -= sectorSize;
            differentSector = true;
        }
        if (!differentSector)
            return GetBlock(pos);
        
        Vector2Int sectorPos = new Vector2Int(offset.x + sectorPosX, offset.y + sectorPosY);
        return WorldGenerator.Instance.GetOrGenerateSector(sectorPos).GetBlock(pos);
    }

    public void GenerateMesh() {
        if (_isGridGenerated && _isMeshGenerated)
            return;
        
        foreach (var helper in _meshHelpers)
            helper.Clear();
        SweepMeshFaces();
        var solidsMesh = _meshHelpers[0].MakeMesh();
        GetComponent<MeshFilter>().mesh = solidsMesh;
        GetComponent<MeshCollider>().sharedMesh = solidsMesh;
        gameObject.SetActive(true);
        var transparentsMesh = _meshHelpers[1].MakeMesh();
        transform.GetChild(0).GetComponent<MeshFilter>().mesh = transparentsMesh;
        _isMeshGenerated = true;
    }

    private struct SweepData {
        public Vector3Int pos;
        public BlockType type;
        public BlockGroup group;
    }

    // TODO remove and inline
    void FillData(ref SweepData data) {
        data.type = SafeGetBlock(data.pos);
        data.group = Block.GetGroup(data.type);
    }

    SweepData ConstructFace(SweepData previous, Vector3Int currentPos, Direction currentDirection, Direction previousDirection) {
        SweepData current = default;
        current.pos = currentPos;
        FillData(ref current);
        if (current.group != previous.group) {
            // draw solid surface from solid into empty
            if (current.group == BlockGroup.Transparent)
                AddFace(previous.pos, currentDirection, previous.type, 0);
            else
                AddFace(currentPos, previousDirection, current.type, 0);
        }
        else if (current.type == BlockType.Empty && previous.type == BlockType.Water) {
            // draw water surface from both sides
            AddFace(previous.pos, currentDirection, previous.type, 1);
            AddFace(currentPos, previousDirection, previous.type, 1);
        }
        current.pos = currentPos;
        current.type = current.type;
        current.group = current.group;
        return current;
    }

    private void SweepMeshFaces() {
        bool hasLast = false;
        SweepData last = default;

        // Sweep up
        for (var x = 0; x < sectorSize; x++) {
            for (var z = 0; z < sectorSize; z++) {
                for (var y = 0; y < sectorSizeHeight; y++) {
                    var currentPos = new Vector3Int(x, y, z);
                    // TODO move if inside Construct face
                    if (hasLast)
                        last = ConstructFace(last, currentPos, Direction.UP, Direction.DOWN);
                    else {
                        last.pos = currentPos;
                        FillData(ref last);
                        hasLast = true;
                    }
                }
                hasLast = false;
            }
        }

        // Sweep forward
        for (var x = 0; x < sectorSize; x++) {
            for (var y = 0; y < sectorSizeHeight; y++) {
                for (var z = -1; z <= sectorSize; z++) {
                    var currentPos = new Vector3Int(x, y, z);
                    if (hasLast)
                        last = ConstructFace(last, currentPos, Direction.FORWARD, Direction.BACK);
                    else {
                        last.pos = currentPos;
                        FillData(ref last);
                        hasLast = true;
                    }
                }
                hasLast = false;
            }
        }

        // Sweep right
        for (var y = 0; y < sectorSizeHeight; y++) {
            for (var z = 0; z < sectorSize; z++) {
                for (var x = -1; x <= sectorSize; x++) {
                    var currentPos = new Vector3Int(x, y, z);
                    if (hasLast)
                        last = ConstructFace(last, currentPos, Direction.RIGHT, Direction.LEFT);
                    else {
                        last.pos = currentPos;
                        FillData(ref last);
                        hasLast = true;
                    }
                }
                hasLast = false;
            }
        }
    }

    private void AddFace(in Vector3 center, Direction dir, BlockType type, int meshId) {
        var uvPos = (int)type;
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
    
    private const float _uvDelta = 1f / _uvMapSize;
    private void AddFaceInternal(in Vector3 a, in Vector3 b, in Vector3 c, in Vector3 d, in Vector3 center,
        int uvX, int uvY, int meshId) {
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

    public void Hide() {
        gameObject.SetActive(false);
    }
}
