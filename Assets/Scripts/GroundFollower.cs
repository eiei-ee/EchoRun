using UnityEngine;

public class GroundFollower : MonoBehaviour
{
    public Transform player;
    public float colliderLength = 200f;
    public float updateThreshold = 30f;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.Find("player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (player == null) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

        float dx = transform.position.x - player.position.x;
        float dz = transform.position.z - player.position.z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);

        if (dist > updateThreshold)
        {
            transform.position = new Vector3(player.position.x, -0.1f, player.position.z);
        }
    }
}
