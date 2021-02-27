using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct SectorGenerationJob : IJobParallelFor {
    [ReadOnly] public NativeArray<NoiseMap> noiseMaps;
    [ReadOnly] public int2 sectorSize;
    [ReadOnly] public int2 sectorOffset;
    public NativeArray<BlockType> generatedBlocks;

    public void Execute(int index) {
        float pointHeight = 0f;
        var internalPos = idToPos(index);
        var planePos = Coordinates.InternalToPlanePos(sectorOffset, internalPos, sectorSize);
        for (int i = 0; i < noiseMaps.Length; i++) {
            float sample = noiseMaps[i].SampleJob(planePos.xz);
            pointHeight += sample;
        }
        generatedBlocks[index] = planePos.y < pointHeight ? BlockType.Grass : BlockType.Empty;
    }

    public int3 idToPos(int index) {
        return new int3(
            index / (sectorSize.x * sectorSize.y),
            index % sectorSize.y,
            (index / sectorSize.y) % sectorSize.x
        );
    }
}
