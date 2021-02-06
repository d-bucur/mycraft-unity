using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldGenerator : MonoBehaviour {
    public int viewRange;
    public int generationDepth = 1;
    public int sectorSize;
    public List<NoiseMap> noiseMaps;
    public GameObject blockTemplate;

    private Dictionary<Vector2Int, Sector> _sectors = default;

    private void OnEnable() {
        GenerateInitialMap();
    }

    private void GenerateInitialMap() {
        for (var x = -viewRange; x <= viewRange; x++) {
            for (var y = -viewRange; y <= viewRange; y++) {
                GenerateSector(new Sector(new Vector2Int(x, y)));
            }
        }
    }

    private void GenerateSector(Sector sector) {
        for (var x = -sectorSize; x <= sectorSize; x++) {
            for (var y = -sectorSize; y <= sectorSize; y++) {
                var worldPosition = sector.offset * sectorSize + new Vector2Int(x, y);
                int groundZ = (int)SampleMaps(worldPosition);
                for (var z = groundZ - generationDepth + 1; z <= groundZ; z++) {
                    sector.blocks.Add(new Vector3Int(worldPosition.x, worldPosition.y, z), BlockType.Default);
                    var go = Instantiate(blockTemplate,
                        new Vector3(worldPosition.x, z, worldPosition.y),
                        Quaternion.identity, transform);
                    sector.GameObjects.Add(go);
                }
            }
        }
    }

    private float SampleMaps(Vector2Int pos) {
        return noiseMaps.Aggregate(0f, (res, map) => res + map.Sample(pos));
    }
}