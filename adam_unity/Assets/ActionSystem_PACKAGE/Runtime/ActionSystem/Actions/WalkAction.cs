using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace ActionSystem
{
    [Serializable]
    public enum WalkActionCompletedCode
    {
        // For tool calls
        RequestedDestinationReached,
        SuggestedDestinationReached,
    }
    
    [Serializable]
    public enum WalkActionFailCode
    {
        // For tool calls
        DestinationNotReachable,
        NewDestinationSuggested
    }
    
    [Serializable]
    public class WalkAction : AgentAction
    {
        // Input Params
        [SerializeField] private Transform reqDestination;
        private Vector3 m_reqPoint;
        
        // Output Params
        [SerializeField] private Transform destination;
        public Vector3 Destination
        {
            get
            {
                if(destination!=null)
                    return destination.position;
                else
                    return Vector3.zero;
            }
        }

        [SerializeField] private Transform suggestedDestination;
        
        [SerializeField] private LocomotionSystem locomotionSystem;

        private WalkActionCompletedCode m_possibleCompletionCode;
        
        public WalkAction(Agent agent, Vector3 reqPoint)
        {
            Assert.IsNotNull(agent.LocomotionSystem);

            this.locomotionSystem = agent.LocomotionSystem;
            this.m_reqPoint = reqPoint;
        }

        protected internal override void Setup()
        {
            this.reqDestination = GameObject.Find("WalkAction_ReqDestination").transform;
            reqDestination.position = m_reqPoint;
            
            this.destination = GameObject.Find("WalkAction_Destination").transform;
            this.suggestedDestination = GameObject.Find("WalkAction_SuggestedDestination").transform;

            if (!locomotionSystem.CanReach(reqDestination.position))
            {
                Vector3 reachPosition;
                if (locomotionSystem.CanReachNearPoint(reqDestination.position, 25f,
                        out reachPosition)) //TODO: Distanza di check arbitraria!!!
                {
                    suggestedDestination.position = reachPosition;
                    //SetFailedState(WalkActionFailCode.NewDestinationSuggested);
                    SetLog($"Destination unreachable but {suggestedDestination.position} is the nearest reachablePosition (going there)");
                    
                    //I still go to the suggested position
                    Debug.LogWarning($"I am going to suggested destination {suggestedDestination.position} instead of {reqDestination.position}");
                    
                    destination.position = suggestedDestination.position;
                    m_possibleCompletionCode = WalkActionCompletedCode.SuggestedDestinationReached;
                    SetState(ActionState.Updating);
                }
                else
                {
                    SetFailedState(WalkActionFailCode.DestinationNotReachable);
                    SetLog($"Destination totally unreachable");
                }
            }
            else
            {
                destination.position = reqDestination.position;
                m_possibleCompletionCode = WalkActionCompletedCode.RequestedDestinationReached;
                SetState(ActionState.Updating);
            }
        }

        protected internal override void OnStart()
        {
            locomotionSystem.SetDestination(destination.position);
            locomotionSystem.OnDestinationArrival += OnDestinationArrival;
        }

        protected internal override void OnUpdate()
        {
            //throw new NotImplementedException();
        }

        protected internal override void OnComplete()
        {
            //Debug.Log($"{this} :: OnComplete()");
        }

        protected internal override void OnStop()
        {
            //Debug.Log($"{this} :: OnStop()");
            
            locomotionSystem.OnDestinationArrival -= OnDestinationArrival;
            //I set destination to current location
            destination.position = locomotionSystem.transform.position;
            locomotionSystem.SetDestination(destination.position);
            
            //TODO: refactor this, was made for easiness of use with ADAMO system
            //TODO: OnStop should not change the state of the action to completed
            SetState(ActionState.Completed);
        }

        protected internal override void OnFail()
        {
            //
        }

        private void OnDestinationArrival()
        {
            locomotionSystem.OnDestinationArrival -= OnDestinationArrival;
            SetCompletionState(m_possibleCompletionCode);
        }
    }
}