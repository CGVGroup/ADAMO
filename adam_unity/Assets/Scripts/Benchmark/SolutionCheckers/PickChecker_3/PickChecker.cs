using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class PickChecker : SolutionCheckerBase
{
    [SerializeField] private List<Pickable> pickables = new List<Pickable>();
    
    private List<Action> drawDebugQueue = new List<Action>();
    
    private void DrawDebug(Pickable obj, Color color)
    {
        //Save current gizmos color
        Color gizmosDefaultColor = Gizmos.color;
        
        Gizmos.color = color;
        Gizmos.DrawSphere(obj.transform.position, 0.025f);
        
        //Reset default gizmos color
        Gizmos.color = gizmosDefaultColor;
    }
    
    public override IEnumerator SetupCheckerData<T>(T checkerParams)
    {
        yield return new WaitForEndOfFrame();
        
        Assert.IsTrue(checkerParams is PickCheckerParams);
        PickCheckerParams p = checkerParams as PickCheckerParams;
        
        foreach (int objId in p.objs)
        {
            ObjectTag objTag = SolutionCheckerManager.Instance.GetObjTagFromId(objId);
            pickables.Add(objTag.GetComponent<Pickable>());
        }
    }

    public override float CheckCompletion()
    {
        int trueCount = 0;
        int falseCount = 0;
        
        foreach (Pickable obj in pickables)
        {
            if (obj.IsBeingCarried)
            {
                drawDebugQueue.Add(() => DrawDebug(obj, Color.green));
                trueCount++;
            }
            else
            {
                drawDebugQueue.Add(() => DrawDebug(obj, Color.red));
                falseCount++;
            }
        }
        
        completion = ((float) trueCount) / ((float) (trueCount + falseCount));

        return completion;
    }

    private void OnDrawGizmos()
    {
        foreach (Action operation in drawDebugQueue)
            operation.Invoke();
        drawDebugQueue.Clear();
    }
}