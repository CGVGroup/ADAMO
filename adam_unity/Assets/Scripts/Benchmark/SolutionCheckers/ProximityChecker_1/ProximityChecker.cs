using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

[Serializable]
public class ProximityChecker : SolutionCheckerBase
{
    [SerializeField] private List<Tuple<ObjectTag,ObjectTag>> objPairs = new List<Tuple<ObjectTag, ObjectTag>>(); // < objToMove, targetToMoveObjTo >
    [SerializeField] private float threshold;
    
    private void DrawDebug(ObjectTag obj, ObjectTag target, Color color)
    {
        Debug.DrawLine(obj.transform.position, target.transform.position, color);
    }

    public override IEnumerator SetupCheckerData<T>(T checkerParams)
    {
        yield return new WaitForEndOfFrame();
        
        Assert.IsTrue(checkerParams is ProximityCheckerParams);
        ProximityCheckerParams p = checkerParams as ProximityCheckerParams;
        
        Assert.IsNotNull(p);
        Assert.IsNotNull(p.pairs);
        Assert.IsTrue(p.pairs.Count > 0);
        
        //obj = SolutionCheckerManager.Instance.GetObjTagFromId(checkerParams.objPairs);
        foreach (ObjTargetPair pair in p.pairs)
        {
            ObjectTag obj = SolutionCheckerManager.Instance.GetObjTagFromId(pair.objId);
            ObjectTag target = SolutionCheckerManager.Instance.GetObjTagFromId(pair.targetId);
            
            Assert.IsNotNull(obj, $"ProximityChecker Error: Object_ID {pair.objId} for Obj Not Found");
            Assert.IsNotNull(target, $"ProximityChecker Error: Object_ID {pair.targetId} for Target Not Found");
            
            Tuple<ObjectTag, ObjectTag> t = new Tuple<ObjectTag, ObjectTag>(obj, target);
            
            objPairs.Add(t);
        }
        
        threshold = p.threshold;

        // Check that objects aren't already inside tolerance range
        foreach (Tuple<ObjectTag, ObjectTag> pair in objPairs)
        {
            float distance = Vector3.Distance(pair.Item1.CenterPosition, pair.Item2.CenterPosition);
            if (distance < threshold)
                Debug.LogError($"Object {pair.Item1} and {pair.Item2} are already positioned close to each other, with distance: {distance} m < {threshold} m");
        }
    }

    public override float CheckCompletion()
    {
        int trueCount = 0;
        int falseCount = 0;
        
        foreach (var (obj,target) in objPairs)
        {
            if ((obj.transform.position - target.transform.position).magnitude <= threshold)
            {
                DrawDebug(obj, target, Color.green);
                trueCount++;
            }
            else
            {
                DrawDebug(obj, target, Color.red);
                falseCount++;
            }
        }
        
        completion = ((float) trueCount) / ((float) (falseCount + trueCount));

        return completion;
    }
}
