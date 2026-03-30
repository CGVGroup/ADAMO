using System;
using System.IO;
using MxM;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;

[RequireComponent(typeof(NavMeshAgent))]
public class LocomotionSystem : MonoBehaviour
{
    private NavMeshAgent navAgent;
    private Animator animator;
    public Animator Animator => animator;
    private MxMAnimator mxmAnim;
    public MxMAnimator MotionMatchingAnim => mxmAnim;
    private MxMTIPExtension tipModule;

    [Range(0f,0.30f)]
    public float angleDiffThres = 0.15f;
    
    public Action OnDestinationArrival;

    protected void Start()
    {
        navAgent = this.GetComponent<NavMeshAgent>();
        animator = this.GetComponent<Animator>();
        
        mxmAnim = this.GetComponent<MxMAnimator>();
        tipModule = this.GetComponent<MxMTIPExtension>();

        tipModule.TIPVector = this.transform.forward;
        
        // Disable TIP module by start default
        tipModule.enabled = false;
    }
    
    public bool SetDestination(Transform destination)
    {
        // Enable TIPModule
        tipModule.enabled = true;
        tipModule.TIPVector = destination.transform.forward;
        return SetDestination(destination.position);
    }
    
    public bool SetDestination(Vector3 destinationPos)
    {
        // Disable TIP module
        tipModule.enabled = false;
        
        if (CanReach(destinationPos))
        {
            navAgent.SetDestination(destinationPos);
            return true;
        }
        else 
            return false;
    }

    public void SetTurnToPoint(Vector3 turnToPoint)
    {
        // Enable TIP module
        tipModule.enabled = true;
        
        GameObject dummyGO = new GameObject();
        dummyGO.transform.position = this.transform.position;
        dummyGO.transform.rotation = this.transform.rotation;
            
        Vector3 turnToPointProjectedOnXZ = new Vector3(
            turnToPoint.x, 
            this.transform.position.y, 
            turnToPoint.z);
        dummyGO.transform.LookAt(turnToPointProjectedOnXZ, Vector3.up);

        float dotProduct = Vector3.Dot(dummyGO.transform.up, Vector3.up);
        if (dotProduct < 0.99f)
        {
            Debug.LogWarning(
                $"This should not happen : reqDestination.transform.up is not up!\n" +
                $"DotProduct={dotProduct}\n" +
                $"Maybe VH tried to look to the same position it wanted to go?\n" +
                $"I will continue anyway manually adjusting the rotation.");
            dummyGO.transform.LookAt(this.transform.position + this.transform.forward, Vector3.up);
        }
        
        tipModule.TIPVector = dummyGO.transform.forward;
        
        Destroy(dummyGO);
    }

    public bool CanReach(Vector3 position)
    {
        NavMeshHit navHit;
        NavMeshPath navPath = new NavMeshPath();
        
        bool isOnNavMesh = NavMesh.SamplePosition(position, out navHit, 0.1f, NavMesh.AllAreas); //TODO: Distanza di check arbitraria!!!
        bool hasReachablePath =
            NavMesh.CalculatePath(navAgent.transform.position, position, NavMesh.AllAreas, navPath);

        return (isOnNavMesh && hasReachablePath);
    }

    public bool CanReachNearPoint(Vector3 position, float maxDistance, out Vector3 reachPos)
    {
        NavMeshHit navHit;
        NavMeshPath navPath = new NavMeshPath();
        
        NavMesh.SamplePosition(position, out navHit, maxDistance, NavMesh.AllAreas);
        reachPos = navHit.position;

        return CanReach(navHit.position);
    }

    private void LateUpdate()
    {
        if (IsInPlace() && (IsTurnedRight() || !tipModule.enabled) && IsIdle())
            OnDestinationArrival?.Invoke(); 
    }

    private bool IsInPlace()
    {
        // Check if the agent has a path and is not in the process of calculating one.
        if (!navAgent.pathPending)
        {
            // Check if the agent has a complete path.
            //if (navAgent.pathStatus == NavMeshPathStatus.PathComplete) //To complete action if moving in the same place has the navagent already is!!!
            {
                // Check if the agent is close enough to the destination.
                if (navAgent.remainingDistance <= navAgent.stoppingDistance)
                {
                    // Optional: Check if the agent has stopped moving.
                    if (!navAgent.hasPath || navAgent.velocity.sqrMagnitude == 0f)
                    {
                        //Debug.Log("Agent has arrived at the destination!");
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool IsTurnedRight()
    {
        if (Vector3.Dot(this.transform.forward, tipModule.TIPVector) > (1f - angleDiffThres))
            return true;
        else
            return false;
    }

    private bool IsIdle()
    {
        return mxmAnim.IsIdle;
    }
    
}
