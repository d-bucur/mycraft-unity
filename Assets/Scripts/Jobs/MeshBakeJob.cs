using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Jobs {
public struct MeshBakeJob : IJobParallelFor {
    [DeallocateOnJobCompletion] public NativeArray<int> meshIds;
    
    public void Execute(int index) {
        Physics.BakeMesh(meshIds[index], false);
    }
}
}