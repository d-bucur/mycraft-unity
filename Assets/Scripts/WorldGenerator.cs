using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour {
    public int viewRange;
    public int sectorSize;
    public List<NoiseMap> noiseMaps;
    public float regenTimeBudget;
    public GameObject blockTemplate;
    public Sector sectorTemplate;
    public SectorGenerator sectorGenerator;
    
    private static WorldGenerator _instance;
    private Dictionary<Vector2Int, Sector> _sectors = new Dictionary<Vector2Int, Sector>();
    private Queue<Tuple<Vector2Int, Vector2Int>> _sectorsToUpdate = new Queue<Tuple<Vector2Int, Vector2Int>>();

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
                var sector = Instantiate(
                    sectorTemplate, 
                    new Vector3(x, 0, y) * sectorSize, 
                    Quaternion.identity
                    );
                sector.offset = new Vector2Int(x, y);
                GenerateSector(sector);
                _sectors.Add(sector.offset, sector);
            }
        }
    }

    private void GenerateSector(Sector sector) {
        var sectorSizeLimit = (float)sectorSize / 2;
        var sectorBase = sector.offset * sectorSize;
        for (var x = Mathf.CeilToInt(-sectorSizeLimit); x < sectorSizeLimit; x++) {
            for (var y = Mathf.CeilToInt(-sectorSizeLimit); y < sectorSizeLimit; y++) {
                var worldCoord = sectorBase + new Vector2Int(x, y);
                int groundZ = (int)SampleMaps(worldCoord);
                // TODO handle Z properly
                for (var z = -25; z < 25; z++) {
                    var blockType = (z <= groundZ) ? BlockType.Default : BlockType.Empty;
                    sector.blocks.Add(new Vector3Int(x, z, y), blockType);
                    // TODO unly used for debugging
                    // var worldPos = new Vector3(worldCoord.x, z, worldCoord.y);
                    // if (blockType == BlockType.Default)
                    //     Instantiate(blockTemplate, worldPos, Quaternion.identity);
                }
            }
        }
        sectorGenerator.FillSectorMesh(sector);
    }

    private void UnloadSector(Sector sector) {
        // _freeBlocksUncommited.AddRange(sector.GameObjects);
        // sector.blocks.Clear();
        // sector.GameObjects.Clear();
    }

    private float SampleMaps(Vector2Int pos) {
        var res = 0.0f;
        for (var i = 0; i < noiseMaps.Count; i++)
            res += noiseMaps[i].Sample(pos);
        return res;
    }

    public void OnPlayerMoved(Vector2Int oldPos, Vector2Int newPos) {
        //Debug.Log(String.Format("Player moved from sector {0} to {1}", oldPos, newPos));
        // var delta = newPos - oldPos;
        // if (Mathf.Abs(delta.x) > 0) {
        //     var removeX = oldPos.x - (int) Mathf.Sign(delta.x) * viewRange;
        //     var addX = newPos.x + (int) Mathf.Sign(delta.x) * viewRange;
        //     for (var y = oldPos.y - viewRange; y <= oldPos.y + viewRange; y++) {
        //         var t = new Tuple<Vector2Int, Vector2Int>(new Vector2Int(removeX, y), new Vector2Int(addX, y));
        //         _sectorsToUpdate.Enqueue(t);
        //     }
        // }
        // if (Mathf.Abs(delta.y) > 0) {
        //     var removeY = oldPos.y - (int) Mathf.Sign(delta.y) * viewRange;
        //     var addY = newPos.y + (int) Mathf.Sign(delta.y) * viewRange;
        //     for (var x = oldPos.x - viewRange; x <= oldPos.x + viewRange; x++) {
        //         var t = new Tuple<Vector2Int, Vector2Int>(new Vector2Int(x, removeY), new Vector2Int(x, addY));
        //         _sectorsToUpdate.Enqueue(t);
        //     }
        // }
    }

    private void LateUpdate() {
        var startTime = Time.realtimeSinceStartup;
        while (_sectorsToUpdate.Count > 0) {
            _sectorsToUpdate.Dequeue().Deconstruct(
                out var removePos, 
                out var addPos
            );
            
            var sector = _sectors[removePos];
            UnloadSector(sector);
            _sectors.Remove(removePos);
            
            sector.offset = addPos;
            GenerateSector(sector);
            _sectors.Add(addPos, sector);

            var deltaTime = Time.realtimeSinceStartup - startTime;
            if (deltaTime > regenTimeBudget) {
                Debug.LogWarning("Skipping update to next frame due to budget restriction");
                break;
            }
        }
        CommitBlockChanges();
    }

    private void CommitBlockChanges() {
        /*foreach (var go in _freeBlocksUncommited)
            go.gameObject.SetActive(false);
        _freeBlocksDeactivated.AddRange(_freeBlocksUncommited);
        _freeBlocksUncommited.Clear();
        //Debug.Log("Block count: " + transform.childCount);*/
    }
}