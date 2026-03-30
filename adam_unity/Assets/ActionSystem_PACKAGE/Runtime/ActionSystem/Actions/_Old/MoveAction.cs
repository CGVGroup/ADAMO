using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace ActionSystem
{
    [Serializable]
    public class MoveAction : AgentAction
    {
        [SerializeField] private Transform destination;
        [SerializeField] private Transform origin;
        [SerializeField] private Vector3 movement;
        [SerializeField] private LocomotionSystem locomotionSystem;

        [SerializeField] private Vector3 reachableDestination;
        public Vector3 ReachableDestination => reachableDestination;

        public MoveAction(Agent agent, Transform origin, Vector3 movement)
        {
            Assert.IsNotNull(origin);
            Assert.IsNotNull(agent.LocomotionSystem);

            this.locomotionSystem = agent.LocomotionSystem;
            this.origin = origin;
            this.movement = movement;

            this.destination = GameObject.Find("WalkAction_Destination").transform;
            destination.position = GetGlobalPosition();
            destination.LookAt(destination.position, agent.transform.up);
        }

        protected internal override void Setup()
        {
            //if(IsPointNear(destination.position, locomotionSystem.transform.position, ))

            if (!locomotionSystem.CanReach(destination.position))
            {
                Vector3 reachPosition;
                if (locomotionSystem.CanReachNearPoint(destination.position, 5f,
                        out reachPosition)) //TODO: Distanza di check arbitraria!!!
                {
                    reachableDestination = origin.InverseTransformPoint(reachPosition);
                    SetState(ActionState.Stopped); //TODO: al momento la considero Stopped e non Failed!!!
                    SetLog($"Destination unreachable but {reachableDestination} is the nearest reachablePosition");
                }
                else
                {
                    SetState(ActionState.Failed);
                    SetLog($"Destination totally unreachable");
                }
            }
            else
            {
                SetState(ActionState.Updating);
            }
        }

        protected internal override void OnStart()
        {
            Debug.Log("Move started");

            locomotionSystem.SetDestination(destination);
            locomotionSystem.OnDestinationArrival += OnDestinationArrival;
        }

        protected internal override void OnUpdate()
        {
            //throw new NotImplementedException();
        }

        protected internal override void OnComplete()
        {
            Debug.Log("Move completed");
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

        private Vector3 GetGlobalPosition()
        {
            return origin.position + origin.TransformVector(movement);
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