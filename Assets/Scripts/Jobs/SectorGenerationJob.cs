using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct SectorGenerationJob : IJob {
    [ReadOnly] public NativeArray<NoiseMap> noiseMaps;
    [ReadOnly] public int2 sectorSize;
    [ReadOnly] public int2 sectorOffset;
    public NativeArray<BlockType> generatedBlocks;

    public void Execute() {
        float pointHeight = 0f;
        for (int index = 0; index < generatedBlocks.Length; index++) {
            var internalPos = Sector.IdToPos(index, sectorSize);
            var planePos = Coordinates.InternalToPlanePos(sectorOffset, internalPos, sectorSize);
            if (index % sectorSize.y == 0) {
                pointHeight = 0f;
                for (int i = 0; i < noiseMaps.Length; i++) {
                    float sample = noiseMaps[i].SampleJob(planePos.xz);
                    pointHeight += sample;
                }
            }
            generatedBlocks[index] = planePos.y < pointHeight ? BlockType.Grass : BlockType.Empty;
        }
    }
}
