using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

public class JobCompatibility  {
    [Test]
    public void SamplesAreCompatible() {
        NoiseMap map = new NoiseMap {
            amplitude = 30, frequency = 0.05f, offset = float2.zero, power = 1
        };
        for (int x = -100; x < 100; x++) {
            for (int y = -100; y < 100; y++) {
                var p2 = new int2(x, y);
                var p1 = new Vector2Int(x, y);
                Assert.AreEqual(map.Sample(p1), map.SampleJob(p2));
            }
        }
    }

    // [Test]
    // public void SectorIdsAreCompatible() {
    //     var sectorSize = new int2(10, 20);
    //     Sector.SetSizes(10, 20);
    //     var sector = GameObject.CreatePrimitive(PrimitiveType.Cube).AddComponent<Sector>();
    //     foreach (var itPos in sector) {
    //         var idx = Sector.GetId(itPos);
    //         var jobPos = Sector.IdToPos(idx, sectorSize);
    //         Assert.AreEqual(itPos.x, jobPos.x);
    //         Assert.AreEqual(itPos.y, jobPos.y);
    //         Assert.AreEqual(itPos.z, jobPos.z);
    //     }
    // }

    [Test]
    public void GridGenerationIsCompatible() {
        Assert.Fail();
    }
}
