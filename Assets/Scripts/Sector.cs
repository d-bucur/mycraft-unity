using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class Sector : MonoBehaviour {
    private readonly MeshHelper[] _meshHelpers = new MeshHelper[2];
    public Vector2Int offset => _offset;

    public static int sectorSize;
    public static int sectorSizeHeight;
    private static int sectorSizeMult;
    private Vector2Int _offset;
    
    public JobHandle meshJobHandle;
    public NativeArray<BlockType> blocksNative;
    public NativeHashMap<int3, BlockType> neighbors;
    private Mesh _collisionMesh;

    public static void SetSizes(int sectorSize, int sectorSizeHeight) {
        Sector.sectorSize = sectorSize;
        Sector.sectorSizeHeight = sectorSizeHeight;
        sectorSizeMult = sectorSizeHeight * sectorSize;
    }

    public void Init() {
        var averageFaces = sectorSize * sectorSize;
        for (int i = 0; i < _meshHelpers.Length; i++) {
            _meshHelpers[i] = new MeshHelper(averageFaces);
        }
        blocksNative = new NativeArray<BlockType>(GetTotalBlocks(), Allocator.Persistent);
        neighbors = new NativeHashMap<int3, BlockType>(sectorSize * sectorSize * 2, Allocator.Persistent);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddBlock(in Vector3Int pos, BlockType blockType) {
        blocksNative[GetId(pos)] = blockType;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetId(in Vector3Int pos) {
        return pos.x * sectorSizeMult + pos.z * sectorSizeHeight + pos.y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetId(in Vector3Int pos, in int3 sectorSize) {
        return pos.x * sectorSize.z + pos.z * sectorSize.y + pos.y;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int3 IdToPos(int index, in int2 sectorSize) {
        return new int3(
            index / (sectorSize.x * sectorSize.y),
            index % sectorSize.y,
            (index / sectorSize.y) % sectorSize.x
        );
    }

    private void AssignRenderMesh() {
        Profiler.BeginSample("Generating render mesh");
        var solidsMesh = _meshHelpers[0].GetRenderMesh();
        GetComponent<MeshFilter>().mesh = solidsMesh;
        var transparentsMesh = _meshHelpers[1].GetRenderMesh();
        transform.GetChild(0).GetComponent<MeshFilter>().mesh = transparentsMesh;
        Profiler.EndSample();
    }

    private Mesh PrepareCollisionMesh() {
        Profiler.BeginSample("Generating collision mesh");
        _collisionMesh = _meshHelpers[0].GetCollisionMesh();
        Profiler.EndSample();
        return _collisionMesh;
    }

    private void AssignCollisionMesh() {
        var meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = _collisionMesh;
        gameObject.SetActive(true);
    }

    private static int GetTotalBlocks() {
        return sectorSize * sectorSize * sectorSizeHeight;
    }

    public void StartMeshGeneration(SectorGenerationJob sectorGenerationJob) {
        foreach (var helper in _meshHelpers)
            helper.Clear();
        var generationHandle = sectorGenerationJob.Schedule();
        var meshHandle = new MeshGenerationJob {
            solidMesh = _meshHelpers[0],
            waterMesh = _meshHelpers[1],
            sectorSize = new int3(sectorSize, sectorSizeHeight, sectorSizeMult),
            blocks = blocksNative,
            neighbors = neighbors,
        };
        meshJobHandle = meshHandle.Schedule(generationHandle);
    }

    public static void AssignMeshesParallel(List<Sector> sectorsToGenerate) {
        if (sectorsToGenerate.Count == 0)
            return;
        var bakeJobs = new List<JobHandle>(sectorsToGenerate.Count);
        for (int i = 0; i < sectorsToGenerate.Count; i++) {
            var sector = sectorsToGenerate[i];
            sector.meshJobHandle.Complete();
            var meshId = sector.PrepareCollisionMesh().GetInstanceID();
            var job = new MeshBakeJob {meshId = meshId}.Schedule();
            bakeJobs.Add(job);
        }
        JobHandle.ScheduleBatchedJobs();
        foreach (var s in sectorsToGenerate)
            s.AssignRenderMesh();
        for (var i = 0; i < sectorsToGenerate.Count; i++) {
            bakeJobs[i].Complete();
            var s = sectorsToGenerate[i];
            s.AssignCollisionMesh();
        }
    }

    public void AssignMeshParallel() {
        AssignMeshesParallel(new List<Sector> {this});
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    private void OnDestroy() {
        blocksNative.Dispose();
        neighbors.Dispose();
        foreach (var mesh in _meshHelpers) {
            mesh.Dispose();
        }
    }

    public void SetOffset(in Vector2Int pos) {
        _offset = pos;
        transform.position = new Vector3(pos.x, 0, pos.y) * sectorSize;
    }
}
