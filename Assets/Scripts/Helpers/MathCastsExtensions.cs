
using Unity.Mathematics;
using UnityEngine;

public static class MathCastsExtensions
{
    public static Vector2Int ToVector2Int (this int2 input) {
        return new Vector2Int(input.x, input.y);
    }
    public static int2 ToVector2Int (this Vector2Int input) {
        return new int2(input.x, input.y);
    }
    public static Vector3Int ToVector3Int (this int3 input) {
        return new Vector3Int(input.x, input.y, input.z);
    }
}