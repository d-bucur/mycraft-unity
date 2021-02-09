using System.Collections.Generic;

public enum BlockType {
    Water = 0,
    Sand = 1,
    Snow = 2,
    Grass = 3,
    Empty = 4
}

// TODO add enum for group
public struct Block {
    public static readonly Dictionary<BlockType, int> Groups = new Dictionary<BlockType, int> {
        {BlockType.Empty, 1},
        {BlockType.Water, 1},
        {BlockType.Snow, 0},
        {BlockType.Sand, 0},
        {BlockType.Grass, 0},
    };
}