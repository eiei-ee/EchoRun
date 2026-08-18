using UnityEngine;

public enum TrackSegmentType { Straight, TurnLeft, TurnRight }

public class TrackSegmentData : MonoBehaviour
{
    public TrackSegmentType segmentType;
    public float routeDistance;
    public Vector3 spawnOrigin;
    public float spawnAngle;
    public Vector3 entryDirection;
    public Vector3 exitDirection;
    public Vector3 turnPointWorld;
}
