using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class AreaChecker : SolutionCheckerBase
{
    [SerializeField] private ObjectTag obj;
    [SerializeField] private List<AreaTag> areaTags = new List<AreaTag>();
    
    [SerializeField] private Vector3 startPosition;
    
    private List<Action> drawDebugQueue = new List<Action>();
    
    private void DrawDebug(Collider col, Color color)
    {
        //Save current gizmos color
        Color gizmosDefaultColor = Gizmos.color;
        
        Gizmos.color = color;
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.DrawSphere(obj.CenterPosition, 0.025f);
        
        Vector3 nearestPoint = col.ClosestPointOnBounds(obj.CenterPosition);
        Debug.DrawLine(obj.CenterPosition, nearestPoint, color);
        
        //Reset default gizmos color
        Gizmos.color = gizmosDefaultColor;
    }
    
    public override IEnumerator SetupCheckerData<T>(T checkerParams)
    {
        yield return new WaitForEndOfFrame();
        
        Assert.IsTrue(checkerParams is AreaCheckerParams);
        AreaCheckerParams p = checkerParams as AreaCheckerParams;

        obj = SolutionCheckerManager.Instance.GetObjTagFromId(p.obj);

        foreach (int areaId in p.colliders)
        {
            AreaTag areaTag = AreaTag.GetAreaTag(areaId);
            if (areaTag != null)
                areaTags.Add(areaTag);
            else
                Debug.LogError("AreaChecker Error: AreaTag_ID Not Found: " + areaId);
        }

        Assert.IsTrue(areaTags.Count > 0);
        Assert.IsNotNull(obj);
        
        startPosition = obj.CenterPosition;
    }

    public override float CheckCompletion()
    {
        float currentMaxCompletion = 0f;
        
        foreach(AreaTag areaTag in areaTags)
        {
            if (CheckForOverlap(areaTag.Collider, obj))
            {
                //DrawDebug(areaTag.Collider, Color.green);
                drawDebugQueue.Add(() => DrawDebug(areaTag.Collider, Color.green));
                currentMaxCompletion = 1f;
            }
            else
            {
                float initialDistance = DistancePointToCollider(startPosition, areaTag.Collider);
                float distanceFromCompletion = DistancePointToCollider(obj.CenterPosition, areaTag.Collider);
                
                float areaRelativeCompletion = (initialDistance - distanceFromCompletion)/initialDistance;
                areaRelativeCompletion = Mathf.Clamp(areaRelativeCompletion, 0f, 1f);
                
                currentMaxCompletion = Mathf.Max(currentMaxCompletion, areaRelativeCompletion);
                
                //DrawDebug(areaTag.Collider, Color.cyan);
                drawDebugQueue.Add(() => DrawDebug(areaTag.Collider, Color.cyan));
            }
        }
        
        completion = currentMaxCompletion;

        return completion;
    }
    
    private bool CheckForOverlap(Collider areaCol, ObjectTag objTag)
    {
        Assert.IsNotNull(areaCol);
        Assert.IsNotNull(objTag);
        
        Vector3 colCenter = areaCol.bounds.center;
        Vector3 halfExtents = areaCol.bounds.extents;
        Quaternion orientation = areaCol.transform.rotation;
        
        // This returns an array of all colliders that are overlapping the box we just defined.
        Collider[] hitColliders = Physics.OverlapBox(colCenter, halfExtents, orientation);
        
        if (hitColliders.Length > 0)
        {
            //Debug.Log($"Overlap detected! Hit {hitColliders.Length} object(s).");
            
            foreach (var hitCollider in hitColliders)
            {
                ObjectTag hitObj = hitCollider.GetComponent<ObjectTag>();
                
                if (hitObj == null)
                    continue;
                
                if(hitObj.GetComponent<Collider>() == objTag.ObjectCollider)
                {
                    // If it collided with the actual object I am checking
                    Debug.Log("Hit: " + hitObj.GetStringId());
                    completion = 1f;
                    return true;
                }
            }
        }
        
        //If it didn't collide with anything or with any ObjectTag
        return false;
    }
    
    public float DistancePointToCollider(Vector3 point, Collider collider)
    {
        // Get the closest point on the bounding box to the given point.
        Vector3 closestPoint = collider.bounds.ClosestPoint(point);

        // Calculate the distance between the original point and the closest point on the bounds.
        return Vector3.Distance(point, closestPoint);
    }
    
    private void OnDrawGizmos()
    {
        foreach (Action operation in drawDebugQueue)
            operation.Invoke();
        drawDebugQueue.Clear();
    }
}
