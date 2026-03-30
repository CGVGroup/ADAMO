using System;
using HumanoidInteraction;
using UnityEngine;

namespace ActionSystem
{
    [Serializable]
    public enum ReachPickCompletionCode
    {
        WithoutMoving,
        WithMoving,
    }

    [Serializable]
    public enum ReachPickFailCode
    {
        ObjNotReachable,
        ObjNotFound,
        ObjAlreadyHeld,

        LocationNotReachable,
    }

    [Serializable]
    public class ReachPickCompositeAction : AgentCompositeAction
    {
        private Agent agent;
        private Pickable pickableObj;

        public WalkAction walkAction;
        public TurnAction turnAction;
        public PickAction pickAction;

        private ReachPickCompletionCode m_possibleCompletionCode;

        public ReachPickCompositeAction(Agent agent, Pickable pickableObj, float waitTimeBetweenActions = 0.5f) : base(waitTimeBetweenActions)
        {
            this.agent = agent;
            this.pickableObj = pickableObj;
            
            this.pickAction = new PickAction(agent, EffectorType.RightHand, pickableObj); //TODO: generalize

            pickAction.PreStartCheck();
            if (pickAction.FailCode != null)
            {
                switch (pickAction.FailCode)
                {
                    case PickActionFailCode.ObjNotFound:
                        SetFailedState(ReachPickFailCode.ObjNotFound);
                        break;
                    case PickActionFailCode.ObjAlreadyHeld:
                        SetFailedState(ReachPickFailCode.ObjAlreadyHeld);
                        break;
                    default:
                        Debug.LogError(
                            $"PickAction.FailCode={pickAction.FailCode} is not supported when ReachPickCompositeAction.State={this.State} ");
                        break;
                }
                return;
            }

            if (!agent.InteractionSystem.IsTargetInHandsRange(pickableObj.transform.position))
            {
                m_possibleCompletionCode = ReachPickCompletionCode.WithMoving;
                this.walkAction = new WalkAction(agent, pickableObj.transform.position);
                this.subActionsQueue.Add(walkAction);
                this.turnAction = new TurnAction(agent, pickableObj.transform.position);
                this.subActionsQueue.Add(turnAction);
            }
            else
            {
                m_possibleCompletionCode = ReachPickCompletionCode.WithoutMoving;
            }

            this.subActionsQueue.Add(pickAction);
        }

        protected internal override void OnUpdate()
        {
            base.OnUpdate();

            if (currentSubAction == walkAction)
                if (agent.InteractionSystem.IsTargetInHandsRange(pickableObj.transform.position))
                    StopCurrentSubAction();
        }

        protected internal override void OnComplete()
        {
            base.OnComplete();

            switch (pickAction.State)
            {
                case ActionState.Completed:
                    SetCompletionState(m_possibleCompletionCode);
                    break;
                default:
                    Debug.LogError($"PickAction.State={pickAction.State} is not supported when ReachPickCompositeAction.State={this.State} ");
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
                    SetFailedState(ReachPickFailCode.LocationNotReachable);
                    break;
                case ActionState.Idle:
                    // Pick must have failed
                case ActionState.Completed:
                    // Pick must have failed
                    switch (pickAction.State)
                    {
                        case ActionState.Failed:
                            switch (pickAction.FailCode)
                            {
                                case PickActionFailCode.ObjNotReachable:
                                    SetFailedState(ReachPickFailCode.ObjNotReachable);
                                    break;
                                default:
                                    Debug.LogError($"PickAction.FailCode={pickAction.FailCode} is not supported when ReachPickCompositeAction.State={this.State} ");
                                    break;
                            }
                            break;
                        default:
                            Debug.LogError($"PickAction.State={pickAction.State} is not supported when ReachDropCompositeAction.State={this.State} ");
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