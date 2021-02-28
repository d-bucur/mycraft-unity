using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct NoiseMap {
    public float amplitude;
    public float frequency;
    public float power;
    public float2 offset;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Sample(int2 pos) {
        var p =(float2)pos * frequency + offset;
        // var z = noise.cnoise(p);  // TODO PERF try noise library
        var z = Mathf.PerlinNoise(p.x, p.y) - 0.5f;
        z = math.pow(z, power);
        return z * amplitude;
    }
}
