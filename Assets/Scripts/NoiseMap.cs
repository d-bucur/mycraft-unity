using System;
using UnityEngine;

[Serializable]
public struct NoiseMap {
    public float amplitude;
    public float frequency;
    public float power;
    public Vector2 offset;

    public float Sample(Vector2Int pos) {
        var px = pos.x * frequency + offset.x;
        var py = pos.y * frequency + offset.y;
        var z = Mathf.PerlinNoise(px, py) - 0.5f;
        z = Mathf.Pow(z, power);
        return z * amplitude;
    }
}
