using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AreaCheckerParams", menuName = "ADAMO Solution Checkers Data/AreaChecker_Params")]
public class AreaCheckerParams : SolutionCheckerParams
{
    public int obj;
    public List<int> colliders;
}
