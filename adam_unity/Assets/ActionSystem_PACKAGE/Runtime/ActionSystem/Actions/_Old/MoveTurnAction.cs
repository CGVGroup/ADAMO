using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace ActionSystem
{
    [Serializable]
    public enum MoveTurnActionFailCode
    {
        // For tool calls
        DestinationNotReachable,
        NewDestinationSuggested
    }
    
    [Serializable]
    public class MoveTurnAction : AgentAction
    {
        // Input Params
        [SerializeField] private Transform reqDestination;
        [SerializeField] private Vector3 turnToPoint;
        
        // Output Params
        [SerializeField] private Transform destination;
        [SerializeField] private Transform suggestedDestination;
        
        [SerializeField] private LocomotionSystem locomotionSystem;
        public Vector3 SuggestedDestination => suggestedDestination.position;

        //public MoveTurnAction(Agent agent, Transform origin, Vector3 movementLocal, Vector3 turnToPointLocal)
        public MoveTurnAction(Agent agent, Vector3 moveToPoint, Vector3 turnToPoint)
        {
            //Assert.IsNotNull(origin);
            Assert.IsNotNull(agent.LocomotionSystem);

            this.locomotionSystem = agent.LocomotionSystem;
            //this.origin = origin;
            //this.turnToPointGlobal = origin.TransformPoint(turnToPointLocal);
            this.turnToPoint = turnToPoint;

            this.reqDestination = GameObject.Find("WalkAction_ReqDestination").transform;
            //reqDestination.position = origin.TransformPoint(movementLocal);
            reqDestination.position = moveToPoint;
            
            //reqDestination.transform.up = Vector3.up;
        }

        protected internal override void Setup()
        {
            suggestedDestination = GameObject.Find("WalkAction_SuggestedDestination").transform;
            
            //if(IsPointNear(destination.position, locomotionSystem.transform.position, ))

            if (!locomotionSystem.CanReach(reqDestination.position))
            {
                Vector3 reachPosition;
                if (locomotionSystem.CanReachNearPoint(reqDestination.position, 5f,
                        out reachPosition)) //TODO: Distanza di check arbitraria!!!
                {
                    suggestedDestination.position = reachPosition;
                    //reachableDestination = origin.InverseTransformPoint(reachPosition);
                    SetFailedState(MoveTurnActionFailCode.NewDestinationSuggested);
                    SetLog($"Destination unreachable but {suggestedDestination.position} is the nearest reachablePosition");
                }
                else
                {
                    SetFailedState(MoveTurnActionFailCode.DestinationNotReachable);
                    SetLog($"Destination totally unreachable");
                }
            }
            else
            {
                GameObject.Find("TurnToPoint_Transform").transform.position = turnToPoint;
                
                Vector3 turnToPointProjectedOnXZ = new Vector3(
                    turnToPoint.x, 
                    locomotionSystem.transform.position.y, 
                    turnToPoint.z);
                reqDestination.LookAt(turnToPointProjectedOnXZ, Vector3.up);

                float dotProduct = Vector3.Dot(reqDestination.transform.up, Vector3.up);
                if (dotProduct < 0.99f)
                {
                    Debug.LogWarning(
                        $"This should not happen : reqDestination.transform.up is not up!\n" +
                        $"DotProduct={dotProduct}\n" +
                        $"Maybe VH tried to look to the same position it wanted to go?\n" +
                        $"I will continue anyway manually adjusting the rotation.");
                    reqDestination.LookAt(locomotionSystem.transform.position + locomotionSystem.transform.forward, Vector3.up);
                }

                SetState(ActionState.Updating);
            }
        }

        protected internal override void OnStart()
        {
            //Debug.Log("Move started");

            this.destination = GameObject.Find("WalkAction_Destination").transform;
            destination.position = reqDestination.position;
            destination.rotation = reqDestination.rotation;
            
            locomotionSystem.SetDestination(destination);
            locomotionSystem.OnDestinationArrival += OnDestinationArrival;
        }

        protected internal override void OnUpdate()
        {
            //throw new NotImplementedException();
        }

        protected internal override void OnComplete()
        {
            //Debug.Log("Move completed");
        }

        protected internal override void OnStop()
        {
            throw new NotImplementedException();
        }

        protected internal override void OnFail()
        {
            throw new NotImplementedException();
        }

        private void OnDestinationArrival()
        {
            locomotionSystem.OnDestinationArrival -= OnDestinationArrival;
            SetState(ActionState.Completed);
        }

        // private bool IsPointNear(Vector3 point, Vector3 target, float distance)
        // {
        //     if((target-point).magnitude <= distance)
        //         return true;
        //     else
        //         return false;
        // }
    }
}