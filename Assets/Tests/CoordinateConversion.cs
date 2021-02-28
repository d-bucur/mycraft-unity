using NUnit.Framework;
using Unity.Mathematics;

// ReSharper disable HeapView.BoxingAllocation

public class CoordinateConversion {
    [Test]
    public void InternalPositionsWrapToNeighboringSectors() {
        var sectorSize = new int2(3, 5);
        Assert.AreEqual(
            Coordinates.InternalToPlanePos(
                new int2(0, 0),
                new int3(0, 0, 0),
                sectorSize
            ),
            Coordinates.InternalToPlanePos(
                new int2(-1, 0),
                new int3(sectorSize.x, 0, 0),
                sectorSize
            )
        );
        Assert.AreEqual(
            Coordinates.InternalToPlanePos(
                new int2(0, 0),
                new int3(-1, 0, 0),
                sectorSize
            ),
            Coordinates.InternalToPlanePos(
                new int2(-1, 0),
                new int3(sectorSize.x - 1, 0, 0),
                sectorSize
            )
        );
        Assert.AreEqual(
            Coordinates.InternalToPlanePos(
                new int2(0, 0),
                new int3(0, 0, sectorSize.x),
                sectorSize
            ),
            Coordinates.InternalToPlanePos(
                new int2(0, 1),
                new int3(0, 0, 0),
                sectorSize
            )
        );
        Assert.AreEqual(
            Coordinates.InternalToPlanePos(
                new int2(0, 0),
                new int3(0, 0, sectorSize.x-1),
                sectorSize
            ),
            Coordinates.InternalToPlanePos(
                new int2(0, 1),
                new int3(0, 0, -1),
                sectorSize
            )
        );
    }
}
