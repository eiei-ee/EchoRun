using UnityEngine;

// An authored distant silhouette ring. It follows translation only, so turns
// retain a consistent horizon; it never participates in track or collision.
public sealed class CityV7DistantBackdrop : MonoBehaviour
{
    private Transform _camera;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (FindObjectOfType<CityV7DistantBackdrop>() != null) return;
        var prefab = Resources.Load<GameObject>("CityV7/ExperienceSkyline");
        if (prefab != null) Instantiate(prefab).AddComponent<CityV7DistantBackdrop>();
    }
    private void LateUpdate()
    {
        if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
        if (_camera == null) return;
        Vector3 p = _camera.position;
        transform.position = new Vector3(p.x, 0f, p.z);
    }
}
