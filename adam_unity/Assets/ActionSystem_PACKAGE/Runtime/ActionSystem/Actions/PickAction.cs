using System;
using HumanoidInteraction;
using UnityEngine;
using UnityEngine.Assertions;

namespace ActionSystem
{
    
    [Serializable]
    public enum PickActionFailCode
    {
        // For tool calls
        ObjNotReachable,
        ObjNotFound,
        ObjAlreadyHeld,
        
        // Others internal
        ObjNotInteractable,
        EffectorIsAlreadyActive,
    }
    
    [Serializable]
    public class PickAction : AgentAction
    {
        [SerializeField] private EffectorType effectorType;
        [SerializeField] private Pickable pickableObj;

        [SerializeField] private InteractionSystem interactionSystem;

        [SerializeField] private Interaction interaction;

        [SerializeField] private Vector3 stoppedPosition = Vector3.zero;
        public Vector3 StoppedPosition => stoppedPosition;
        public Transform ObjectTransform => pickableObj?.transform;

        public PickAction(ActionState state)
        {
            // Just for returning HTTP response purposes
            SetState(state);
        }
        
        public PickAction(Agent agent, EffectorType effectorType, Pickable pickableObj)
        {
            Assert.IsNotNull(agent.InteractionSystem);
            
            // I do not check if pickableObj != null here because i check it in Setup() to set the Action FailCode accordingly 

            this.interactionSystem = agent.InteractionSystem;
            this.effectorType = effectorType;
            this.pickableObj = pickableObj;
        }

        protected internal void PreStartCheck() //TODO: generalize for all actions
        {
            if (pickableObj == null)
            {
                SetLog($"Pickable object is null!");
                SetFailedState(PickActionFailCode.ObjNotFound);
                return;
            }
            
            if (!pickableObj.CanInteract)
            {
                SetLog("Not interactable at the moment!");
                SetFailedState(PickActionFailCode.ObjNotInteractable);
                return;
            }

            if (! interactionSystem.IsEffectorTotallyInactive(effectorType))
            {
                SetLog("Effector is not totally inactive!");
                SetFailedState(PickActionFailCode.EffectorIsAlreadyActive);
                return;
            }
        }
        
        protected internal override void Setup()
        {
            PreStartCheck();

            SetState(ActionState.Updating);
        }

        protected internal override void OnStart()
        {
            //Debug.Log("Pick started");

            if (!pickableObj.IsBeingCarried)
            {
                interaction = interactionSystem.StartPickInteraction(pickableObj, effectorType);

                interaction.OnInteractionStarted += OnInteractionStarted;
                interaction.OnInteractionCompleted += OnInteractionCompleted;
                interaction.OnInteractionFailed += OnInteractionFailed;
            }
            else
            {
                SetLog($"{pickableObj} is already being carried");
                SetFailedState(PickActionFailCode.ObjAlreadyHeld);
            }
        }

        protected internal override void OnUpdate()
        {
            if (!interactionSystem.IsEffectorTotallyActive(effectorType))
                return;

            //EffectorRig is totally blended in, so i can check if the effector is actually able to reach the target
            if (interactionSystem.IsConstraintTipAwayFromTarget(effectorType))
            {
                stoppedPosition = interactionSystem.GetEffectorStoppedTransform(effectorType).position;
                interactionSystem.StopInteraction(interaction);
                SetFailedState(PickActionFailCode.ObjNotReachable);
            }
        }

        protected internal override void OnComplete()
        {
            //
        }

        protected internal override void OnStop()
        {
            throw new NotImplementedException();
        }

        protected internal override void OnFail()
        {
            //
        }

        private void OnInteractionStarted(Interaction interaction)
        {
            interaction.OnInteractionStarted -= OnInteractionStarted;
        }

        private void OnInteractionCompleted(Interaction interaction)
        {
            interaction.OnInteractionCompleted -= OnInteractionCompleted;
            interaction.OnInteractionFailed -= OnInteractionFailed;
            this.SetState(ActionState.Completed);
        }

        private void OnInteractionFailed(Interaction interaction)
        {
            interaction.OnInteractionFailed -= OnInteractionFailed;
        }
    }
}