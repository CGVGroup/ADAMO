using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks the transform history of a GameObject and visualizes it in the Scene view using Gizmos,
/// including a full 3-axis representation of rotation.
/// </summary>
public class TransformTracker : MonoBehaviour
{
    [Header("Visualization Settings")]
    [Tooltip("Draw local rotation?")]
    public bool drawRotation = false;
    
    [Tooltip("Toggle the visibility of the path in the Scene view.")]
    public bool visualizePath = true;

    [Tooltip("The color of the visualized path line and points.")]
    public Color pathColor = Color.cyan;

    [Tooltip("The radius of the spheres drawn at each recorded point.")]
    public float pointRadius = 0.05f;

    [Tooltip("The length of the lines indicating the transform's axes (rotation).")]
    public float rotationIndicatorLength = 0.25f;


    [Header("Recording Settings")]
    [Tooltip("The minimum distance the object must move to record a new point.")]
    public float positionThreshold = 0.1f;

    [Tooltip("The minimum angle (in degrees) the object must rotate to record a new point.")]
    public float rotationThreshold = 1.0f;

    private bool isFirst = true;
    
    // A simple struct to hold both position and rotation at a specific moment.
    private struct TransformSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    // The list that stores the entire history of the transform.
    private readonly List<TransformSnapshot> _history = new List<TransformSnapshot>();
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;

    void Awake()
    {
        // Clear any previous history and record the starting state.
        _history.Clear();
        RecordSnapshot();
        _lastPosition = transform.position;
        _lastRotation = transform.rotation;
    }

    void Update()
    {
        // Check if the distance or angle moved exceeds the defined thresholds.
        float distanceMoved = Vector3.Distance(transform.position, _lastPosition);
        float angleTurned = Quaternion.Angle(transform.rotation, _lastRotation);

        if (distanceMoved > positionThreshold || angleTurned > rotationThreshold)
        {
            RecordSnapshot();
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }
    }
    
    /// <summary>
    /// A public method to allow other scripts or UI buttons to clear the history.
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
        // After clearing, re-add the current position as the new starting point.
        RecordSnapshot();
        _lastPosition = transform.position;
        _lastRotation = transform.rotation;
        Debug.Log("Transform history cleared.");
    }

    private void RecordSnapshot()
    {
        if (isFirst)
        {
            isFirst = false;
            return;
        }
        
        _history.Add(new TransformSnapshot
        {
            Position = transform.position,
            Rotation = transform.rotation
        });
    }

    void OnDrawGizmos()
    {
        // If visualization is disabled or there's nothing to draw, do nothing.
        if (!visualizePath || _history.Count == 0)
        {
            return;
        }

        // Loop through all points in the history.
        for (int i = 0; i < _history.Count; i++)
        {
            TransformSnapshot current = _history[i];

            // Set the color for the path line and sphere.
            Gizmos.color = pathColor;

            // Draw a sphere at the current point.
            Gizmos.DrawSphere(current.Position, pointRadius);
            
            // Draw a line connecting this point to the previous one.
            if (i > 0)
            {
                TransformSnapshot previous = _history[i - 1];
                Gizmos.DrawLine(previous.Position, current.Position);
            }

            // === MODIFIED SECTION: Draw all 3 axes for rotation ===

            if (drawRotation)
            {
                // Draw the Forward vector (Blue for Z-axis)
                Gizmos.color = Color.blue;
                Vector3 forwardDir = current.Rotation * Vector3.forward;
                Gizmos.DrawLine(current.Position, current.Position + forwardDir * rotationIndicatorLength);

                // Draw the Up vector (Green for Y-axis)
                Gizmos.color = Color.green;
                Vector3 upDir = current.Rotation * Vector3.up;
                Gizmos.DrawLine(current.Position, current.Position + upDir * rotationIndicatorLength);

                // Draw the Right vector (Red for X-axis)
                Gizmos.color = Color.red;
                Vector3 rightDir = current.Rotation * Vector3.right;
                Gizmos.DrawLine(current.Position, current.Position + rightDir * rotationIndicatorLength);
            }
        }
    }
}