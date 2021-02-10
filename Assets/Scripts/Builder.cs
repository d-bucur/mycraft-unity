using System;
using UnityEngine;

public class Builder : MonoBehaviour {
    public float maxDistance = 5f;
    public new Transform camera;
    public GameObject constructionBlock;
    
    private int _layerMask;

    private void Awake() {
        _layerMask = LayerMask.GetMask("World");
    }

    void Update() {
        RaycastHit hit;
        var hasHit = Physics.Raycast(new Ray(transform.position, camera.forward), out hit, maxDistance, _layerMask);
        if (!hasHit)
            return;

        var richochet = camera.forward * (-0.1f);
        var reboundPoint = hit.point + richochet;
        var target = new Vector3Int(
            Mathf.RoundToInt(reboundPoint.x),
            Mathf.RoundToInt(reboundPoint.y),
            Mathf.RoundToInt(reboundPoint.z)
        );
        constructionBlock.transform.position = target;

        if (Input.GetKeyDown(KeyCode.Mouse0)) {
            var sectorPos = new Vector2Int(
                Mathf.FloorToInt(target.x / (float)Sector.sectorSize), 
                Mathf.FloorToInt(target.z / (float)Sector.sectorSize));
            var sector = WorldGenerator.Instance._sectors[sectorPos];
            var gridPos = sector.WorldToInternalPos(target);
            // Debug.Log(String.Format("Building at ({0}): {1}", sectorPos, gridPos));
            sector.AddBlock(gridPos, BlockType.Grass);
            // TODO should only add new meshes instead of redrawing the whole sector
            sector.FillMesh();
        }
    }
}
