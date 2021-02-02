using System;
using UnityEngine;

[Serializable]
public struct NoiseMap {
    public float amplitude;
    public float frequency;
    public float power;
    public Vector2 offset;

    public float Sample(Vector2Int pos) {
        var p = (Vector2)pos * frequency + offset;
        var z = Mathf.PerlinNoise(p.x, p.y) - 0.5f;
        z = Mathf.Pow(z, power);
        return z * amplitude;
    }
}
