using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// A SolutionChecker that verifies if a specified set of objects
/// are arranged in a circular pattern on the floor.
/// ID: Check_5
/// </summary>
[Serializable]
public class CircularLayoutChecker : SolutionCheckerBase
{
    // --- Parameters ---
    [SerializeField] private List<ObjectTag> objs = new List<ObjectTag>();
    [SerializeField] private float floorThreshold;
    [SerializeField] private float circularityThreshold;
    
    // Internal variables
    [SerializeField] private float floorCompletion = 0f;
    [SerializeField] private float circularityCompletion = 0f;
    
    // Assuming a reference to the 'floor' or navmesh is available. 
    // For this example, we'll check against a simple y-coordinate.
    [SerializeField] private float floorHeight = 0f;

    public override IEnumerator SetupCheckerData<T>(T checkerParams)
    {
        yield return new WaitForEndOfFrame();
        
        Assert.IsTrue(checkerParams is CircularLayoutCheckerParams);
        CircularLayoutCheckerParams p = checkerParams as CircularLayoutCheckerParams;

        foreach (int objId in p.objs)
        {
            ObjectTag objTag = SolutionCheckerManager.Instance.GetObjTagFromId(objId);
            if(objTag !=null)
                objs.Add(objTag);
            else
                Debug.LogError("CircularLayoutChecker Error: ObjTag_ID Not Found: " + objId);
        }

        Assert.IsTrue(objs.Count > 3, "There should be at least 3 objects to determine circularity!");
        
        floorThreshold = p.floorThreshold;
        circularityThreshold = p.circularityThreshold;
    }

    public override float CheckCompletion()
    {
        // --- Success Criteria 1: All targets lie on the floor ---
        bool allOnFloor = AreAllTargetsOnFloor();

        // --- Success Criteria 2: Circularity of targets ---
        float circularityScore = CalculateCircularity();

        bool circularityMet = circularityScore >= circularityThreshold;

        // Update completion: 50% for floorCompletion, 50% for circularityCompletion
        completion = (floorCompletion * 0.5f) + (circularityCompletion * 0.5f);

        // Draw debug information
        DrawDebug(circularityScore, allOnFloor);

        return completion;
    }
    
    /// <summary>
    /// Checks if all target objects are within the floorThreshold distance from the floor height.
    /// </summary>
    private bool AreAllTargetsOnFloor()
    {
        int trueCount = 0;
        int falseCount = 0;
        
        foreach (ObjectTag objTag in objs)
        {
            if (Mathf.Abs(objTag.transform.position.y - floorHeight) > floorThreshold)
                falseCount++;
            else
                trueCount++;
        }
        
        floorCompletion = (float) trueCount / ((float) (trueCount + falseCount));
        
        if(falseCount == 0)
            return true;
        else
            return false;
    }

    /// <summary>
    /// Calculates a circularity score based on both distance from the center and angular spacing.
    /// A score of 1 represents a perfect regular polygon layout.
    /// </summary>
    /// <returns>A circularity score between 0 and 1.</returns>
    private float CalculateCircularity()
    {
        List<Vector2> points = objs.Select(t => new Vector2(t.transform.position.x, t.transform.position.z)).ToList();

        // 1. Find the geometric center (centroid) of the points.
        Vector2 center = Vector2.zero;
        foreach (var p in points)
        {
            center += p;
        }
        center /= points.Count;

        // --- Part 1: Calculate Distance Regularity Score ---
        List<float> distances = points.Select(p => Vector2.Distance(p, center)).ToList();
        float averageDistance = distances.Average();
        if (averageDistance < 0.001f) return 0; // Avoid division by zero if all points are at the center.

        float sumOfSquaresDist = distances.Select(d => (d - averageDistance) * (d - averageDistance)).Sum();
        float stdDevDist = Mathf.Sqrt(sumOfSquaresDist / distances.Count);
        float distanceScore = Mathf.Max(0, 1.0f - (stdDevDist / averageDistance));

        // --- Part 2: Calculate Angle Regularity Score ---
        // Get angles for each point relative to the center
        List<float> angles = points.Select(p => Mathf.Atan2(p.y - center.y, p.x - center.x)).ToList();
        angles.Sort(); // Sort angles to process them in order around the circle

        // Calculate the angular separation between adjacent points
        List<float> angleDiffs = new List<float>();
        for (int i = 0; i < angles.Count - 1; i++)
        {
            angleDiffs.Add(angles[i + 1] - angles[i]);
        }
        // Add the final angle diff, wrapping around from the last to the first point
        angleDiffs.Add((angles[0] + 2 * Mathf.PI) - angles[angles.Count - 1]);

        // Calculate the standard deviation of these angle differences
        float averageAngleDiff = angleDiffs.Average(); // This should be close to the ideal angle
        if (averageAngleDiff < 0.001f) return 0;

        float sumOfSquaresAngles = angleDiffs.Select(d => (d - averageAngleDiff) * (d - averageAngleDiff)).Sum();
        float stdDevAngles = Mathf.Sqrt(sumOfSquaresAngles / angleDiffs.Count);
        
        // Normalize the angle deviation to get a score from 0 to 1.
        float angleScore = Mathf.Max(0, 1.0f - (stdDevAngles / averageAngleDiff));

        // --- Part 3: Combine Scores ---
        // Multiply the scores. If either is poor, the final score will be poor.
        circularityCompletion = distanceScore * angleScore;
        
        return circularityCompletion;
    }


    /// <summary>
    /// Draws debug lines and information in the editor.
    /// </summary>
    private void DrawDebug(float circularity, bool onFloor)
    {
        // Draw floor check lines
        Color floorColor = onFloor ? Color.green : Color.red;
        foreach (var obj in objs)
        {
             Debug.DrawLine(obj.transform.position, new Vector3(obj.transform.position.x, floorHeight, obj.transform.position.z), floorColor, Time.deltaTime);
        }

        // Draw circularity debug info
        Color circleColor = circularity >= circularityThreshold ? Color.green : Color.red;
        List<Vector2> points = objs.Select(t => new Vector2(t.transform.position.x, t.transform.position.z)).ToList();
        Vector2 center2D = Vector2.zero;
        foreach (var p in points) center2D += p;
        center2D /= points.Count;
        Vector3 center3D = new Vector3(center2D.x, floorHeight, center2D.y);

        foreach (var objTag in objs)
        {
            Debug.DrawLine(center3D, objTag.transform.position, circleColor, Time.deltaTime);
        }
    }
}

