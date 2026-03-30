using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "CircularLayoutCheckerParams", menuName = "ADAMO Solution Checkers Data/CircularLayoutChecker_Params")]
public class CircularLayoutCheckerParams : SolutionCheckerParams
{
    public List<int> objs;
    
    public float floorThreshold = 0.3f;
    [Range(0f, 1f)]
    public float circularityThreshold = 0.75f;
}