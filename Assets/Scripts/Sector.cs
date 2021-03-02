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
    public Vector2Int offset;
    
    private readonly MeshHelper[] _meshHelpers = new MeshHelper[2];
    private bool _isMeshGenerated = false;
    private bool _isGridGenerated = false;
    public bool IsGridGenerated => _isGridGenerated;

    public static int sectorSize;
    public static int sectorSizeHeight;
    private static int sectorSizeMult;
    
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
    private void FinishGeneratingGrid() {
        _isGridGenerated = true;
        _isMeshGenerated = false;
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

    // TODO refactor generation methods - probably not needed anymore
    private void StartGeneratingMesh() {
        // TODO BUG sometimes is triggered before last one finished
        // TODO maybe not the best place for this?
        if (!_isGridGenerated) {
            FinishGeneratingGrid();
        }
        if (_isGridGenerated && _isMeshGenerated)
            return;
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
        _isMeshGenerated = true;
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    private static int GetTotalBlocks() {
        return sectorSize * sectorSize * sectorSizeHeight;
    }

    public void StartGeneratingGrid(SectorGenerationJob sectorGenerationJob) {
        foreach (var helper in _meshHelpers)
            helper.Clear();
        _isGridGenerated = false;
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

    private void OnDestroy() {
        blocksNative.Dispose();
        neighbors.Dispose();
        foreach (var mesh in _meshHelpers) {
            mesh.Dispose();
        }
    }

    public void RenderSectorParallel() {
        FinishGeneratingGrid(); // TODO sequence of calls is confusing, refactor
        RenderSectorsParallel(new List<Sector> {this});
    }

    public static void RenderSectorsParallel(List<Sector> sectorsToGenerate) {
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
}
