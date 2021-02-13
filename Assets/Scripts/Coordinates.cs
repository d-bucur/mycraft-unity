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
            pos.y, // TODO cannot be. all types have different y axis
            pos.z - sectorPos.y * Sector.sectorSize
        );
        return (sectorPos, internalPos);
    }

    public static Vector3Int InternalToPlanePos(Vector2Int sector, Vector3Int pos) {
        // TODO create new struct directly
        pos.x += sector.x * Sector.sectorSize;
        pos.z += sector.y * Sector.sectorSize;
        pos.y -= Sector.sectorSizeHeight / 2;
        return pos;
    }
}