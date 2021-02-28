using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class WorldGenerator : MonoBehaviour {
    public int viewRange;
    public int sectorSize;
    public int sectorSizeHeight;
    public List<NoiseMap> noiseMaps;
    private NativeArray<NoiseMap> _noiseMapsNative;
    public NoiseMap typeNoise;
    public float regenTimeBudget;
    public Sector sectorTemplate;
    public GroundTypeThresholds groundTypeThresholds;
    public RandomizationType randomizationType; 
    public int seed;
    public int frameRateLimit;
    private NativeHashMap<int3, BlockType> _worldChanges;
    
    public enum RandomizationType {
        NoRandom,
        Random,
        Seeded,
    }

    private readonly Dictionary<Vector2Int, Sector> _activeSectors = new Dictionary<Vector2Int, Sector>();
    private readonly Queue<Sector> _sectorsReleased = new Queue<Sector>();
    private readonly Queue<Vector2Int> _sectorsToRender = new Queue<Vector2Int>();

    public static WorldGenerator Instance { get; private set; }

    private void Awake() {
        Instance = this;
        Sector.SetSizes(sectorSize, sectorSizeHeight);
        GenerateRandomness();
        PrepareNativeMaps();
        GenerateInitialMap();
    }

    private void PrepareNativeMaps() {
        _noiseMapsNative = new NativeArray<NoiseMap>(noiseMaps.Count, Allocator.Persistent);
        for (int i = 0; i < noiseMaps.Count; i++) {
            _noiseMapsNative[i] = noiseMaps[i];
        }
        _worldChanges = new NativeHashMap<int3, BlockType>(100, Allocator.Persistent);
    }

    private void OnDestroy() {
        _noiseMapsNative.Dispose();
        _worldChanges.Dispose();
    }

    private void GenerateRandomness() { 
        if (randomizationType == RandomizationType.NoRandom) 
            return;
        if (randomizationType == RandomizationType.Seeded) 
            Random.InitState(seed); 
        else if (randomizationType == RandomizationType.Random) 
            Random.InitState((int)DateTimeOffset.Now.ToUnixTimeSeconds()); 
        for (int i = 0; i < noiseMaps.Count; i++) {
            var map = noiseMaps[i];
            map.offset = new float2(Random.Range(-1000.0f, 1000.0f), Random.Range(-1000.0f, 1000.0f));
            noiseMaps[i] = map;
        }
    }

    private void Start() {
        if (frameRateLimit > 0)
            Application.targetFrameRate = frameRateLimit;
    }

    private void GenerateInitialMap() {
        var sectors = new List<Sector>();
        for (var x = -viewRange; x <= viewRange; x++) {
            for (var y = -viewRange; y <= viewRange; y++) {
                var sectorPos = new Vector2Int(x, y);
                sectors.Add(GetOrGenerateSector(sectorPos));
            }
        }
        Sector.RenderSectorsParallel(sectors);
    }

    private void GenerateSector(Sector sector, in Vector2Int pos) {
        sector.offset = pos;
        sector.transform.position = new Vector3(pos.x, 0, pos.y) * sectorSize;
        sector.StartGeneratingGrid();
        var job = new SectorGenerationJob {
            noiseMaps = _noiseMapsNative,
            typeNoise = typeNoise,
            generatedBlocks = sector.blocksNative,
            sectorSize = new int2(Sector.sectorSize, Sector.sectorSizeHeight),
            sectorOffset = pos.ToVector2Int(),
            thresholds = groundTypeThresholds,
            worldChanges = _worldChanges,
            neighbors = sector.neighbors,
        };
        // TODO bug sometimes is triggered before last one finished
        var handle = job.Schedule();
        sector.blocksJobHandle = handle;
        // TODO probably should not add until generation is done
        _activeSectors.Add(pos, sector);
    }

    private float SampleMaps(in int2 pos) {
        var res = 0.0f;
        for (var i = 0; i < noiseMaps.Count; i++)
            res += noiseMaps[i].Sample(pos);
        return res;
    }

    public Vector3 GetHeightAt(in int2 pos) {
        var y = SampleMaps(pos) + sectorSizeHeight / 2.0f;
        return new Vector3(pos.x, y, pos.y);
    }

    public void OnPlayerMoved(Vector2Int oldPos, Vector2Int newPos) {
        // Debug.Log(String.Format("Player moved from sector {0} to {1}", oldPos, newPos));
        var delta = newPos - oldPos;
        if (Mathf.Abs(delta.x) > 0) {
            var xBorder = (int) Mathf.Sign(delta.x) * viewRange;
            var addX = newPos.x + xBorder;
            var removeX = oldPos.x - xBorder;
            for (var d = -viewRange-1; d <= viewRange+1; d++) {
                var y = oldPos.y + d;
                ReleaseSector(new Vector2Int(removeX-delta.x, y));
                _sectorsToRender.Enqueue(new Vector2Int(addX, y));
            }
        }

        if (Mathf.Abs(delta.y) > 0) {
            var yBorder = (int) Mathf.Sign(delta.y) * viewRange;
            var addY = newPos.y + yBorder;
            var removeY = oldPos.y - yBorder;
            for (var d = -viewRange-1; d <= viewRange+1; d++) {
                var x = oldPos.x + d;
                ReleaseSector(new Vector2Int(x, removeY-delta.y));
                _sectorsToRender.Enqueue(new Vector2Int(x, addY));
            }
        }
        
        if (Mathf.Abs(delta.y) > 1 || Mathf.Abs(delta.x) > 1)
            Debug.LogError("Player delta is >1. Not handling this case");
    }

    private void ReleaseSector(Vector2Int pos) {
        if (!_activeSectors.TryGetValue(pos, out var sector))
            return;
        _sectorsReleased.Enqueue(sector);
        sector.Hide();
    }

    private void LateUpdate() {
        var sectorsToGenerate = new List<Sector>(_sectorsToRender.Count);
        while (_sectorsToRender.Count > 0 && sectorsToGenerate.Count < JobsUtility.JobWorkerCount) {
            var newPos = _sectorsToRender.Dequeue();
            sectorsToGenerate.Add(GetOrGenerateSector(newPos));
        }
        Sector.RenderSectorsParallel(sectorsToGenerate);
        // TODO repeat if frame budget available
    }

    private Sector GetOrGenerateSector(Vector2Int sectorPos) {
        Sector sector;
        if (_activeSectors.TryGetValue(sectorPos, out sector)) {
            if (sector.IsGridGenerated)
                return sector;
            else
                throw new InvalidProgramException("Sector in _active sectors that has not been generated");
        }
        if (_sectorsReleased.Count > 0) {
            sector = _sectorsReleased.Dequeue();
            _activeSectors.Remove(sector.offset);
        }
        else {
            sector = Instantiate(sectorTemplate);
            sector.Init();
        }
        GenerateSector(sector, sectorPos);
        return sector;
    }

    private Sector GetSector(Vector2Int sectorPos) {
        return _activeSectors[sectorPos];
    }

    public void ConstructBlock(Vector3Int worldPos) {
        var (sectorPos, internalPos) = Coordinates.WorldToInternalPos(worldPos);
        var sector = GetSector(sectorPos);
        sector.AddBlock(internalPos, BlockType.Grass);
        var planePos = Coordinates.InternalToPlanePos(sectorPos, internalPos);
        _worldChanges.AddOrReplace(planePos.ToInt3(), BlockType.Grass);
        sector.RenderSectorParallel();
        // TODO should only add new meshes instead of redrawing the whole sector
    }

    public void DestroyBlock(Vector3Int worldPos) {
        var (sectorPos, internalPos) = Coordinates.WorldToInternalPos(worldPos);
        var sector = GetSector(sectorPos);
        var planePos = Coordinates.InternalToPlanePos(sectorPos, internalPos);
        var blockType = planePos.y < groundTypeThresholds.water ? BlockType.Water : BlockType.Empty;
        sector.AddBlock(internalPos, blockType);
        _worldChanges.AddOrReplace(planePos.ToInt3(), blockType);
        sector.RenderSectorParallel();
        // TODO should only add new meshes instead of redrawing the whole sector
    }
}