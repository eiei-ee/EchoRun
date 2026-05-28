using UnityEngine;

public class GroundFollower : MonoBehaviour
{
    public Transform player;
    public float colliderLength = 200f;
    public float updateThreshold = 100f;

    private float _nextSnapZ;
    private BoxCollider _boxCollider;

    void Start()
    {
        _boxCollider = GetComponent<BoxCollider>();
        if (player == null)
        {
            GameObject p = GameObject.Find("player");
            if (p != null) player = p.transform;
        }
        if (player != null)
            _nextSnapZ = player.position.z + updateThreshold;
    }

    void Update()
    {
        if (player == null) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

        float playerZ = player.position.z;

        // When player approaches the end, snap the ground forward
        if (playerZ > _nextSnapZ)
        {
            float snapZ = playerZ + colliderLength * 0.3f;
            transform.position = new Vector3(0, -0.1f, snapZ);
            _nextSnapZ = snapZ - colliderLength * 0.3f + updateThreshold;
        }
    }
}
