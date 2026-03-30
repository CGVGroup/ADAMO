using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

[Serializable]
public class FloorChecker : SolutionCheckerBase
{
    [SerializeField] private float threshold;
    [SerializeField] private List<ObjectTag> objectTags = new List<ObjectTag>();

    [SerializeField] private float groundY = 0f; // Floor level is at 0f circa in S1
    
    private void DrawDebug(ObjectTag obj, Color color)
    {
        Debug.DrawLine(obj.transform.position, new Vector3(obj.transform.position.x, groundY, obj.transform.position.z), color);
    }

    public override IEnumerator SetupCheckerData<T>(T checkerParams)
    {
        yield return new WaitForEndOfFrame();
        
        Assert.IsTrue(checkerParams is FloorCheckerParams);
        FloorCheckerParams p = checkerParams as FloorCheckerParams;

        foreach (int objId in p.objs)
        {
            ObjectTag objTag = SolutionCheckerManager.Instance.GetObjTagFromId(objId);
            if (objTag != null)
                objectTags.Add(objTag);
            else
                Debug.LogError("FloorChecker Error: ObjTag_ID Not Found: " + objId);
        }
        
        threshold = p.threshold;
    }

    public override float CheckCompletion()
    {
        int trueCount = 0;
        int falseCount = 0;
        
        foreach (ObjectTag obj in objectTags)
        {
            if ((obj.transform.position.y - groundY) >= threshold)
            {
                DrawDebug(obj, Color.red);
                falseCount++;
            }
            else
            {
                DrawDebug(obj, Color.green);
                trueCount++;
            }
        }
        
        completion = ((float) trueCount) / ((float) (trueCount + falseCount));

        return completion;
    }
}
