using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour {
    public int viewRange;
    public int sectorSize;
    public int sectorSizeHeight;
    public List<NoiseMap> noiseMaps;
    public float regenTimeBudget;
    public Sector sectorTemplate;
    
    private static WorldGenerator _instance;
    private Dictionary<Vector2Int, Sector> _sectors = new Dictionary<Vector2Int, Sector>();
    private Queue<Tuple<Vector2Int, Vector2Int>> _sectorsToUpdate = new Queue<Tuple<Vector2Int, Vector2Int>>();

    public static WorldGenerator Instance {
        get { return _instance; }
    }

    private void Awake() {
        _instance = this;
        Sector.SetSizes(sectorSize, sectorSizeHeight);
        GenerateInitialMap();
    }

    private void GenerateInitialMap() {
        for (var x = -viewRange; x <= viewRange; x++) {
            for (var y = -viewRange; y <= viewRange; y++) {
                var sector = Instantiate(sectorTemplate);
                sector.Init();
                GenerateSector(sector, new Vector2Int(x, y));
                _sectors.Add(sector.offset, sector);
            }
        }
    }

    private void GenerateSector(Sector sector, in Vector2Int pos) {
        sector.offset = pos;
        sector.transform.position = new Vector3(pos.x, 0, pos.y) * sectorSize;

        int lastX = Int32.MaxValue, lastZ = Int32.MaxValue;
        int groundHeight = 0;
        foreach (var blockPos in sector) {
            var worldPos = sector.InternalToWorldPos(blockPos);
            if (worldPos.z != lastZ || worldPos.x != lastX) {
                groundHeight = (int) SampleMaps(new Vector2Int(worldPos.x, worldPos.z));
                lastZ = worldPos.z;
                lastX = worldPos.x;
            }
            BlockType blockType;
            if (worldPos.y > groundHeight) {
                if (worldPos.y < - 15)
                    blockType = BlockType.Water;
                else
                    blockType = BlockType.Empty;
            }
            else {
                if (worldPos.y > 10)
                    blockType = BlockType.Snow;
                else if (worldPos.y < -10)
                    blockType = BlockType.Sand;
                else
                    blockType = BlockType.Grass;
            }
            // TODO temp debug
            // if (worldPos.y == 30)
            //     blockType = BlockType.Grass;
            sector.AddBlock(blockPos, blockType);
        }
        sector.FillMesh();
    }

    private float SampleMaps(in Vector2Int pos) {
        var res = 0.0f;
        for (var i = 0; i < noiseMaps.Count; i++)
            res += noiseMaps[i].Sample(pos);
        return res;
    }

    public void OnPlayerMoved(Vector2Int oldPos, Vector2Int newPos) {
        Debug.Log(String.Format("Player moved from sector {0} to {1}", oldPos, newPos));
         var delta = newPos - oldPos;
         if (Mathf.Abs(delta.x) > 0) {
             var removeX = oldPos.x - (int) Mathf.Sign(delta.x) * viewRange;
             var addX = newPos.x + (int) Mathf.Sign(delta.x) * viewRange;
             for (var y = oldPos.y - viewRange; y <= oldPos.y + viewRange; y++) {
                 var t = new Tuple<Vector2Int, Vector2Int>(new Vector2Int(removeX, y), new Vector2Int(addX, y));
                 _sectorsToUpdate.Enqueue(t);
             }
         }
         if (Mathf.Abs(delta.y) > 0) {
             var removeY = oldPos.y - (int) Mathf.Sign(delta.y) * viewRange;
             var addY = newPos.y + (int) Mathf.Sign(delta.y) * viewRange;
             for (var x = oldPos.x - viewRange; x <= oldPos.x + viewRange; x++) {
                 var t = new Tuple<Vector2Int, Vector2Int>(new Vector2Int(x, removeY), new Vector2Int(x, addY));
                 _sectorsToUpdate.Enqueue(t);
             }
         }
    }

    private void LateUpdate() {
        var startTime = Time.realtimeSinceStartup;
        while (_sectorsToUpdate.Count > 0) {
            _sectorsToUpdate.Dequeue().Deconstruct(
                out var removePos, 
                out var addPos
            );
            
            var sector = _sectors[removePos];
            //UnloadSector(sector);
            _sectors.Remove(removePos);
            GenerateSector(sector, addPos);
            _sectors.Add(addPos, sector);

            var deltaTime = Time.realtimeSinceStartup - startTime;
            if (deltaTime > regenTimeBudget) {
                Debug.LogWarning("Skipping update to next frame due to budget restriction");
                break;
            }
        }
    }
}