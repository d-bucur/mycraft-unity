using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct SectorGenerationJob : IJob {
    [ReadOnly] public NativeArray<NoiseMap> noiseMaps;
    [ReadOnly] public NoiseMap typeNoise;
    [ReadOnly] public int2 sectorSize;
    [ReadOnly] public int2 sectorOffset;
    [ReadOnly] public GroundTypeThresholds thresholds;
    [ReadOnly] public NativeHashMap<int3, BlockType> worldChanges;
    public NativeArray<BlockType> generatedBlocks;
    public NativeHashMap<int3, BlockType> neighbors;

    public void Execute() {
        float groundHeight = 0f;
        float typeNoiseSample = 0f;
        for (int index = 0; index < generatedBlocks.Length; index++) {
            var internalPos = Sector.IdToPos(index, sectorSize);
            var planePos = Coordinates.InternalToPlanePos(sectorOffset, internalPos, sectorSize);
            if (index % sectorSize.y == 0) {
                groundHeight = SampleMaps(planePos.xz);
                typeNoiseSample = SampleMaps(planePos.xz);
            }
            generatedBlocks[index] = GetBlockTypeWithDiffs(planePos, groundHeight, typeNoiseSample);
        }
        GenerateNeighbors();
    }

    private void GenerateNeighbors() {
        neighbors.Clear();
        for (int z = 0; z < sectorSize.x; z++)
            GenerateNeighborsInternal(-1, z);
        for (int x = 0; x < sectorSize.x; x++)
            GenerateNeighborsInternal(x, -1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GenerateNeighborsInternal(int x, int z) {
        var planePos = Coordinates.InternalToPlanePos(sectorOffset, new int3(x, 0, z), sectorSize);
        float groundHeight = SampleMaps(planePos.xz);
        float typeNoiseSample = typeNoise.Sample(planePos.xz);
        var internalPos = new int3(x, 0, z);
        for (int y = 0; y < sectorSize.y; y++) {
            internalPos.y = y;
            // TODO BUG HIGH typenoise is sometimes wrong on border
            var blockType = GetBlockTypeWithDiffs(planePos, groundHeight, typeNoiseSample);
            neighbors[internalPos] = blockType;
            planePos.y++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BlockType GetBlockTypeWithDiffs(in int3 planePos, float groundHeight, float typeNoiseSample) {
        return worldChanges.TryGetValue(planePos, out var type) ? 
            type : GetBlockType(planePos, groundHeight, typeNoiseSample);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float SampleMaps(in int2 planePos) {
        float groundHeight = 0f;
        for (int i = 0; i < noiseMaps.Length; i++) {
            float sample = noiseMaps[i].Sample(planePos);
            groundHeight += sample;
        }
        return groundHeight;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BlockType GetBlockType(in int3 planePos, float groundHeight, float noise = 0f) {
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
