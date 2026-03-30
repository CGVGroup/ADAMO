using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "FloorCheckerParams", menuName = "ADAMO Solution Checkers Data/FloorChecker_Params")]
public class FloorCheckerParams : SolutionCheckerParams
{
    public List<int> objs;
    public float threshold;
}
