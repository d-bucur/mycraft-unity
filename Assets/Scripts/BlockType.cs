using System;
using System.Runtime.CompilerServices;

public enum BlockType : byte {
    Water = 0,
    Sand = 1,
    Snow = 2,
    Grass = 3,
    Empty = 4,
}

public enum BlockGroup : byte {
    Solid = 0,
    Transparent = 1,
}

public struct Block {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlockGroup GetGroup(BlockType type) {
        return type switch {
            BlockType.Empty => BlockGroup.Transparent,
            BlockType.Water => BlockGroup.Transparent,
            BlockType.Snow => BlockGroup.Solid,
            BlockType.Sand => BlockGroup.Solid,
            BlockType.Grass => BlockGroup.Solid,
            _ => throw new ArgumentException("Unknown block type")
        };
    }
}