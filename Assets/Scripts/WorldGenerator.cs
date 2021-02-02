using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldGenerator : MonoBehaviour {
    public int viewRange;
    public List<NoiseMap> noiseMaps;
    public GameObject blockTemplate;
    public float blockSize = 1f;

    private void OnEnable() {
        GenerateInitialMap();
    }

    private void GenerateInitialMap() {
        var pos = new Vector2Int();
        for (var x = -viewRange; x <= viewRange; x++) {
            pos.x = x;
            for (var y = -viewRange; y <= viewRange; y++) {
                pos.y = y;
                var z = SampleMaps(pos);
                GameObject.Instantiate(blockTemplate, new Vector3(x, z, y), Quaternion.identity, transform);
            }
        }
    }

    private float SampleMaps(Vector2Int pos) {
        return noiseMaps.Aggregate(0f, (res, map) => res + map.Sample(pos));
    }
}