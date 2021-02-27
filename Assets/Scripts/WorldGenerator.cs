using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(WorldChanges))]
public class WorldGenerator : MonoBehaviour {
    public int viewRange;
    public int sectorSize;
    public int sectorSizeHeight;
    public List<NoiseMap> noiseMaps;
    private NativeArray<NoiseMap> _noiseMapsNative;
    public NoiseMap typeNoise;
    public float regenTimeBudget;
    public Sector sectorTemplate;
    public int waterTreshold;
    public int snowTreshold;
    public int sandTreshold;
    public RandomizationType randomizationType; 
    public int seed;
    public int frameRateLimit;
    private WorldChanges _worldChanges;
    
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
        _worldChanges = GetComponent<WorldChanges>();
        Sector.SetSizes(sectorSize, sectorSizeHeight);
        PrepareNativeMaps();
        GenerateRandomness();
        GenerateInitialMap();
        Random.InitState(seed);
    }

    private void PrepareNativeMaps() {
        _noiseMapsNative = new NativeArray<NoiseMap>(noiseMaps.Count, Allocator.Persistent);
        for (int i = 0; i < noiseMaps.Count; i++) {
            _noiseMapsNative[i] = noiseMaps[i];
        }
    }

    private void OnDestroy() {
        _noiseMapsNative.Dispose();
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
        for (var x = -viewRange; x <= viewRange; x++) {
            for (var y = -viewRange; y <= viewRange; y++) {
                var sectorPos = new Vector2Int(x, y);
                GetOrGenerateSector(sectorPos);
            }
        }
        foreach (var sector in _activeSectors.ToList()) {
            sector.Value.GenerateMesh();
        }
    }

    private void GenerateSector(Sector sector, in Vector2Int pos) {
        sector.offset = pos;
        sector.transform.position = new Vector3(pos.x, 0, pos.y) * sectorSize;
        sector.StartGeneratingGrid();
        
        int sectorBlocks = Sector.GetTotalBlocks();
        int batchCount = sectorBlocks / JobsUtility.JobWorkerCount;
        // Debug.Log($"Generating sector {pos} with {sectorBlocks} blocks divided into chunks of {batchCount}");
        sector.generatedBlocks = new NativeArray<BlockType>(sectorBlocks, Allocator.TempJob);
        // TODO optimization: only do sampling job once for a single x,y point
        var job = new SectorGenerationJob {
            noiseMaps = _noiseMapsNative,
            generatedBlocks = sector.generatedBlocks,
            sectorSize = new int2(Sector.sectorSize, Sector.sectorSizeHeight),
            sectorOffset = pos.ToVector2Int(),
        };
        var handle = job.Schedule(sectorBlocks, batchCount);
        sector.writeHandle = handle;
        _activeSectors.Add(pos, sector);
    }

    private void GenerateSectorOld(Sector sector, in Vector2Int pos) {
        sector.offset = pos;
        sector.transform.position = new Vector3(pos.x, 0, pos.y) * sectorSize;

        int lastX = Int32.MaxValue, lastZ = Int32.MaxValue;
        int groundHeight = 0;
        int typeNoiseSample = 0;
        foreach (var blockPos in sector) {
            var planePos = Coordinates.InternalToPlanePos(sector.offset, blockPos);
            if (planePos.z != lastZ || planePos.x != lastX) {
                var gridPos = new Vector2Int(planePos.x, planePos.z);
                groundHeight = (int) SampleMaps(gridPos);
                typeNoiseSample = (int) typeNoise.Sample(gridPos);
                lastZ = planePos.z;
                lastX = planePos.x;
            }
            BlockType blockType = _worldChanges.TryGetValue(planePos, out var diffType) ? 
                diffType : 
                GetBlockType(planePos, groundHeight, typeNoiseSample);
            sector.AddBlock(blockPos, blockType);
        }
        sector.FinishGeneratingGrid();
        _activeSectors.Add(pos, sector);
    }

    private BlockType GetBlockType(Vector3Int worldPos, int groundHeight, int noise = 0) {
        BlockType blockType;
        if (worldPos.y > groundHeight) {
            blockType = worldPos.y < waterTreshold ? BlockType.Water : BlockType.Empty;
        }
        else {
            if (worldPos.y + noise > snowTreshold)
                blockType = BlockType.Snow;
            else if (worldPos.y + noise < sandTreshold)
                blockType = BlockType.Sand;
            else
                blockType = BlockType.Grass;
        }

        return blockType;
    }

    private float SampleMaps(in Vector2Int pos) {
        var res = 0.0f;
        for (var i = 0; i < noiseMaps.Count; i++)
            res += noiseMaps[i].Sample(pos);
        return res;
    }

    public Vector3 GetHeightAt(in Vector2Int pos) {
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
        var startTime = Time.realtimeSinceStartup;
        while (_sectorsToRender.Count > 0) {
            var newPos = _sectorsToRender.Dequeue();
            var sector = GetOrGenerateSector(newPos);
            sector.GenerateMesh();

            var deltaTime = Time.realtimeSinceStartup - startTime;
            if (deltaTime > regenTimeBudget) {
                // Skip further rendering to next frame due to budget restriction
                break;
            }
        }
    }
    
    public Sector GetOrGenerateSector(Vector2Int sectorPos) {
        Sector sector;
        if (_activeSectors.TryGetValue(sectorPos, out sector)) {
            if (sector.IsGridGenerated) {
                return sector;
            }
            else {
                throw new InvalidProgramException("Sector in _active sectors that has not been generated");
            }
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

    public Sector GetSector(Vector2Int sectorPos) {
        return _activeSectors[sectorPos];
    }

    public void ConstructBlock(Vector3Int worldPos) {
        var (sectorPos, internalPos) = Coordinates.WorldToInternalPos(worldPos);
        var sector = GetSector(sectorPos);
        sector.AddBlock(internalPos, BlockType.Grass);
        var planePos = Coordinates.InternalToPlanePos(sectorPos, internalPos);
        _worldChanges.Add(planePos, BlockType.Grass);
        // TODO should only add new meshes instead of redrawing the whole sector
        sector.FinishGeneratingGrid();
        sector.GenerateMesh();

    }

    public void DestroyBlock(Vector3Int worldPos) {
        var (sectorPos, internalPos) = Coordinates.WorldToInternalPos(worldPos);
        var sector = GetSector(sectorPos);
        // Debug.Log(String.Format("Building at ({0}): {1}", sectorPos, gridPos));
        var planePos = Coordinates.InternalToPlanePos(sectorPos, internalPos);
        var blockType = planePos.y < waterTreshold ? BlockType.Water : BlockType.Empty;
        sector.AddBlock(internalPos, blockType);
        _worldChanges.Add(planePos, blockType);
        // TODO should only add new meshes instead of redrawing the whole sector
        sector.FinishGeneratingGrid();
        sector.GenerateMesh();
    }
}