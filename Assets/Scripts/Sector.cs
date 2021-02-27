using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
    
    public JobHandle writeHandle;
    public NativeArray<BlockType> blocksNative;
    private NativeList<int> _triangles;
    private NativeList<Vector2> _uvs;
    private NativeList<Vector3> _vertices;

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
        blocksNative = new NativeArray<BlockType>(GetTotalBlocks(), Allocator.Persistent);
        _triangles = new NativeList<int>(Allocator.Persistent);
        _uvs = new NativeList<Vector2>(Allocator.Persistent);
        _vertices = new NativeList<Vector3>(Allocator.Persistent);
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
        return pos.x * sectorSize.x + pos.z * sectorSize.y + pos.y;
    }

    public static int3 IdToPos(int index, in int2 sectorSize) {
        return new int3(
            index / (sectorSize.x * sectorSize.y),
            index % sectorSize.y,
            (index / sectorSize.y) % sectorSize.x
        );
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
            triangles = _triangles,
            uvs = _uvs,
            vertices = _vertices,
            sectorSize = new int2(sectorSize,sectorSizeHeight),
            blocks = blocksNative,
        };
        writeHandle = job.Schedule();
    }

    public void FinishGeneratingMesh() {
        // TODO find some copy that doesn't generate garbage
        _meshHelpers[0].triangles.SetArray(_triangles.ToArray());
        _meshHelpers[0].uvs.SetArray(_uvs.ToArray());
        _meshHelpers[0].vertices.SetArray(_vertices.ToArray());
        var solidsMesh = _meshHelpers[0].MakeMesh();
        GetComponent<MeshFilter>().mesh = solidsMesh;
        GetComponent<MeshCollider>().sharedMesh = solidsMesh;
        gameObject.SetActive(true);
        var transparentsMesh = _meshHelpers[1].MakeMesh();
        transform.GetChild(0).GetComponent<MeshFilter>().mesh = transparentsMesh;
        _isMeshGenerated = true;
        _triangles.Clear();
        _uvs.Clear();
        _vertices.Clear();
    }

    private void SweepMeshFaces() {
        // TODO persist and reuse native collections?
        var triangles = new NativeList<int>(Allocator.TempJob);
        var uvs = new NativeList<Vector2>(Allocator.TempJob);
        var vertices = new NativeList<Vector3>(Allocator.TempJob);
        var job = new MeshGenerationJob {
            triangles = triangles,
            uvs = uvs,
            vertices = vertices,
            sectorSize = new int2(sectorSize,sectorSizeHeight),
            blocks = blocksNative,
        };
        var jobHandle = job.Schedule();
        jobHandle.Complete();
        // TODO find some copy that doesn't generate garbage
        _meshHelpers[0].triangles.SetArray(job.triangles.ToArray());
        _meshHelpers[0].uvs.SetArray(job.uvs.ToArray());
        _meshHelpers[0].vertices.SetArray(job.vertices.ToArray());
        triangles.Dispose();
        uvs.Dispose();
        vertices.Dispose();
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    public static int GetTotalBlocks() {
        return sectorSize * sectorSize * sectorSizeHeight;
    }

    public void CopyJobData(NativeArray<BlockType> native) {
        native.CopyTo(_blocks);
    }

    public void StartGeneratingGrid() {
        _isGridGenerated = false;
    }

    private void OnDestroy() {
        blocksNative.Dispose();
        _uvs.Dispose();
        _triangles.Dispose();
        _vertices.Dispose();
    }
}
