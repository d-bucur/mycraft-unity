using System.Collections.Generic;

public enum BlockType {
    Water = 0,
    Sand = 1,
    Snow = 2,
    Grass = 3,
    Empty = 4
}

public enum BlockGroup {
    Solid = 0,
    Transparent = 1,
}

public struct Block {
    public static readonly Dictionary<BlockType, BlockGroup> Groups = new Dictionary<BlockType, BlockGroup> {
        {BlockType.Empty, BlockGroup.Transparent},
        {BlockType.Water, BlockGroup.Transparent},
        {BlockType.Snow, BlockGroup.Solid},
        {BlockType.Sand, BlockGroup.Solid},
        {BlockType.Grass, BlockGroup.Solid},
    };
}