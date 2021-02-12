using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class WorldGenerator : MonoBehaviour {
    public int viewRange;
    public int sectorSize;
    public int sectorSizeHeight;
    public List<NoiseMap> noiseMaps;
    public NoiseMap typeNoise;
    public float regenTimeBudget;
    public Sector sectorTemplate;
    public int waterTreshold;
    public int snowTreshold;
    public int sandTreshold;
    public RandomizationType randomizationType; 
    public int seed;
    public int frameRateLimit;
    
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
        GenerateInitialMap();
        Random.InitState(seed);
    }

    private void GenerateRandomness() { 
        if (randomizationType == RandomizationType.NoRandom) 
            return;
        if (randomizationType == RandomizationType.Seeded) 
            Random.InitState(seed); 
        else if (randomizationType == RandomizationType.Random) 
            Random.InitState((int)DateTimeOffset.Now.ToUnixTimeSeconds()); 
        foreach (var t in noiseMaps) 
            t.offset = new Vector2(Random.Range(-1000.0f, 1000.0f), Random.Range(-1000.0f, 1000.0f));
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

        int lastX = Int32.MaxValue, lastZ = Int32.MaxValue;
        int groundHeight = 0;
        int typeNoiseSample = 0;
        foreach (var blockPos in sector) {
            var worldPos = sector.InternalToWorldPos(blockPos);
            if (worldPos.z != lastZ || worldPos.x != lastX) {
                var gridPos = new Vector2Int(worldPos.x, worldPos.z);
                groundHeight = (int) SampleMaps(gridPos);
                typeNoiseSample = (int) typeNoise.Sample(gridPos);
                lastZ = worldPos.z;
                lastX = worldPos.x;
            }
            BlockType blockType = GetBlockType(worldPos, groundHeight, typeNoiseSample);
            sector.AddBlock(blockPos, blockType);
        }
        sector.FinishGeneratingGrid();
        _activeSectors.Add(pos, sector);
    }

    private BlockType GetBlockType(Vector3Int worldPos, int groundHeight, int noise = 0) {
        BlockType blockType;
        if (worldPos.y > groundHeight) {
            if (worldPos.y < waterTreshold)
                blockType = BlockType.Water;
            else
                blockType = BlockType.Empty;
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
        Debug.Log(String.Format("Player moved from sector {0} to {1}", oldPos, newPos));
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
            // TODO hidden sectors on the side get left out of release
            // TODO bug when moving diagonally
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
}