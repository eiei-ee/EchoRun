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
    private int _lastSafeLane = 1;
    private int _obstacleFreeSegments;
   private int _straightSegmentsSinceLastTurn;
    private Transform _player;

    private const float SEGMENT_CHECK_MULT = 1.5f;
    private const float SEGMENT_RECYCLE_MULT = 5f;

    public TrackSegmentData CurrentTurnSegment { get; private set; }
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

        float spawnThresholdSqr = (segmentLength * (poolSize / 2));
        spawnThresholdSqr *= spawnThresholdSqr;

        while (XZSqrDistance(_player.position, _spawnPosition) < spawnThresholdSqr)
        {
            SpawnSegment();
        }

        float recycleThresholdSqr = (segmentLength * SEGMENT_RECYCLE_MULT);
        recycleThresholdSqr *= recycleThresholdSqr;

        while (_activeSegments.Count > 0)
        {
            GameObject seg = _activeSegments[0];
            if (XZSqrDistance(_player.position, seg.transform.position) < recycleThresholdSqr) break;
            RecycleSegment(seg);
        }
    }

    float XZSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    public TrackSegmentData FindTurnAtPosition(Vector3 worldPos)
    {
        // Check cached turn first
        if (CurrentTurnSegment != null && CurrentTurnSegment.gameObject.activeSelf)
        {
            float sqrDist = XZSqrDistance(worldPos, CurrentTurnSegment.transform.position);
            float checkSqr = (segmentLength * SEGMENT_CHECK_MULT);
            checkSqr *= checkSqr;
            if (sqrDist < checkSqr)
                return CurrentTurnSegment;
        }

        CurrentTurnSegment = null;

        float checkDistSqr = (segmentLength * SEGMENT_CHECK_MULT);
        checkDistSqr *= checkDistSqr;

        for (int i = _activeSegments.Count - 1; i >= 0; i--)
        {
            GameObject seg = _activeSegments[i];
            if (!seg.activeSelf) continue;

            TrackSegmentData data = seg.GetComponent<TrackSegmentData>();
            if (data == null || data.segmentType == TrackSegmentType.Straight) continue;

            if (XZSqrDistance(worldPos, seg.transform.position) < checkDistSqr)
            {
                CurrentTurnSegment = data;
                return data;
            }
        }
       return null;
   }

   void InitializePools()
   {
        EnsureProceduralAssets();

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

            // Cache the newly spawned turn for fast lookup
            CurrentTurnSegment = data;
        }
    }

    void RecycleSegment(GameObject segment)
    {
        TrackSegmentData data = segment.GetComponent<TrackSegmentData>();

        // Clear cached turn if this is the one being recycled
        if (data != null && data == CurrentTurnSegment)
            CurrentTurnSegment = null;

        // Destroy dynamic objects on this segment
        Vector3 segPos = segment.transform.position;
        float checkDistSqr = (segmentLength * SEGMENT_CHECK_MULT);
        checkDistSqr *= checkDistSqr;

        for (int i = _dynamicObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = _dynamicObjects[i];
            if (obj == null) { _dynamicObjects.RemoveAt(i); continue; }
            if (XZSqrDistance(obj.transform.position, segPos) < checkDistSqr)
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

       float buffer = 4f;
       float end    = segmentLength - 2f;

       if (segType != TrackSegmentType.Straight)
       {
           SpawnCoinsZigzag(segment, buffer, segmentLength * 0.35f);
           return;
       }

        // Warmup: first few segments have no obstacles
        float warmupSegments = 4f;
        _obstacleFreeSegments++;

        // Progressive difficulty based on segments spawned and speed
        float speedFactor = GameManager.Instance != null
            ? Mathf.InverseLerp(GameManager.Instance.startSpeed,
                                GameManager.Instance.maxSpeed,
                                GameManager.Instance.CurrentSpeed)
            : 0.5f;
        float segmentFactor = Mathf.Clamp01(_obstacleFreeSegments / 15f);
        float diff = Mathf.Max(speedFactor, segmentFactor);

        // Obstacle probability: 0% during warmup, ramps to 70% at high difficulty
        float obstacleChance = 0f;
        if (_obstacleFreeSegments > warmupSegments)
            obstacleChance = Mathf.Lerp(0.25f, 0.70f, diff);

        // Lane continuity: safe lane shifts at most 1 from previous
        int safeLane = _lastSafeLane + Random.Range(-1, 2);
        safeLane = Mathf.Clamp(safeLane, 0, 2);
        _lastSafeLane = safeLane;

        // Determine coin Z first so obstacles can avoid it
        float coinZ = Random.Range(buffer + 2f, end - 4f);

        // Always put a dense coin trail on the safe lane
        SpawnCoinLine(segment, safeLane, coinZ, Random.Range(6, 10));
        // Sometimes add sparse coins on an adjacent lane
        if (Random.value < 0.6f)
        {
            int altLane = (safeLane + (Random.value < 0.5f ? -1 : 1) + 3) % 3;
            SpawnCoinLine(segment, altLane, coinZ + Random.Range(-1f, 1f), Random.Range(2, 5));
        }

        // Spawn obstacles if we're past warmup and dice roll passes
        if (_obstacleFreeSegments > warmupSegments
            && Random.value < obstacleChance
            && obstaclePrefabs.Length >= 3)
        {
            // Place obstacles at a different Z from the coin trail
            float obsZ = coinZ + 3f + Random.Range(0f, 3f);
            if (obsZ > end - 1f) obsZ = coinZ - 3f - Random.Range(0f, 3f);
            obsZ = Mathf.Clamp(obsZ, buffer + 1f, end - 1f);
            SpawnObstacleRow(segment, obsZ, diff, safeLane);
        }
   }

    // ---- coin patterns ----

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

   // ---- obstacle patterns ----

    // Guarantees at least 1 lane is always open
    void SpawnObstacleRow(GameObject segment, float obsZ, float difficulty, int safeLane)
   {
       if (obstaclePrefabs.Length < 3) return;

       // How many lanes to block (1 or 2, never 3)
        int blocked = difficulty > 0.5f ? 2 : 1;
       List<int> lanes = new List<int>();
       for (int l = 0; l < 3; l++)
           if (l != safeLane) lanes.Add(l);

       // Shuffle then take <blocked> lanes
       for (int i = 0; i < lanes.Count; i++)
       {
           int swap = Random.Range(i, lanes.Count);
           int tmp = lanes[i]; lanes[i] = lanes[swap]; lanes[swap] = tmp;
       }

       for (int i = 0; i < blocked && i < lanes.Count; i++)
       {
           int lane = lanes[i];

            // Progressive obstacle types: harder types appear at higher difficulty
           int type;
            if (difficulty < 0.3f)
                type = 0; // early game: only Low obstacles
            else if (difficulty < 0.6f)
                type = Random.value < 0.35f ? 1 : 0; // mid game: Low + High
            else
                type = Random.value < 0.3f ? 2 : (Random.value < 0.5f ? 1 : 0); // late: all types

            // Never put a Barrier when only 1 lane is blocked (can't dodge)
            if (blocked == 1 && type == 2) type = 1;

            SpawnObstacleAt(segment, lane, obsZ + Random.Range(-0.8f, 0.8f), type);
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

    void EnsureProceduralAssets()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) groundLayer = 0;

        if (trackSegmentPrefab == null)
            trackSegmentPrefab = CreateProcStraight(groundLayer);
        if (turnLeftPrefab == null)
            turnLeftPrefab = CreateProcTurn(groundLayer);
        if (turnRightPrefab == null)
            turnRightPrefab = CreateProcTurn(groundLayer);
        if (coinPrefab == null)
            coinPrefab = CreateProcCoin();
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
            obstaclePrefabs = CreateProcObstacles();
    }

    GameObject CreateProcStraight(int layer)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "ProcStraight";
        go.layer = layer;
        go.transform.localScale = new Vector3(0.9f, 1f, 2f);
        Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.center = Vector3.zero; bc.size = new Vector3(9f, 0.3f, 20f);
        go.SetActive(false); go.transform.SetParent(transform);
        return go;
    }

    GameObject CreateProcTurn(int layer)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "ProcTurn";
        go.layer = layer;
        go.transform.localScale = new Vector3(0.9f, 1f, 2f);
        Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.center = Vector3.zero; bc.size = new Vector3(9f, 0.3f, 20f);
        go.SetActive(false); go.transform.SetParent(transform);
        return go;
    }

    GameObject CreateProcCoin()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "ProcCoin";
        go.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true; bc.size = Vector3.one;
        go.AddComponent<Coin>();
        go.SetActive(false); go.transform.SetParent(transform);
        return go;
    }

    GameObject[] CreateProcObstacles()
    {
        ObstacleType[] types = { ObstacleType.Low, ObstacleType.High, ObstacleType.Barrier };
        Vector3[] sizes  = { new Vector3(3f, 1f, 0.6f), new Vector3(0.8f, 3.5f, 0.6f), new Vector3(3.5f, 2.5f, 0.8f) };
        Color[] colors    = { new Color(1f, 0.45f, 0.1f), new Color(0.85f, 0.15f, 0.05f), new Color(0.9f, 0.25f, 0.15f) };
        Shader sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");

        GameObject[] obs = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            obs[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obs[i].name = "ProcObstacle_" + i;
            obs[i].transform.localScale = sizes[i];
            if (sh != null) obs[i].GetComponent<MeshRenderer>().material = new Material(sh) { color = colors[i] };
            Object.DestroyImmediate(obs[i].GetComponent<Collider>());
            BoxCollider bc = obs[i].AddComponent<BoxCollider>();
            bc.isTrigger = true; bc.size = Vector3.one;
            Obstacle o = obs[i].AddComponent<Obstacle>();
            o.type = types[i];
            obs[i].SetActive(false); obs[i].transform.SetParent(transform);
        }
        return obs;
    }
}
