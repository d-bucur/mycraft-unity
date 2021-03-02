using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct MeshGenerationJob : IJob {
    public MeshHelper solidMesh;
    public MeshHelper waterMesh;
    [ReadOnly] public int3 sectorSize;
    [ReadOnly] public NativeArray<BlockType> blocks;
    [ReadOnly] public NativeHashMap<int3, BlockType> neighbors;  // TODO deallocate on finish, when double generation bug is resolved

    public void Execute() {
        SweepMeshFaces();
        SweepBorderFaces();
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
    
    private static readonly Vector3 _nu = new Vector3(0, 1, 0);
    private static readonly Vector3 _nd = new Vector3(0, -1, 0);
    private static readonly Vector3 _nf = new Vector3(0, 0, 1);
    private static readonly Vector3 _nb = new Vector3(0, 0, -1);
    private static readonly Vector3 _nr = new Vector3(1, 0, 0);
    private static readonly Vector3 _nl = new Vector3(-1, 0, 0);
    
    private void SweepBorderFaces() {
        for (var z = 0; z < sectorSize.x; z++) {
            for (var y = 0; y < sectorSize.y; y++) {
                var lastPos = new int3(-1, y, z);
                var type = neighbors[lastPos];
                var last = new SweepData {
                    pos = lastPos.ToVector3Int(),
                    type = type,
                    group = Block.GetGroup(type)
                };
                var currentPos = new Vector3Int(0, y, z);
                ConstructFace(last, currentPos, Direction.RIGHT, Direction.LEFT);
            }
        }
        for (var x = 0; x < sectorSize.x; x++) {
            for (var y = 0; y < sectorSize.y; y++) {
                var lastPos = new int3(x, y, -1);
                var type = neighbors[lastPos];
                var last = new SweepData {
                    pos = lastPos.ToVector3Int(),
                    type = type,
                    group = Block.GetGroup(type)
                };
                var currentPos = new Vector3Int(x, y, 0);
                ConstructFace(last, currentPos, Direction.FORWARD, Direction.BACK);
            }
        }
    }
    
    private void SweepMeshFaces() {
        bool hasLast = false;
        SweepData last = default;

        // Sweep up
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
        for (var z = 0; z < sectorSize.x; z++) {
            for (var y = 0; y < sectorSize.y; y++) {
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
                AddFace(previous.pos, currentDirection, previous.type, ref solidMesh);
            else
                AddFace(currentPos, previousDirection, currentType, ref solidMesh);
        }
        else if (currentType == BlockType.Empty && previous.type == BlockType.Water) {
            // draw water surface from both sides
            AddFace(previous.pos, currentDirection, previous.type, ref waterMesh);
            AddFace(currentPos, previousDirection, previous.type, ref waterMesh);
        }
        return new SweepData {
            pos = currentPos,
            group = currentGroup,
            type = currentType,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BlockType SafeGetBlock(in Vector3Int lastPos) {
        return blocks[Sector.GetId(lastPos, sectorSize)];
    }

    private void AddFace(in Vector3 center, Direction dir, BlockType type, ref MeshHelper mesh) {
        var uvPos = (int)type;
        switch (dir) {
            case Direction.UP:
                AddFaceInternal(_rub, _lub, _luf, _ruf, center, 0, uvPos, ref mesh, _nu);
                break;
            case Direction.DOWN:
                AddFaceInternal(_rdf, _ldf, _ldb, _rdb, center, 2, uvPos, ref mesh, _nd);
                break;
            case Direction.RIGHT:
                AddFaceInternal(_rdb, _rub, _ruf, _rdf, center, 1, uvPos, ref mesh, _nr);
                break;
            case Direction.LEFT:
                AddFaceInternal(_ldf, _luf, _lub, _ldb, center, 1, uvPos, ref mesh, _nl);
                break;
            case Direction.FORWARD:
                AddFaceInternal(_rdf, _ruf, _luf, _ldf, center, 1, uvPos, ref mesh, _nf);
                break;
            case Direction.BACK:
                AddFaceInternal(_ldb, _lub, _rub, _rdb, center, 1, uvPos, ref mesh, _nb);
                break;
        }
    }
    
    private const float _uvDelta = 1f / _uvMapSize;
    private void AddFaceInternal(in Vector3 a, in Vector3 b, in Vector3 c, in Vector3 d, in Vector3 center,
        int uvX, int uvY, ref MeshHelper mesh, in Vector3 normal) {
        
        var i = mesh.vertices.Length;
        mesh.vertices.Add(center + a);
        mesh.vertices.Add(center + b);
        mesh.vertices.Add(center + c);
        mesh.vertices.Add(center + d);
        
        mesh.normals.Add(normal);
        mesh.normals.Add(normal);
        mesh.normals.Add(normal);
        mesh.normals.Add(normal);
        
        mesh.uvs.Add(new Vector2(uvX * _uvDelta, uvY * _uvDelta));
        mesh.uvs.Add(new Vector2(uvX * _uvDelta, uvY * _uvDelta + _uvDelta));
        mesh.uvs.Add(new Vector2(uvX * _uvDelta + _uvDelta, uvY * _uvDelta + _uvDelta));
        mesh.uvs.Add(new Vector2(uvX * _uvDelta + _uvDelta, uvY * _uvDelta));
        
        mesh.triangles.Add(i);
        mesh.triangles.Add(i+1);
        mesh.triangles.Add(i+3);
        
        mesh.triangles.Add(i+1);
        mesh.triangles.Add(i+2);
        mesh.triangles.Add(i+3);
    }
}