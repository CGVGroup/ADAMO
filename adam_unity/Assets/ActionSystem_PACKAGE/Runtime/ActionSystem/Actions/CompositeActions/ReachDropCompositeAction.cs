using System;
using HumanoidInteraction;
using UnityEngine;

namespace ActionSystem
{
    [Serializable]
    public enum ReachDropCompletionCode
    {
        WithoutMoving,
        WithMoving,
    }
    
    [Serializable]
    public enum ReachDropFailCode
    {
        PosNotReachable,
        ObjNotFound,
        ObjNotHeld,
        
        LocationNotReachable,
    }
    
    [Serializable]
    public class ReachDropCompositeAction : AgentCompositeAction
    {
        private Agent agent;
        private Transform dropTarget;
        
        public WalkAction walkAction;
        public TurnAction turnAction;
        public DropAction dropAction;
    
        private ReachDropCompletionCode m_possibleCompletionCode;
        
        public ReachDropCompositeAction(Agent agent, Pickable pickableObj, Transform dropTarget, float waitTimeBetweenActions = 0.5f) : base(waitTimeBetweenActions)
        {
            this.agent = agent;
            this.dropTarget = dropTarget;
            
            this.dropAction = new DropAction(agent, pickableObj, dropTarget, EffectorType.RightHand); //TODO: generalize

            dropAction.PreStartCheck();
            if (dropAction.FailCode != null)
            {
                switch (dropAction.FailCode)
                {
                    case DropActionFailCode.ObjNotFound:
                        SetFailedState(ReachDropFailCode.ObjNotFound);
                        break;
                    case DropActionFailCode.ObjNotHeld:
                        SetFailedState(ReachDropFailCode.ObjNotHeld);
                        break;
                    default:
                        Debug.LogError(
                            $"DropAction.FailCode={dropAction.FailCode} is not supported when ReachDropCompositeAction.State={this.State} ");
                        break;
                }
                return;
            }

            if (!agent.InteractionSystem.IsTargetInHandsRange(dropTarget.position))
            {
                m_possibleCompletionCode = ReachDropCompletionCode.WithMoving;
                this.walkAction = new WalkAction(agent, dropTarget.position);
                this.subActionsQueue.Add(walkAction);
                this.turnAction = new TurnAction(agent, dropTarget.position);
                this.subActionsQueue.Add(turnAction);
            }
            else
            {
                m_possibleCompletionCode = ReachDropCompletionCode.WithoutMoving;
            }
            
            this.subActionsQueue.Add(dropAction);
        }

        protected internal override void OnUpdate()
        {
            base.OnUpdate();
            
            if (currentSubAction == walkAction)
                if (agent.InteractionSystem.IsTargetInHandsRange(dropTarget.position))
                    StopCurrentSubAction();
        }

        protected internal override void OnComplete()
        {
            base.OnComplete();

            switch (dropAction.State)
            {
                case ActionState.Completed:
                    SetCompletionState(m_possibleCompletionCode);
                    break;
                default:
                    Debug.LogError($"DropAction.State={dropAction.State} is not supported when ReachDropCompositeAction.State={this.State} ");
                    break;
            }
        }

        protected internal override void OnFail()
        {
            base.OnFail();
            
            if(walkAction == null)
                return;
            
            switch (walkAction.State)
            {
                case ActionState.Failed:
                    SetFailedState(ReachDropFailCode.LocationNotReachable);
                    break;
                case ActionState.Idle:
                    // Drop must have failed
                case ActionState.Completed:
                    // Drop must have failed
                    switch (dropAction.State)
                    {
                        case ActionState.Failed:
                            switch (dropAction.FailCode)
                            {
                                case DropActionFailCode.PosNotReachable:
                                    SetFailedState(ReachDropFailCode.PosNotReachable);
                                    break;
                                default:
                                    Debug.LogError($"DropAction.FailCode={dropAction.FailCode} is not supported when ReachDropCompositeAction.State={this.State} ");
                                    break;
                            }
                            break;
                        default:
                            Debug.LogError($"DropAction.State={dropAction.State} is not supported when ReachDropCompositeAction.State={this.State} ");
                            break;
                    }

                    break;
                default:
                    Debug.LogError($"WalkAction.State={walkAction.State} is not supported when ReachDropCompositeAction.State={this.State} ");
                    break;
            }
        }
    }
}