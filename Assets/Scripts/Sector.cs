using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

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
    
    public JobHandle writeHandle;
    public NativeArray<BlockType> blocksNative;
    private Mesh _collisionMesh;

    public static void SetSizes(int sectorSize, int sectorSizeHeight) {
        Sector.sectorSize = sectorSize;
        Sector.sectorSizeHeight = _zSize = sectorSizeHeight;
        _xSize = sectorSizeHeight * sectorSize;
    }

    public void Init() {
        _blocks = new BlockType[sectorSize * sectorSize * sectorSizeHeight];
        var averageFaces = sectorSize * sectorSize * 2;
        for (int i = 0; i < _meshHelpers.Length; i++) {
            _meshHelpers[i] = new MeshHelper(averageFaces);
        }
        blocksNative = new NativeArray<BlockType>(GetTotalBlocks(), Allocator.Persistent);
    }

    public void AddBlock(in Vector3Int pos, BlockType blockType) {
        _blocks[GetId(pos)] = blockType;
    }

    public void FinishGeneratingGrid() {
        _isGridGenerated = true;
        _isMeshGenerated = false;
    }

    public static int GetId(in Vector3Int pos) {
        return pos.x * _xSize + pos.z * _zSize + pos.y;
    }

    public static int GetId(in Vector3Int pos, in int2 sectorSize) {
        // TODO cache
        return pos.x * sectorSize.x * sectorSize.y + pos.z * sectorSize.y + pos.y;
    }

    public static int3 IdToPos(int index, in int2 sectorSize) {
        return new int3(
            index / (sectorSize.x * sectorSize.y),
            index % sectorSize.y,
            (index / sectorSize.y) % sectorSize.x
        );
    }

    // TODO remove
    public IEnumerator<Vector3Int> GetEnumerator() {
        for (var x = 0; x < sectorSize; x++)
            for (var z = 0; z < sectorSize; z++)
                for (var y = 0; y < sectorSizeHeight; y++)
                    yield return new Vector3Int(x, y, z);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public void StartGeneratingMesh() {
        if (!_isGridGenerated) {
            CopyJobData(blocksNative);
            FinishGeneratingGrid();
        }
        if (_isGridGenerated && _isMeshGenerated)
            return;
        
        foreach (var helper in _meshHelpers)
            helper.Clear();
        
        var job = new MeshGenerationJob {
            mesh = _meshHelpers[0],
            sectorSize = new int2(sectorSize,sectorSizeHeight),
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
        // TODO should maybe use a different mesh?
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

    private void CopyJobData(NativeArray<BlockType> native) {
        native.CopyTo(_blocks);
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

    public static void GenerateSectorsParallel(List<Sector> sectorsToGenerate) {
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
        // TODO maybe launch bake jobs independently?
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
