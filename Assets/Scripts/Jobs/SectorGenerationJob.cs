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
    [ReadOnly] public GroundTypeThresholds thresholds;
    public NativeArray<BlockType> generatedBlocks;

    public void Execute() {
        float groundHeight = 0f;
        for (int index = 0; index < generatedBlocks.Length; index++) {
            var internalPos = Sector.IdToPos(index, sectorSize);
            var planePos = Coordinates.InternalToPlanePos(sectorOffset, internalPos, sectorSize);
            if (index % sectorSize.y == 0) {
                groundHeight = 0f;
                for (int i = 0; i < noiseMaps.Length; i++) {
                    float sample = noiseMaps[i].Sample(planePos.xz);
                    groundHeight += sample;
                }
            }
            generatedBlocks[index] = GetBlockType(planePos, groundHeight);
        }
    }

    private BlockType GetBlockType(int3 planePos, float groundHeight, int noise = 0) {
        BlockType blockType;
        if (planePos.y > groundHeight) {
            blockType = planePos.y < thresholds.water ? BlockType.Water : BlockType.Empty;
        }
        else {
            if (planePos.y + noise > thresholds.snow)
                blockType = BlockType.Snow;
            else if (planePos.y + noise < thresholds.sand)
                blockType = BlockType.Sand;
            else
                blockType = BlockType.Grass;
        }
        return blockType;
    }
}
