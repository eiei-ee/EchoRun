using System.Collections.Generic;
using UnityEngine;

public class TrackManager : MonoBehaviour
{
    public static TrackManager Instance { get; private set; }

    [Header("Track")]
    public GameObject trackSegmentPrefab;
    public float segmentLength = 20f;
    public int poolSize = 10;

    [Header("Turns")]
    public GameObject turnLeftPrefab;
    public GameObject turnRightPrefab;
    [Range(0, 1)] public float turnChance = 0.15f;
    public int minStraightBeforeTurn = 4;

    [Header("Lanes")]
    public float laneDistance = 3f;

    [Header("Obstacles & Coins")]
    public GameObject[] obstaclePrefabs;
    public GameObject coinPrefab;
    [Range(0, 1)] public float obstacleChance = 0.4f;
    [Range(0, 1)] public float coinChance = 0.6f;

    private Queue<GameObject> _straightPool = new Queue<GameObject>();
    private Queue<GameObject> _turnLeftPool = new Queue<GameObject>();
    private Queue<GameObject> _turnRightPool = new Queue<GameObject>();
    private List<GameObject> _activeSegments = new List<GameObject>();
    private List<GameObject> _dynamicObjects = new List<GameObject>();

    private Vector3 _spawnPosition;
    private float _spawnAngle;
    private int _straightSegmentsSinceLastTurn;
    private Transform _player;

