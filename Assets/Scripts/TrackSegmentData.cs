using UnityEngine;

public enum TrackSegmentType { Straight, TurnLeft, TurnRight }

public class TrackSegmentData : MonoBehaviour
{
    public TrackSegmentType segmentType;
    public Vector3 entryDirection;
    public Vector3 exitDirection;
    public Vector3 turnPointWorld;
}
