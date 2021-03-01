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
        // math.cnoise is actually slower, strangely enough, even in burst
        var z = Mathf.PerlinNoise(p.x, p.y) - 0.5f;
        z = math.pow(z, power);
        return z * amplitude;
    }
}
