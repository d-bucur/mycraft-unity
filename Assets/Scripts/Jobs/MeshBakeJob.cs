using Unity.Burst;
using Unity.Jobs;
using UnityEngine;

/** Runs a bake mesh job that is required before assigning a mesh to a collider */
[BurstCompile]
public struct MeshBakeJob : IJob {
    public int meshId;

    public void Execute() {
        Physics.BakeMesh(meshId, false);
    }
}