    public Vector3 ForwardDirection =>
        Quaternion.Euler(0, _spawnAngle, 0) * Vector3.forward;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _player = GameObject.Find("player")?.transform;
        _spawnPosition = _player != null ? new Vector3(_player.position.x, 0, _player.position.z) : Vector3.zero;
        _spawnAngle = 0f;
        _straightSegmentsSinceLastTurn = 0;
        InitializePools();
    }

    void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;
        if (_player == null) return;
        if (trackSegmentPrefab == null) return;

        float distToSpawn = XZDistance(_player.position, _spawnPosition);

        while (distToSpawn < segmentLength * (poolSize / 2))
        {
            SpawnSegment();
            distToSpawn = XZDistance(_player.position, _spawnPosition);
        }

        while (_activeSegments.Count > 0)
        {
            GameObject seg = _activeSegments[0];
            float dist = XZDistance(_player.position, seg.transform.position);
            if (dist < segmentLength * 5) break;
            RecycleSegment(seg);
        }
    }

    float XZDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public TrackSegmentData FindTurnAtPosition(Vector3 worldPos)
    {
        for (int i = _activeSegments.Count - 1; i >= 0; i--)
        {
            GameObject seg = _activeSegments[i];
            if (!seg.activeSelf) continue;

            TrackSegmentData data = seg.GetComponent<TrackSegmentData>();
            if (data == null || data.segmentType == TrackSegmentType.Straight) continue;

            // Check if player is within this turn segment's bounds
            float dist = XZDistance(worldPos, seg.transform.position);
            if (dist < segmentLength * 1.5f)
                return data;
        }
        return null;
    }

    void InitializePools()
    {
        if (trackSegmentPrefab != null)
        {
            for (int i = 0; i < 6; i++)
            {
                GameObject seg = Instantiate(trackSegmentPrefab, Vector3.zero, Quaternion.identity, transform);
                seg.SetActive(false);
                _straightPool.Enqueue(seg);
            }
        }
        if (turnLeftPrefab != null)
        {
            for (int i = 0; i < 2; i++)
            {
                GameObject seg = Instantiate(turnLeftPrefab, Vector3.zero, Quaternion.identity, transform);
                seg.SetActive(false);
                _turnLeftPool.Enqueue(seg);
            }
        }
        if (turnRightPrefab != null)
        {
            for (int i = 0; i < 2; i++)
            {
                GameObject seg = Instantiate(turnRightPrefab, Vector3.zero, Quaternion.identity, transform);
                seg.SetActive(false);
                _turnRightPool.Enqueue(seg);
            }
        }
    }

    void SpawnSegment()
    {
        bool shouldTurn = _straightSegmentsSinceLastTurn >= minStraightBeforeTurn
                          && Random.value < turnChance
                          && turnLeftPrefab != null && turnRightPrefab != null;

        GameObject prefab;
        TrackSegmentType segType;
        float angleDelta = 0f;
        Queue<GameObject> pool;

        if (shouldTurn)
        {
            bool turnRight = Random.value < 0.5f;
            prefab = turnRight ? turnRightPrefab : turnLeftPrefab;
            segType = turnRight ? TrackSegmentType.TurnRight : TrackSegmentType.TurnLeft;
            angleDelta = turnRight ? 90f : -90f;
            pool = turnRight ? _turnRightPool : _turnLeftPool;
        }
        else
        {
            prefab = trackSegmentPrefab;
            segType = TrackSegmentType.Straight;
            pool = _straightPool;
        }

        GameObject segment = pool.Count > 0
            ? pool.Dequeue()
            : Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);

        segment.transform.position = _spawnPosition;
        segment.transform.rotation = Quaternion.Euler(0, _spawnAngle, 0);
        segment.SetActive(true);

        TrackSegmentData data = segment.GetComponent<TrackSegmentData>();
        if (data == null) data = segment.AddComponent<TrackSegmentData>();
        data.segmentType = segType;

        _activeSegments.Add(segment);

        if (segType == TrackSegmentType.Straight)
        {
            data.entryDirection = ForwardDirection;
            SpawnObstaclesAndCoins(segment, segType);
            _spawnPosition += ForwardDirection * segmentLength;
            _straightSegmentsSinceLastTurn++;
        }
        else
        {
            Vector3 entryDir = ForwardDirection;
            Vector3 cornerPos = _spawnPosition;

            // Shift turn back half a segment so entry strip bridges the gap
            // from previous straight's visual end to the corner
            Vector3 turnPlacePos = _spawnPosition - entryDir * (segmentLength * 0.5f);
            segment.transform.position = turnPlacePos;
            segment.transform.rotation = Quaternion.Euler(0, _spawnAngle, 0);

            SpawnObstaclesAndCoins(segment, segType);

            // Advance spawn: full segment in exit direction from corner
            // (entry half was consumed by the shifted-back placement)
            _spawnAngle += angleDelta;
            _spawnPosition += ForwardDirection * segmentLength;

            data.entryDirection = entryDir;
            data.exitDirection = ForwardDirection;
            data.turnPointWorld = cornerPos;
            _straightSegmentsSinceLastTurn = 0;
        }
    }

    void RecycleSegment(GameObject segment)
    {
        TrackSegmentData data = segment.GetComponent<TrackSegmentData>();

        // Destroy dynamic objects on this segment
        Vector3 segPos = segment.transform.position;
        for (int i = _dynamicObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = _dynamicObjects[i];
            if (obj == null) { _dynamicObjects.RemoveAt(i); continue; }
            if (XZDistance(obj.transform.position, segPos) < segmentLength * 1.5f)
            {
                _dynamicObjects.RemoveAt(i);
                Destroy(obj);
            }
        }

        segment.SetActive(false);
        _activeSegments.RemoveAt(0);

        if (data != null)
        {
            switch (data.segmentType)
            {
                case TrackSegmentType.TurnLeft:
                    _turnLeftPool.Enqueue(segment);
                    break;
                case TrackSegmentType.TurnRight:
                    _turnRightPool.Enqueue(segment);
                    break;
                default:
                    _straightPool.Enqueue(segment);
                    break;
            }
        }
        else
        {
            _straightPool.Enqueue(segment);
        }
    }

    void SpawnObstaclesAndCoins(GameObject segment, TrackSegmentType segType)
    {
        if (obstaclePrefabs.Length == 0 && coinPrefab == null) return;

        float s = 2f;
        float e = segmentLength - 2f;

        if (segType != TrackSegmentType.Straight)
        {
            SpawnCoinsZigzag(segment, 2f, segmentLength * 0.4f);
            return;
        }

        float roll = Random.value;

        if (roll < 0.15f && coinPrefab != null)
        {
            // Full coin row across all 3 lanes (reward)
            SpawnCoinRow(segment, s, e, -2);
        }
        else if (roll < 0.35f && coinPrefab != null)
        {
            // Zigzag coins guiding lane switch
            SpawnCoinsZigzag(segment, s, e);
        }
        else if (roll < 0.45f && coinPrefab != null)
        {
            // Jump arc coins (floating high)
            SpawnCoinArc(segment, s, e);
        }
        else if (roll < 0.75f && obstaclePrefabs.Length >= 3)
        {
            SpawnObstacleSet(segment, s, e);
        }
        else if (coinPrefab != null)
        {
            int skip = Random.Range(0, 3);
            for (int lane = 0; lane < 3; lane++)
            {
                if (lane == skip && Random.value < 0.5f) continue;
                SpawnCoinLine(segment, lane, Random.Range(s + 2f, e - 3f), Random.Range(4, 8));
            }
        }
    }

    // ── coin patterns ──────────────────────────────────

    void SpawnCoinLine(GameObject segment, int lane, float startZ, int count)
    {
        if (coinPrefab == null) return;
        float x = (lane - 1) * laneDistance;
        for (int c = 0; c < count; c++)
        {
            Vector3 lp = new Vector3(x, 1f, startZ + c * 1.5f);
            if (lp.z > segmentLength - 1f) break;
            Vector3 wp = segment.transform.TransformPoint(lp);
            _dynamicObjects.Add(Instantiate(coinPrefab, wp, Quaternion.identity));
        }
    }

    void SpawnCoinRow(GameObject segment, float zStart, float zEnd, int skipLane)
    {
        if (coinPrefab == null) return;
        float rowZ = Random.Range(zStart + 1f, zEnd - 3f);
        int count = Random.Range(5, 9);
        for (int lane = 0; lane < 3; lane++)
        {
            if (lane == skipLane) continue;
            SpawnCoinLine(segment, lane, rowZ, count);
        }
    }

    void SpawnCoinsZigzag(GameObject segment, float zStart, float zEnd)
    {
        // Subway Surfers-style: coins weave between lanes
        if (coinPrefab == null) return;
        int steps = Random.Range(5, 9);
        float zStep = (zEnd - zStart) / steps;

        int fromLane = Random.Range(0, 3);
        for (int i = 0; i < steps; i++)
        {
            float z = zStart + zStep * i;
            int toLane = (fromLane + (Random.value < 0.5f ? 1 : -1) + 3) % 3;
            toLane = Mathf.Clamp(toLane, 0, 2);

            float x = (fromLane - 1) * laneDistance;
            float x2 = (toLane - 1) * laneDistance;

            int coins = Random.Range(2, 5);
            for (int c = 0; c < coins; c++)
            {
                float t = (float)c / (coins - 1);
                float cx = Mathf.Lerp(x, x2, t);
                Vector3 lp = new Vector3(cx, 1f, z + c * 1.2f);
                if (lp.z > zEnd) break;
                Vector3 wp = segment.transform.TransformPoint(lp);
                _dynamicObjects.Add(Instantiate(coinPrefab, wp, Quaternion.identity));
            }
            fromLane = toLane;
        }
    }

    void SpawnCoinArc(GameObject segment, float zStart, float zEnd)
    {
        // Jump arc: coins floating in an arc at Y=1.5-3.5
        if (coinPrefab == null) return;
        float centerZ = (zStart + zEnd) * 0.5f;
        int lane = Random.Range(0, 3);
        float x = (lane - 1) * laneDistance;
        int count = Random.Range(6, 12);
        float arcLen = Random.Range(4f, 8f);

        for (int c = 0; c < count; c++)
        {
            float t = (float)c / (count - 1) - 0.5f; // -0.5 to 0.5
            float z = centerZ + t * arcLen;
            if (z < zStart || z > zEnd) continue;
            float y = 1f + Mathf.Sin((t + 0.5f) * Mathf.PI) * 2.5f;
            Vector3 lp = new Vector3(x, y, z);
            Vector3 wp = segment.transform.TransformPoint(lp);
            _dynamicObjects.Add(Instantiate(coinPrefab, wp, Quaternion.identity));
        }
    }

    // ── obstacle patterns ──────────────────────────────

    void SpawnObstacleSet(GameObject segment, float zStart, float zEnd)
    {
        if (obstaclePrefabs.Length < 3) return;
        // obstaclePrefabs indices: 0=Low, 1=High, 2=Barrier

        // 1-3 obstacle groups spread along the segment
        int groups = Random.Range(1, 3);
        float step = (zEnd - zStart) / (groups + 1);

        for (int g = 0; g < groups; g++)
        {
            float z = zStart + step * (g + 1) + Random.Range(-2f, 2f);
            if (z < zStart + 1f || z > zEnd - 1f) continue;

            float pattern = Random.value;
            List<int> lanes = new List<int> { 0, 1, 2 };

            if (pattern < 0.35f)
            {
                // Low barriers: slide under (1-2 lanes)
                int count = Random.Range(1, 3);
                for (int i = 0; i < count; i++)
                {
                    int idx = Random.Range(0, lanes.Count);
                    SpawnObstacleAt(segment, lanes[idx], z, 0); // Low
                    lanes.RemoveAt(idx);
                }
            }
            else if (pattern < 0.65f)
            {
                // High barrier: jump over (1 lane)
                int lane = lanes[Random.Range(0, lanes.Count)];
                SpawnObstacleAt(segment, lane, z, 1); // High
            }
            else if (pattern < 0.85f)
            {
                // Barriers: force lane switch (1-2 lanes blocked)
                int count = Random.Range(1, 3);
                for (int i = 0; i < count; i++)
                {
                    int idx = Random.Range(0, lanes.Count);
                    SpawnObstacleAt(segment, lanes[idx], z, 2); // Barrier
                    lanes.RemoveAt(idx);
                }
            }
            else
            {
                // Combo: low + high on different lanes
                int lowLane = lanes[Random.Range(0, lanes.Count)];
                lanes.Remove(lowLane);
                SpawnObstacleAt(segment, lowLane, z, 0);
                if (lanes.Count > 0)
                {
                    int highLane = lanes[Random.Range(0, lanes.Count)];
                    SpawnObstacleAt(segment, highLane, z + 1.5f, 1);
                }
            }
        }
    }

    void SpawnObstacleAt(GameObject segment, int lane, float z, int prefabIndex)
    {
        float x = (lane - 1) * laneDistance;
        Vector3 lp = new Vector3(x, 1f, z);
        Vector3 wp = segment.transform.TransformPoint(lp);
        Quaternion rot = segment.transform.rotation;
        GameObject obs = Instantiate(obstaclePrefabs[prefabIndex], wp, rot);
        _dynamicObjects.Add(obs);
    }
}
