using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldGenerator : MonoBehaviour {
    public int viewRange;
    public int generationDepth = 1;
    public int sectorSize;
    public List<NoiseMap> noiseMaps;
    public GameObject blockTemplate;
    private List<GameObject> _freeBlockGOs = new List<GameObject>();

    private Dictionary<Vector2Int, Sector> _sectors = new Dictionary<Vector2Int, Sector>();
    private static WorldGenerator _instance;
    
    public static WorldGenerator Instance {
        get { return _instance; }
    }

    private void Awake() {
        _instance = this;
        GenerateInitialMap();
    }

    private void GenerateInitialMap() {
        for (var x = -viewRange; x <= viewRange; x++) {
            for (var y = -viewRange; y <= viewRange; y++) {
                var sector = new Sector(new Vector2Int(x, y));
                GenerateSector(sector);
                _sectors.Add(sector.offset, sector);
            }
        }
    }

    private void GenerateSector(Sector sector) {
        var sectorBase = sector.offset * (2 * sectorSize);
        for (var x = -sectorSize; x <= sectorSize; x++) {
            for (var y = -sectorSize; y <= sectorSize; y++) {
                var worldCoord = sectorBase + new Vector2Int(x, y);
                int groundZ = (int)SampleMaps(worldCoord);
                for (var z = groundZ - generationDepth + 1; z <= groundZ; z++) {
                    sector.blocks.Add(new Vector3Int(worldCoord.x, worldCoord.y, z), BlockType.Default);
                    var worldPos = new Vector3(worldCoord.x, z, worldCoord.y);
                    var go = CreateGO(worldPos);
                    sector.GameObjects.Add(go);
                }
            }
        }
    }

    private GameObject CreateGO(Vector3 worldPos) {
        if (_freeBlockGOs.Count > 0) {
            var go = _freeBlockGOs.Last();
            _freeBlockGOs.RemoveAt(_freeBlockGOs.Count-1);
            go.SetActive(true);
            go.transform.position = worldPos;
            return go;
        }
        else
            return Instantiate(blockTemplate, worldPos, Quaternion.identity, transform);
    }

    private void UnloadSector(Sector sector, bool softRemoval = false) {
        sector.blocks = null;
        _freeBlockGOs.AddRange(sector.GameObjects);
        if (softRemoval) return;
        foreach (var blockGO in sector.GameObjects) {
            blockGO.SetActive(false);
        }
    }

    private float SampleMaps(Vector2Int pos) {
        return noiseMaps.Aggregate(0f, (res, map) => res + map.Sample(pos));
    }

    public void OnPlayerMoved(Vector2Int oldPos, Vector2Int newPos) {
        Debug.Log(String.Format("Player moved from sector {0} to {1}", oldPos, newPos));
        UnloadSector(_sectors[oldPos]);
        _sectors.Remove(oldPos);
        var newSector = new Sector(newPos);
        GenerateSector(newSector);
        _sectors.Add(newPos, newSector);
    }
}