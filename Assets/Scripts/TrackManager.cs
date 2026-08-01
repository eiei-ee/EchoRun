using System.Collections.Generic;
using UnityEngine;

public class TrackManager : MonoBehaviour
{
    public static TrackManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<TrackManager>() != null) return;
        new GameObject("TrackManager_Runtime").AddComponent<TrackManager>();
    }

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
    [Min(1)] public int maxConsecutiveObstacleFreeStraights = 3;

    [Header("AI Track Director")]
    public bool useAITrackDirector = true;

    private Queue<GameObject> _straightPool = new Queue<GameObject>();
    private Queue<GameObject> _turnLeftPool = new Queue<GameObject>();
    private Queue<GameObject> _turnRightPool = new Queue<GameObject>();
    private List<GameObject> _activeSegments = new List<GameObject>();
    private readonly Dictionary<GameObject, Queue<GameObject>> _dynamicPools =
        new Dictionary<GameObject, Queue<GameObject>>();
    private readonly List<DynamicEntry> _dynamicObjects = new List<DynamicEntry>();

    private class DynamicEntry
    {
        public GameObject instance;
        public GameObject prefab;
        public GameObject ownerSegment;
    }

    private Vector3 _spawnPosition;
   private float _spawnAngle;
    private int _lastSafeLane = 1;
    private int _obstacleFreeSegments;
    private int _straightSegmentsSpawned;
    private readonly int[] _laneObstacleDrought = new int[3];
    private int _straightSegmentsSinceLastTurn;
    private float _plannedDistance;
    private Transform _player;
    private AITrackDirector _aiDirector;

    private const float SEGMENT_CHECK_MULT = 1.5f;
    private const float SEGMENT_RECYCLE_MULT = 5f;

    public TrackSegmentData CurrentTurnSegment { get; private set; }
    public int ActiveSegmentCount => _activeSegments.Count;
    public Vector3 ForwardDirection =>
        Quaternion.Euler(0, _spawnAngle, 0) * Vector3.forward;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        TrackBalance balance = GameBalanceConfig.Current.track;
        obstacleChance = balance.obstacleChance;
        coinChance = balance.coinChance;
        turnChance = balance.turnChance;

        if (useAITrackDirector)
        {
            _aiDirector = GetComponent<AITrackDirector>();
            if (_aiDirector == null) _aiDirector = gameObject.AddComponent<AITrackDirector>();
        }

        if (GetComponent<AIShadowRunner>() == null)
            gameObject.AddComponent<AIShadowRunner>();
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
        if (GameManager.Instance == null
            || GameManager.Instance.State != GameState.Playing) return;
        if (_player == null) return;
        if (trackSegmentPrefab == null) return;

        float playerRouteDistance = GameManager.Instance.Distance;
        int spawnBudget = Mathf.Max(1, poolSize);
        while (spawnBudget-- > 0 && TrackSpawnRules.NeedsSegment(
                   _plannedDistance, playerRouteDistance, segmentLength, poolSize))
        {
            SpawnSegment();
        }

        while (_activeSegments.Count > 0)
        {
            GameObject seg = _activeSegments[0];
            TrackSegmentData data = seg.GetComponent<TrackSegmentData>();
            if (data == null || !TrackSpawnRules.CanRecycleSegment(
                    data.routeDistance, playerRouteDistance, segmentLength,
                    SEGMENT_RECYCLE_MULT)) break;
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

    public bool IsInsideTurnTransition(Vector3 worldPosition,
        float transitionDistance = 3f)
    {
        float distance = Mathf.Max(0.1f, transitionDistance);
        float maxRadius = distance + laneDistance;
        float maxRadiusSqr = maxRadius * maxRadius;

        for (int i = _activeSegments.Count - 1; i >= 0; i--)
        {
            GameObject segment = _activeSegments[i];
            if (segment == null || !segment.activeInHierarchy) continue;

            TrackSegmentData data = segment.GetComponent<TrackSegmentData>();
            if (data == null || data.segmentType == TrackSegmentType.Straight)
                continue;

            Vector3 fromTurn = worldPosition - data.turnPointWorld;
            fromTurn.y = 0f;
            if (fromTurn.sqrMagnitude > maxRadiusSqr) continue;

            Vector3 entryDirection = data.entryDirection.normalized;
            Vector3 exitDirection = data.exitDirection.normalized;
            float entryProgress = Vector3.Dot(fromTurn, entryDirection);
            float exitProgress = Vector3.Dot(fromTurn, exitDirection);
            if (entryProgress >= -distance && exitProgress <= distance)
                return true;
        }

        return false;
    }

    public bool TryGetUpcomingObstacle(Vector3 playerPosition, Vector3 forward,
        int currentLane, out int obstacleLane, out float obstacleDistance,
        out ObstacleType obstacleType, out int obstacleId)
    {
        return TryGetUpcomingObstacleInternal(playerPosition, forward, currentLane,
            false, null, out obstacleLane, out obstacleDistance,
            out obstacleType, out obstacleId);
    }

    public bool TryGetUpcomingObstacleInLane(Vector3 position, Vector3 forward,
        int lane, ISet<int> ignoredObstacleIds, out float obstacleDistance,
        out ObstacleType obstacleType, out int obstacleId)
    {
        return TryGetUpcomingObstacleInternal(position, forward, lane,
            true, ignoredObstacleIds, out _, out obstacleDistance,
            out obstacleType, out obstacleId);
    }

    private bool TryGetUpcomingObstacleInternal(Vector3 playerPosition, Vector3 forward,
        int currentLane, bool currentLaneOnly, ISet<int> ignoredObstacleIds,
        out int obstacleLane, out float obstacleDistance,
        out ObstacleType obstacleType, out int obstacleId)
    {
        obstacleLane = currentLane;
        obstacleDistance = float.MaxValue;
        obstacleType = ObstacleType.Low;
        obstacleId = 0;

        Vector3 normalizedForward = forward.sqrMagnitude > 0.001f
            ? forward.normalized
            : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, normalizedForward).normalized;
        bool found = false;

        for (int i = 0; i < _dynamicObjects.Count; i++)
        {
            DynamicEntry entry = _dynamicObjects[i];
            if (entry.instance == null || !entry.instance.activeInHierarchy) continue;

            Obstacle obstacle = entry.instance.GetComponent<Obstacle>();
            if (obstacle == null) continue;

            Vector3 offset = entry.instance.transform.position - playerPosition;
            float forwardDistance = Vector3.Dot(offset, normalizedForward);
            if (forwardDistance <= 0.5f || forwardDistance > 24f
                || forwardDistance >= obstacleDistance)
                continue;

            float laneDelta = Vector3.Dot(offset, right) / Mathf.Max(0.1f, laneDistance);
            int candidateLane = Mathf.Clamp(
                currentLane + Mathf.RoundToInt(laneDelta), 0, 2);
            if (currentLaneOnly && candidateLane != currentLane) continue;

            Vector3 obstaclePosition = entry.instance.transform.position;
            int candidateId = entry.instance.GetInstanceID()
                              ^ Mathf.RoundToInt(obstaclePosition.x * 17f)
                              ^ Mathf.RoundToInt(obstaclePosition.z * 31f);
            if (ignoredObstacleIds != null && ignoredObstacleIds.Contains(candidateId))
                continue;

            obstacleLane = candidateLane;
            obstacleDistance = forwardDistance;
            obstacleType = obstacle.type;
            obstacleId = candidateId;
            found = true;
        }

        return found;
    }

    public void GetTrackPoseAhead(Vector3 playerPosition, Vector3 playerForward,
        int playerLane, float targetLane, float distanceAhead,
        out Vector3 trackPosition, out Vector3 trackForward)
    {
        Vector3 forward = playerForward.sqrMagnitude > 0.001f
            ? playerForward.normalized
            : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 trackCenter = playerPosition
                              - right * ((playerLane - 1) * laneDistance);
        trackForward = forward;

        if (distanceAhead > 0f)
        {
            TrackSegmentData nextTurn = null;
            float nearestTurnDistance = float.MaxValue;
            for (int i = 0; i < _activeSegments.Count; i++)
            {
                GameObject segment = _activeSegments[i];
                if (segment == null || !segment.activeInHierarchy) continue;

                TrackSegmentData data = segment.GetComponent<TrackSegmentData>();
                if (data == null || data.segmentType == TrackSegmentType.Straight
                    || Vector3.Dot(data.entryDirection, forward) < 0.9f)
                    continue;

                Vector3 toTurn = data.turnPointWorld - trackCenter;
                float forwardDistance = Vector3.Dot(toTurn, forward);
                float lateralDistance = Mathf.Abs(Vector3.Dot(toTurn, right));
                const float cornerBlendDistance = 2.5f;
                if (forwardDistance <= 0.1f
                    || forwardDistance > distanceAhead + cornerBlendDistance
                    || lateralDistance > laneDistance * 1.5f
                    || forwardDistance >= nearestTurnDistance)
                    continue;

                nextTurn = data;
                nearestTurnDistance = forwardDistance;
            }

            if (nextTurn != null)
            {
                const float cornerBlendDistance = 2.5f;
                float distanceFromTurn = distanceAhead - nearestTurnDistance;
                if (distanceFromTurn <= -cornerBlendDistance)
                {
                    trackCenter += forward * distanceAhead;
                }
                else if (distanceFromTurn >= cornerBlendDistance)
                {
                    trackCenter = nextTurn.turnPointWorld
                                  + nextTurn.exitDirection * distanceFromTurn;
                    trackForward = nextTurn.exitDirection;
                }
                else
                {
                    float t = Mathf.InverseLerp(-cornerBlendDistance,
                        cornerBlendDistance, distanceFromTurn);
                    Vector3 entryPoint = nextTurn.turnPointWorld
                                         - forward * cornerBlendDistance;
                    Vector3 exitPoint = nextTurn.turnPointWorld
                                        + nextTurn.exitDirection * cornerBlendDistance;
                    float oneMinusT = 1f - t;
                    trackCenter = oneMinusT * oneMinusT * entryPoint
                                  + 2f * oneMinusT * t * nextTurn.turnPointWorld
                                  + t * t * exitPoint;
                    trackForward = Vector3.Slerp(
                        forward, nextTurn.exitDirection, t).normalized;
                }
            }
            else
            {
                trackCenter += forward * distanceAhead;
            }
        }
        else
        {
            trackCenter += forward * distanceAhead;
        }

        Vector3 targetRight = Vector3.Cross(Vector3.up, trackForward).normalized;
        trackPosition = trackCenter
                        + targetRight * ((Mathf.Clamp(targetLane, 0f, 2f) - 1f)
                                         * laneDistance);
    }

   void InitializePools()
   {
       EnsureProceduralAssets();

       if (trackSegmentPrefab != null)
        {
            int straightPoolSize = Mathf.Max(1, poolSize - 4);
            for (int i = 0; i < straightPoolSize; i++)
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
        float baseDifficulty = CalculateBaseDifficulty();
        bool canTurn = _straightSegmentsSinceLastTurn >= minStraightBeforeTurn
                       && turnLeftPrefab != null && turnRightPrefab != null;
        AITrackPlan plan = _aiDirector != null && _aiDirector.useAI
            ? _aiDirector.CreatePlan(baseDifficulty, obstacleChance, coinChance,
                turnChance, _lastSafeLane, canTurn, _plannedDistance + segmentLength)
            : CreateFallbackPlan(baseDifficulty, canTurn);
        bool shouldTurn = canTurn && plan.shouldTurn;

        GameObject prefab;
        TrackSegmentType segType;
        float angleDelta = 0f;
        Queue<GameObject> pool;

        if (shouldTurn)
        {
            bool turnRight = AIRunRandom.Value < 0.5f;
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
        data.routeDistance = _plannedDistance;

        _activeSegments.Add(segment);

        if (segType == TrackSegmentType.Straight)
        {
            data.entryDirection = ForwardDirection;
            SpawnObstaclesAndCoins(segment, segType, plan);
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

            EnsureTurnCoverage(segment, angleDelta > 0f ? 1 : -1);

            SpawnObstaclesAndCoins(segment, segType, plan);

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

        WorldStyler.Instance?.DecorateSegment(segment, segType);
        AIRunTelemetry.RecordEvent("track_segment", (int)segType,
            plan.safeLane, plan.difficulty, plan.obstacleChance);
        _plannedDistance += segmentLength;
    }

    void RecycleSegment(GameObject segment)
    {
        TrackSegmentData data = segment.GetComponent<TrackSegmentData>();

        // Clear cached turn if this is the one being recycled
        if (data != null && data == CurrentTurnSegment)
            CurrentTurnSegment = null;

        // Return dynamic objects owned by this segment to their pools.
        for (int i = _dynamicObjects.Count - 1; i >= 0; i--)
        {
            DynamicEntry entry = _dynamicObjects[i];
            if (entry.instance == null)
            {
                _dynamicObjects.RemoveAt(i);
                continue;
            }
            if (entry.ownerSegment == segment)
            {
                ReturnDynamicToPool(entry);
                _dynamicObjects.RemoveAt(i);
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

   void SpawnObstaclesAndCoins(GameObject segment, TrackSegmentType segType, AITrackPlan plan)
   {
       if ((obstaclePrefabs == null || obstaclePrefabs.Length == 0)
           && coinPrefab == null) return;

       float buffer = 4f;
       float end    = segmentLength - 2f;

       if (segType != TrackSegmentType.Straight)
       {
           SpawnCoinsZigzag(segment, buffer, segmentLength * 0.35f);
           return;
       }

        // Warmup: first few segments have no obstacles
        const int warmupSegments = 2;
        _straightSegmentsSpawned++;
        _obstacleFreeSegments++;
        for (int lane = 0; lane < _laneObstacleDrought.Length; lane++)
            _laneObstacleDrought[lane]++;

        float diff = Mathf.Clamp01(plan.difficulty);

        // Preserve route continuity while rotating protection away from lanes
        // that have gone too long without an obstacle.
        int safeLane = ChooseFairSafeLane(
            plan.safeLane, _lastSafeLane, _laneObstacleDrought);
        _lastSafeLane = safeLane;

        // Determine coin Z first so obstacles can avoid it
        float coinZ = AIRunRandom.Range(buffer + 2f, end - 4f);

        // Always put a dense coin trail on the safe lane
        int minCoins = Mathf.Max(2, plan.minCoinCount);
        int maxCoins = Mathf.Max(minCoins + 1, plan.maxCoinCount);
        SpawnCoinLine(segment, safeLane, coinZ,
            AIRunRandom.Range(minCoins, maxCoins));
        // Sometimes add sparse coins on an adjacent lane
        if (AIRunRandom.Value < plan.coinChance)
        {
            int altLane = (safeLane + (AIRunRandom.Value < 0.5f ? -1 : 1) + 3) % 3;
            SpawnCoinLine(segment, altLane,
                coinZ + AIRunRandom.Range(-1f, 1f), AIRunRandom.Range(2, 5));
        }

        bool prefabsReady = obstaclePrefabs != null && obstaclePrefabs.Length >= 3;
        bool shouldSpawnObstacles = prefabsReady && ShouldSpawnObstacleRow(
            _straightSegmentsSpawned, _obstacleFreeSegments, warmupSegments,
            maxConsecutiveObstacleFreeStraights, plan.obstacleChance,
            AIRunRandom.Value);
        if (shouldSpawnObstacles)
        {
            // Place obstacles at a different Z from the coin trail
            float obsZ = coinZ + 3f + AIRunRandom.Range(0f, 3f);
            if (obsZ > end - 1f)
                obsZ = coinZ - 3f - AIRunRandom.Range(0f, 3f);
            obsZ = Mathf.Clamp(obsZ, buffer + 1f, end - 1f);
            if (SpawnObstacleRow(
                    segment, obsZ, diff, safeLane, plan.maxBlockedLanes) > 0)
                _obstacleFreeSegments = 0;
        }
   }

    float CalculateBaseDifficulty()
    {
        float speedFactor = GameManager.Instance != null
            ? Mathf.InverseLerp(GameManager.Instance.startSpeed,
                GameManager.Instance.maxSpeed, GameManager.Instance.CurrentSpeed)
            : 0f;
        float segmentFactor = Mathf.Clamp01(_straightSegmentsSpawned / 15f);
        return Mathf.Max(speedFactor, segmentFactor);
    }

    AITrackPlan CreateFallbackPlan(float difficulty, bool canTurn)
    {
        int safeLane = Mathf.Clamp(
            _lastSafeLane + AIRunRandom.Range(-1, 2), 0, 2);
        return new AITrackPlan
        {
            intent = AIDirectorIntent.Observe,
            difficulty = difficulty,
            obstacleChance = Mathf.Lerp(
                obstacleChance, Mathf.Clamp01(obstacleChance + 0.3f), difficulty),
            coinChance = coinChance,
            minCoinCount = 5,
            maxCoinCount = 8,
            maxBlockedLanes = difficulty > 0.5f ? 2 : 1,
            safeLane = safeLane,
            shouldTurn = canTurn && AIRunRandom.Value < turnChance
        };
    }

    // ---- coin patterns ----

    void SpawnCoinLine(GameObject segment, int lane, float startZ, int count)
    {
        if (coinPrefab == null) return;
        float x = (lane - 1) * laneDistance;
        for (int c = 0; c < count; c++)
        {
            Vector3 lp = new Vector3(x, 1f, startZ + c * 1.8f);
            if (lp.z > segmentLength - 1f) break;
            Vector3 wp = segment.transform.TransformPoint(lp);
            SpawnDynamic(coinPrefab, segment, wp, Quaternion.identity);
        }
    }

   void SpawnCoinsZigzag(GameObject segment, float zStart, float zEnd)
    {
        // Subway Surfers-style: coins weave between lanes
        if (coinPrefab == null) return;
        int steps = AIRunRandom.Range(5, 9);
        float zStep = (zEnd - zStart) / steps;

        int fromLane = AIRunRandom.Range(0, 3);
        for (int i = 0; i < steps; i++)
        {
            float z = zStart + zStep * i;
            int toLane = (fromLane + (AIRunRandom.Value < 0.5f ? 1 : -1) + 3) % 3;
            toLane = Mathf.Clamp(toLane, 0, 2);

            float x = (fromLane - 1) * laneDistance;
            float x2 = (toLane - 1) * laneDistance;

            int coins = AIRunRandom.Range(2, 5);
            for (int c = 0; c < coins; c++)
            {
                float t = (float)c / (coins - 1);
                float cx = Mathf.Lerp(x, x2, t);
                Vector3 lp = new Vector3(cx, 1f, z + c * 1.2f);
                if (lp.z > zEnd) break;
                Vector3 wp = segment.transform.TransformPoint(lp);
                SpawnDynamic(coinPrefab, segment, wp, Quaternion.identity);
            }
            fromLane = toLane;
        }
    }

   // ---- obstacle patterns ----

    // Guarantees at least 1 lane is always open
    int SpawnObstacleRow(GameObject segment, float obsZ, float difficulty, int safeLane,
        int maxBlockedLanes)
    {
       if (obstaclePrefabs == null || obstaclePrefabs.Length < 3) return 0;

       // How many lanes to block (1 or 2, never 3)
        int blocked = difficulty > 0.5f ? 2 : 1;
        blocked = Mathf.Clamp(blocked, 1, Mathf.Clamp(maxBlockedLanes, 1, 2));
        int[] lanes = SelectBlockedLanes(
            safeLane, blocked, _laneObstacleDrought);
        int spawned = 0;

       for (int i = 0; i < lanes.Length; i++)
       {
           int lane = lanes[i];

            // Progressive obstacle types: harder types appear at higher difficulty
           int type;
            if (difficulty < 0.3f)
                type = 0; // early game: only Low obstacles
            else if (difficulty < 0.6f)
                type = AIRunRandom.Value < 0.35f ? 1 : 0; // mid game: Low + High
            else
                type = AIRunRandom.Value < 0.3f
                    ? 2
                    : (AIRunRandom.Value < 0.5f ? 1 : 0); // late: all types

            // Never put a Barrier when only 1 lane is blocked (can't dodge)
            if (blocked == 1 && type == 2) type = 1;

            if (SpawnObstacleAt(
                    segment, lane,
                    obsZ + AIRunRandom.Range(-0.8f, 0.8f), type))
            {
                _laneObstacleDrought[lane] = 0;
                spawned++;
            }
       }
       return spawned;
    }

    bool SpawnObstacleAt(GameObject segment, int lane, float z, int prefabIndex)
    {
        if (obstaclePrefabs == null || prefabIndex < 0
            || prefabIndex >= obstaclePrefabs.Length || obstaclePrefabs[prefabIndex] == null)
            return false;

        float x = (lane - 1) * laneDistance;
        Vector3 lp = new Vector3(x, 1f, z);
        Vector3 wp = segment.transform.TransformPoint(lp);
        Quaternion rot = segment.transform.rotation;
        SpawnDynamic(obstaclePrefabs[prefabIndex], segment, wp, rot);
        return true;
    }

    public static bool ShouldSpawnObstacleRow(int straightSegmentsSpawned,
        int obstacleFreeSegments, int warmupSegments, int maxFreeSegments,
        float chance, float chanceRoll)
    {
        return TrackSpawnRules.ShouldSpawnObstacleRow(straightSegmentsSpawned,
            obstacleFreeSegments, warmupSegments, maxFreeSegments, chance,
            chanceRoll);
    }

    public static int ChooseFairSafeLane(int proposedLane, int previousSafeLane,
        int[] laneObstacleDrought)
    {
        return TrackSpawnRules.ChooseFairSafeLane(
            proposedLane, previousSafeLane, laneObstacleDrought);
    }

    public static int[] SelectBlockedLanes(int safeLane, int blockedLaneCount,
        int[] laneObstacleDrought)
    {
        return TrackSpawnRules.SelectBlockedLanes(
            safeLane, blockedLaneCount, laneObstacleDrought);
    }

   GameObject SpawnDynamic(GameObject prefab, GameObject ownerSegment,
       Vector3 position, Quaternion rotation)
   {
       if (!_dynamicPools.TryGetValue(prefab, out Queue<GameObject> pool))
       {
           pool = new Queue<GameObject>();
           _dynamicPools.Add(prefab, pool);
       }

       GameObject instance = pool.Count > 0
           ? pool.Dequeue()
           : Instantiate(prefab);

       instance.SetActive(false);
       instance.transform.SetParent(ownerSegment.transform, true);
       instance.transform.SetPositionAndRotation(position, rotation);
       instance.SetActive(true);
       if (WorldStyler.Instance != null)
       {
           if (instance.GetComponent<Coin>() != null)
               WorldStyler.Instance.StyleCoin(instance);
           else if (instance.GetComponent<Obstacle>() != null)
               WorldStyler.Instance.StyleObstacle(instance);
       }
       _dynamicObjects.Add(new DynamicEntry
       {
           instance = instance,
           prefab = prefab,
           ownerSegment = ownerSegment
       });
       return instance;
   }

   public void ReleaseDynamic(GameObject instance)
   {
       for (int i = _dynamicObjects.Count - 1; i >= 0; i--)
       {
           DynamicEntry entry = _dynamicObjects[i];
           if (entry.instance != instance) continue;
           ReturnDynamicToPool(entry);
           _dynamicObjects.RemoveAt(i);
           return;
       }

       if (instance != null) instance.SetActive(false);
   }

   void ReturnDynamicToPool(DynamicEntry entry)
   {
       if (entry.instance == null || entry.prefab == null) return;
       entry.instance.SetActive(false);
       entry.instance.transform.SetParent(transform, false);

       if (!_dynamicPools.TryGetValue(entry.prefab, out Queue<GameObject> pool))
       {
           pool = new Queue<GameObject>();
           _dynamicPools.Add(entry.prefab, pool);
       }
       pool.Enqueue(entry.instance);
   }

   void EnsureProceduralAssets()
   {
        Shader sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Mobile/Diffuse");

       int groundLayer = LayerMask.NameToLayer("Ground");
       if (groundLayer < 0) groundLayer = 0;

       if (trackSegmentPrefab == null)
            trackSegmentPrefab = CreateProcStraight(groundLayer);
       if (turnLeftPrefab == null)
            turnLeftPrefab = CreateProcTurn(groundLayer, -1);
       if (turnRightPrefab == null)
            turnRightPrefab = CreateProcTurn(groundLayer, 1);
       if (coinPrefab == null)
           coinPrefab = CreateProcCoin();
       bool missingObstaclePrefab = obstaclePrefabs == null || obstaclePrefabs.Length < 3;
       if (!missingObstaclePrefab)
       {
           for (int i = 0; i < 3; i++)
           {
               if (obstaclePrefabs[i] == null)
               {
                   missingObstaclePrefab = true;
                   break;
               }
           }
       }
       if (missingObstaclePrefab)
           obstaclePrefabs = CreateProcObstacles();
   }

    GameObject CreateProcStraight(int layer)
    {
       return CreateProcTrackRoot("ProcStraight", layer);
    }

    GameObject CreateProcTurn(int layer, int turnDirection)
    {
        GameObject root = new GameObject(
            turnDirection > 0 ? "ProcTurnRight" : "ProcTurnLeft");
        root.layer = layer;
        EnsureTurnCoverage(root, turnDirection);
        root.SetActive(false);
        root.transform.SetParent(transform);
        return root;
    }

    void EnsureTurnCoverage(GameObject segment, int turnDirection)
    {
        if (segment == null || segment.transform.Find("RuntimeTurnCoverage") != null)
            return;

        int layer = LayerMask.NameToLayer("Ground");
        if (layer < 0) layer = segment.layer;

        Material material = null;
        Renderer[] existingRenderers = segment.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < existingRenderers.Length; i++)
        {
            if (existingRenderers[i].sharedMaterial == null) continue;
            material = existingRenderers[i].sharedMaterial;
            break;
        }

        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Mobile/Diffuse");
            if (shader != null)
                material = new Material(shader) { color = new Color(0.25f, 0.28f, 0.35f) };
        }

        GameObject coverage = new GameObject("RuntimeTurnCoverage");
        coverage.layer = layer;
        coverage.transform.SetParent(segment.transform, false);

        CreateTurnSurface("EntryCoverage", coverage.transform,
            new Vector3(0f, -0.15f, segmentLength * 0.5f),
            Quaternion.identity, new Vector3(15f, 0.3f, segmentLength), layer, material);
        CreateTurnSurface("ExitCoverage", coverage.transform,
            new Vector3(turnDirection * segmentLength * 0.5f, -0.15f,
                segmentLength * 0.5f),
            Quaternion.Euler(0f, 90f, 0f),
            new Vector3(15f, 0.3f, segmentLength), layer, material);
        CreateTurnSurface("CornerCoverage", coverage.transform,
            new Vector3(0f, -0.14f, segmentLength * 0.5f),
            Quaternion.identity, new Vector3(15f, 0.32f, 15f), layer, material);
    }

    static void CreateTurnSurface(string name, Transform parent, Vector3 localPosition,
        Quaternion localRotation, Vector3 localScale, int layer, Material material)
    {
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = name;
        surface.layer = layer;
        surface.transform.SetParent(parent, false);
        surface.transform.localPosition = localPosition;
        surface.transform.localRotation = localRotation;
        surface.transform.localScale = localScale;
        if (material != null)
            surface.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    GameObject CreateProcTrackRoot(string name, int layer)
    {
        Shader sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Mobile/Diffuse");

        GameObject root = new GameObject(name);
        root.layer = layer;

        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = "Surface";
        surface.layer = layer;
        surface.transform.SetParent(root.transform, false);
        surface.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        surface.transform.localScale = new Vector3(9f, 0.3f, 20f);
        if (sh != null)
            surface.GetComponent<MeshRenderer>().material = new Material(sh)
            {
                color = new Color(0.25f, 0.28f, 0.35f)
            };

        root.SetActive(false);
        root.transform.SetParent(transform);
        return root;
    }

   GameObject CreateProcCoin()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "ProcCoin";
        go.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
        Collider coinCollider = go.GetComponent<Collider>();
        if (coinCollider == null) coinCollider = go.AddComponent<BoxCollider>();
        coinCollider.isTrigger = true;
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
            BoxCollider bc = obs[i].GetComponent<BoxCollider>();
            if (bc == null) bc = obs[i].AddComponent<BoxCollider>();
            bc.isTrigger = true; bc.size = Vector3.one;
            Obstacle o = obs[i].AddComponent<Obstacle>();
            o.type = types[i];
            obs[i].SetActive(false); obs[i].transform.SetParent(transform);
        }
        return obs;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
