using Unity.Mathematics;
using UnityEngine;

public class Builder : MonoBehaviour {
    public float maxDistance = 5f;
    public new Transform camera;
    public GameObject constructionBlock;
    
    private int _layerMask;

    private void Awake() {
        _layerMask = LayerMask.GetMask("World");
        var startPosition = WorldGenerator.Instance.GetHeightAt(new int2(0, 0));
        transform.position = startPosition + new Vector3(0, 15, 0);
    }

    void Update() {
        RaycastHit hit;
        var t = transform;
        var ray = new Ray(t.position, camera.forward);
        var hasHit = Physics.Raycast(ray, out hit, maxDistance, _layerMask);
        if (!hasHit)
            return;
        CheckConstruct(hit);
        CheckDestroy(hit);
    }

    private void CheckDestroy(RaycastHit hit) {
        var reboundPoint = hit.point + hit.normal * -0.5f;
        var target = Coordinates.RoundWorldPos(reboundPoint);
        
        if (!Input.GetKeyDown(KeyCode.Mouse1)) return;
        WorldGenerator.Instance.DestroyBlock(target);
        // TODO bug: if block is on sector border it should redraw 2 sectors
    }

    private void CheckConstruct(RaycastHit hit) {
        var reboundPoint = hit.point + hit.normal * 0.5f;
        var target = Coordinates.RoundWorldPos(reboundPoint);
        constructionBlock.transform.position = target;

        if (!Input.GetKeyDown(KeyCode.Mouse0)) return;
        var playerPos = Coordinates.RoundWorldPos(transform.position);
        if (playerPos == target)
            GetComponent<CharacterController>().Move(Vector3.up);
        WorldGenerator.Instance.ConstructBlock(target);
    }
}
