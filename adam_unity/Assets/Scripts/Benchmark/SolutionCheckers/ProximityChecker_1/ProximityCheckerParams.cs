using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public struct ObjTargetPair
{
    public int objId;
    public int targetId;
}

[CreateAssetMenu(fileName = "ProximityCheckerParams", menuName = "ADAMO Solution Checkers Data/ProximityChecker_Params")]
public class ProximityCheckerParams : SolutionCheckerParams
{
    public List<ObjTargetPair> pairs;
    public float threshold;
}
