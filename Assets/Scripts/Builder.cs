using System;
using UnityEngine;

public class Builder : MonoBehaviour {
    public float maxDistance = 5f;
    public new Transform camera;
    
    private int _layerMask;

    private void Awake() {
        _layerMask = LayerMask.GetMask("World");
    }

    void Update() {
        RaycastHit hit;
        var hasHit = Physics.Raycast(new Ray(transform.position, camera.forward), out hit, maxDistance, _layerMask);
        if (!hasHit)
            return;
        Debug.DrawLine(transform.position, hit.point, Color.red, 1f);
    }
}
