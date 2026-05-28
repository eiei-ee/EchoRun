using System.Collections.Generic;
using UnityEngine;

public class TrackManager : MonoBehaviour
{
    public static TrackManager Instance { get; private set; }

    [Header("Track")]
    public GameObject trackSegmentPrefab;
    public float segmentLength = 20f;
    public int poolSize = 8;

    [Header("Lanes")]
    public float laneDistance = 3f;

    [Header("Obstacles & Coins")]
    public GameObject[] obstaclePrefabs;
    public GameObject coinPrefab;
    [Range(0, 1)] public float obstacleChance = 0.4f;
    [Range(0, 1)] public float coinChance = 0.6f;

    private Queue<GameObject> _segmentPool = new Queue<GameObject>();
    private List<GameObject> _activeSegments = new List<GameObject>();
    private List<GameObject> _dynamicObjects = new List<GameObject>();
    private float _spawnZ;
    private Transform _player;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _player = GameObject.Find("player")?.transform;
        _spawnZ = _player != null ? _player.position.z : 0f;
        InitializePool();
    }

    void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;
        if (_player == null) return;
        if (trackSegmentPrefab == null) return;

        float playerZ = _player.position.z;

        while (_spawnZ < playerZ + segmentLength * (poolSize / 2))
            SpawnSegment();

        while (_activeSegments.Count > 0 &&
               _activeSegments[0].transform.position.z + segmentLength < playerZ - segmentLength * 2)
            RecycleSegment(_activeSegments[0]);
    }

    void InitializePool()
    {
        if (trackSegmentPrefab == null) return;
        for (int i = 0; i < poolSize; i++)
        {
            GameObject segment = Instantiate(trackSegmentPrefab, Vector3.zero, Quaternion.identity, transform);
            segment.SetActive(false);
            _segmentPool.Enqueue(segment);
        }
    }

    void SpawnSegment()
    {
        if (trackSegmentPrefab == null) return;

        GameObject segment = _segmentPool.Count > 0
            ? _segmentPool.Dequeue()
            : Instantiate(trackSegmentPrefab, Vector3.zero, Quaternion.identity, transform);

        segment.transform.position = new Vector3(0, 0, _spawnZ);
        segment.SetActive(true);
        _activeSegments.Add(segment);
        _spawnZ += segmentLength;

        SpawnObstaclesAndCoins(segment);
    }

    void RecycleSegment(GameObject segment)
    {
        float segZ = segment.transform.position.z;
        for (int i = _dynamicObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = _dynamicObjects[i];
            if (obj == null) { _dynamicObjects.RemoveAt(i); continue; }
            if (obj.transform.position.z >= segZ && obj.transform.position.z < segZ + segmentLength)
            {
                _dynamicObjects.RemoveAt(i);
                Destroy(obj);
            }
        }

        segment.SetActive(false);
        _activeSegments.RemoveAt(0);
        _segmentPool.Enqueue(segment);
    }

    void SpawnObstaclesAndCoins(GameObject segment)
    {
        if (obstaclePrefabs.Length == 0 && coinPrefab == null) return;

        // Decide how many lanes to block (max 2, never all 3)
        int blockedCount = 0;
        float r = Random.value;
        if (r < 0.15f) blockedCount = 2;
        else if (r < 0.55f) blockedCount = 1;

        // Pick which lane(s) to block
        List<int> blockedLanes = new List<int>();
        List<int> available = new List<int> { 0, 1, 2 };
        for (int i = 0; i < blockedCount; i++)
        {
            int idx = Random.Range(0, available.Count);
            blockedLanes.Add(available[idx]);
            available.RemoveAt(idx);
        }

        for (int lane = 0; lane < 3; lane++)
        {
            float x = (lane - 1) * laneDistance;
            float z = segment.transform.position.z + Random.Range(2f, segmentLength - 2f);

            if (blockedLanes.Contains(lane) && obstaclePrefabs.Length > 0)
            {
                Vector3 obsPos = new Vector3(x, 1f, z);
                GameObject obs = Instantiate(obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)], obsPos, Quaternion.identity);
                _dynamicObjects.Add(obs);
            }
            else if (Random.value < coinChance && coinPrefab != null)
            {
                int coinCount = Random.Range(3, 8);
                for (int c = 0; c < coinCount; c++)
                {
                    Vector3 coinPos = new Vector3(x, 1f, z + c * 1.5f);
                    GameObject coin = Instantiate(coinPrefab, coinPos, Quaternion.identity);
                    _dynamicObjects.Add(coin);
                }
            }
        }
    }
}
