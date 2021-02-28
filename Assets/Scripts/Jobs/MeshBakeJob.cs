using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct MeshBakeJob : IJobParallelFor {
    [DeallocateOnJobCompletion] public NativeArray<int> meshIds;
    
    public void Execute(int index) {
        Physics.BakeMesh(meshIds[index], false);
    }
}