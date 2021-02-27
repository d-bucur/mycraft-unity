using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct MeshGenerationJob : IJob {
    public NativeList<Vector3> vertices;
    public NativeList<Vector2> uvs;
    public NativeList<int> triangles;
    [ReadOnly] public int2 sectorSize;
    [ReadOnly] public NativeArray<BlockType> blocks;
    // TODO add another mesh for transparent 
    // TODO test if faster with math structs

    public void Execute() {
        SweepMeshFaces();
    }
    
    private struct SweepData {
        public Vector3Int pos;
        public BlockType type;
        public BlockGroup group;
    }

    private enum Direction {
        UP, DOWN, RIGHT, LEFT, FORWARD, BACK
    }
    
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

    private void SweepMeshFaces() {
        bool hasLast = false;
        SweepData last = default;

        // Sweep up
        // TODO is it faster to sweep by index instead of position?
        for (var x = 0; x < sectorSize.x; x++) {
            for (var z = 0; z < sectorSize.x; z++) {
                for (var y = 0; y < sectorSize.y; y++) {
                    var currentPos = new Vector3Int(x, y, z);
                    if (hasLast)
                        last = ConstructFace(last, currentPos, Direction.UP, Direction.DOWN);
                    else {
                        last.pos = currentPos;
                        last.type = SafeGetBlock(last.pos);
                        last.group = Block.GetGroup(last.type);
                        hasLast = true;
                    }
                }
                hasLast = false;
            }
        }

        // Sweep forward
        for (var x = 0; x < sectorSize.x; x++) {
            for (var y = 0; y < sectorSize.y; y++) {
                for (var z = 0; z < sectorSize.x; z++) {
                    var currentPos = new Vector3Int(x, y, z);
                    if (hasLast)
                        last = ConstructFace(last, currentPos, Direction.FORWARD, Direction.BACK);
                    else {
                        last.pos = currentPos;
                        last.type = SafeGetBlock(last.pos);
                        last.group = Block.GetGroup(last.type);
                        hasLast = true;
                    }
                }
                hasLast = false;
            }
        }

        // Sweep right
        for (var y = 0; y < sectorSize.y; y++) {
            for (var z = 0; z < sectorSize.x; z++) {
                for (var x = 0; x < sectorSize.x; x++) {
                    var currentPos = new Vector3Int(x, y, z);
                    if (hasLast)
                        last = ConstructFace(last, currentPos, Direction.RIGHT, Direction.LEFT);
                    else {
                        last.pos = currentPos;
                        last.type = SafeGetBlock(last.pos);
                        last.group = Block.GetGroup(last.type);
                        hasLast = true;
                    }
                }
                hasLast = false;
            }
        }
    }

    private SweepData ConstructFace(in SweepData previous, in Vector3Int currentPos, Direction currentDirection, Direction previousDirection) {
        var currentType = SafeGetBlock(currentPos);
        var currentGroup = Block.GetGroup(currentType);
        if (currentGroup != previous.group) {
            // draw solid surface from solid into empty
            if (currentGroup == BlockGroup.Transparent)
                AddFace(previous.pos, currentDirection, previous.type, 0);
            else
                AddFace(currentPos, previousDirection, currentType, 0);
        }
        else if (currentType == BlockType.Empty && previous.type == BlockType.Water) {
            // draw water surface from both sides
            // AddFace(previous.pos, currentDirection, previous.type, 1);
            // AddFace(currentPos, previousDirection, previous.type, 1);
        }
        return new SweepData {
            pos = currentPos,
            group = currentGroup,
            type = currentType,
        };
    }

    private BlockType SafeGetBlock(in Vector3Int lastPos) {
        var id = Sector.GetId(lastPos, sectorSize);
        return blocks[id];
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
        
        var i = vertices.Length;
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