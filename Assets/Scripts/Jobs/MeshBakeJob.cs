using Unity.Burst;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct MeshBakeJob : IJob {
    public int meshId;

    public void Execute() {
        Physics.BakeMesh(meshId, false);
    }
}