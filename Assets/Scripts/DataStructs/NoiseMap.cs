using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct NoiseMap {
    public float amplitude;
    public float frequency;
    public float power;
    public float2 offset;
    
    public float Sample(int2 pos) {
        var p =(float2)pos * frequency + offset;
        // var z = noise.cnoise(p);
        var z = Mathf.PerlinNoise(p.x, p.y) - 0.5f;
        // Debug.Log($"noise: {z}");
        z = math.pow(z, power);
        float res = z * amplitude;
        // Debug.Log($"returning {res}");
        return res;
    }
}
