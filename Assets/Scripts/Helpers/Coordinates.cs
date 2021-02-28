using Unity.Mathematics;
using UnityEngine;

internal static class Coordinates {
    /** There are 3 types of coordinates:
     * sampling plane
     * world
     * sector internal
     * This class offers conversion methods between them
     */
    public static Vector3Int RoundWorldPos(in Vector3 pos) {
        return new Vector3Int(
            Mathf.RoundToInt(pos.x),
            Mathf.RoundToInt(pos.y),
            Mathf.RoundToInt(pos.z)
        );
    }

    public static (Vector2Int sectorPos, Vector3Int internalPos) WorldToInternalPos(in Vector3Int pos) {
        var sectorPos = new Vector2Int(
            Mathf.FloorToInt(pos.x / (float) Sector.sectorSize),
            Mathf.FloorToInt(pos.z / (float) Sector.sectorSize));
        var internalPos = new Vector3Int(
            pos.x - sectorPos.x * Sector.sectorSize,
            pos.y,
            pos.z - sectorPos.y * Sector.sectorSize
        );
        return (sectorPos, internalPos);
    }

    public static Vector3Int InternalToPlanePos(in Vector2Int sector, in Vector3Int pos) {
        return new Vector3Int(
            pos.x + sector.x * Sector.sectorSize,
            pos.y - Sector.sectorSizeHeight / 2,
            pos.z + sector.y * Sector.sectorSize
        );
    }
    
    public static int3 InternalToPlanePos(in int2 sector, in int3 pos, in int2 sectorSize) {
        return new int3(
            pos.x + sector.x * sectorSize.x,
            pos.y - sectorSize.y / 2,
            pos.z + sector.y * sectorSize.x
        );
    }
}