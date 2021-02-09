using System;

public enum BlockType {
    Water = 0,
    Sand = 1,
    Snow = 2,
    Grass = 3,
    Empty = 4,
}

public enum BlockGroup {
    Solid = 0,
    Transparent = 1,
}

public struct Block {
    public static BlockGroup GetGroup(BlockType type) {
        switch (type) {
            case BlockType.Empty: return BlockGroup.Transparent;
            case BlockType.Water: return BlockGroup.Transparent;
            case BlockType.Snow: return BlockGroup.Solid;
            case BlockType.Sand: return BlockGroup.Solid;
            case BlockType.Grass: return BlockGroup.Solid;
        }
        throw new ArgumentException("Unknown block type");
    }
}