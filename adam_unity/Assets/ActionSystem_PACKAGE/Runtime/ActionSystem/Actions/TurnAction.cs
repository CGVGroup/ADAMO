using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace ActionSystem
{
    [Serializable]
    public enum TurnActionFailCode
    {
        //
    }
    
    [Serializable]
    public class TurnAction : AgentAction
    {
        [SerializeField] private Vector3 turnToPoint;
        
        [SerializeField] private LocomotionSystem locomotionSystem;

        //public MoveTurnAction(Agent agent, Transform origin, Vector3 movementLocal, Vector3 turnToPointLocal)
        public TurnAction(Agent agent, Vector3 turnToPoint)
        {
            //Assert.IsNotNull(origin);
            Assert.IsNotNull(agent.LocomotionSystem);

            this.locomotionSystem = agent.LocomotionSystem;
            this.turnToPoint = turnToPoint;
        }

        protected internal override void Setup()
        {
            GameObject.Find("TurnToPoint_Transform").transform.position = turnToPoint;

            SetState(ActionState.Updating);
        }

        protected internal override void OnStart()
        {
            // I tell the locomotion system to actually turn on place
            locomotionSystem.SetTurnToPoint(turnToPoint);
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
    }
}