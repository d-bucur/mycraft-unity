using System.Collections.Generic;
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
    
    public JobHandle writeHandle;
    public NativeArray<BlockType> blocksNative;
    private Mesh _collisionMesh;

    public static void SetSizes(int sectorSize, int sectorSizeHeight) {
        Sector.sectorSize = sectorSize;
        Sector.sectorSizeHeight = sectorSizeHeight;
        sectorSizeMult = sectorSizeHeight * sectorSize;
    }

    public void Init() {
        var averageFaces = sectorSize * sectorSize * 2;
        for (int i = 0; i < _meshHelpers.Length; i++) {
            _meshHelpers[i] = new MeshHelper(averageFaces);
        }
        blocksNative = new NativeArray<BlockType>(GetTotalBlocks(), Allocator.Persistent);
    }

    public void AddBlock(in Vector3Int pos, BlockType blockType) {
        blocksNative[GetId(pos)] = blockType;
    }

    public void FinishGeneratingGrid() {
        _isGridGenerated = true;
        _isMeshGenerated = false;
    }

    private static int GetId(in Vector3Int pos) {
        return pos.x * sectorSizeMult + pos.z * sectorSizeHeight + pos.y;
    }

    public static int GetId(in Vector3Int pos, in int3 sectorSize) {
        return pos.x * sectorSize.z + pos.z * sectorSize.y + pos.y;
    }

    public static int3 IdToPos(int index, in int2 sectorSize) {
        return new int3(
            index / (sectorSize.x * sectorSize.y),
            index % sectorSize.y,
            (index / sectorSize.y) % sectorSize.x
        );
    }

    public void StartGeneratingMesh() {
        // TODO maybe not the best place for this?
        if (!_isGridGenerated) {
            FinishGeneratingGrid();
        }
        if (_isGridGenerated && _isMeshGenerated)
            return;
        
        foreach (var helper in _meshHelpers)
            helper.Clear();
        
        var job = new MeshGenerationJob {
            solidMesh = _meshHelpers[0],
            waterMesh = _meshHelpers[1],
            sectorSize = new int3(sectorSize, sectorSizeHeight, sectorSizeMult),
            blocks = blocksNative,
        };
        writeHandle = job.Schedule();
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
        // TODO use a different mesh?
        _collisionMesh = _meshHelpers[0].GetRenderMesh();
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

    public void StartGeneratingGrid() {
        _isGridGenerated = false;
    }

    private void OnDestroy() {
        blocksNative.Dispose();
        foreach (var mesh in _meshHelpers) {
            mesh.Dispose();
        }
    }

    public void RenderSectorParallel() {
        FinishGeneratingGrid(); // TODO sequence of calls is confusing, refactor
        RenderSectorsParallel(new List<Sector> {this});
    }

    public static void RenderSectorsParallel(List<Sector> sectorsToGenerate) {
        JobHandle.ScheduleBatchedJobs();
        foreach (var sector in sectorsToGenerate) {
            sector.writeHandle.Complete();
            sector.StartGeneratingMesh();
        }
        JobHandle.ScheduleBatchedJobs();
        var meshesToBake = new NativeArray<int>(sectorsToGenerate.Count, Allocator.TempJob);
        for (int i = 0; i < sectorsToGenerate.Count; i++) {
            var sector = sectorsToGenerate[i];
            sector.writeHandle.Complete();
            meshesToBake[i] = sector.PrepareCollisionMesh().GetInstanceID();
        }
        // TODO MED launch bake jobs independently?
        var bakeJob = new MeshBakeJob {meshIds = meshesToBake}
            .Schedule(meshesToBake.Length, 1);
        JobHandle.ScheduleBatchedJobs();
        foreach (var s in sectorsToGenerate)
            s.AssignRenderMesh();
        bakeJob.Complete();
        foreach (var s in sectorsToGenerate) {
            s.AssignCollisionMesh();
        }
    }
}
