using System;
using UnityEngine;
using UnityEngine.Events;

public class PositionTracker : MonoBehaviour {
    private Vector2Int lastPos;
    public UnityEvent<Vector2Int, Vector2Int> OnPlayerMoved;
    
    private void FixedUpdate() {
        var sectorSize = WorldGenerator.Instance.sectorSize;
        var position = transform.position;
        var currentPos = new Vector2Int((int)position.x, (int)position.z) / sectorSize;
        if (currentPos != lastPos) {
            OnPlayerMoved.Invoke(lastPos, currentPos);
            lastPos = currentPos;
        }
    }
}