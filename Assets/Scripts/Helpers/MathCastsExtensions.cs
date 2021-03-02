
using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class MathCastsExtensions
{
    public static Vector2Int ToVector2Int (this int2 input) {
        return new Vector2Int(input.x, input.y);
    }
    public static int2 ToInt2 (this Vector2Int input) {
        return new int2(input.x, input.y);
    }
    public static Vector3Int ToVector3Int (this int3 input) {
        return new Vector3Int(input.x, input.y, input.z);
    }
    public static int3 ToInt3 (this Vector3Int input) {
        return new int3(input.x, input.y, input.z);
    }
    public static void AddOrReplace<TK, TV>(this NativeHashMap<TK, TV> hashMap, TK key, TV value)
        where TK : struct, IEquatable<TK>
        where TV : struct
    {
        hashMap.Remove(key);
        hashMap.TryAdd(key, value);
    }

}