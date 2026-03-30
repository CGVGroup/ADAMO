using System;
using HumanoidInteraction;
using UnityEngine;
using UnityEngine.Assertions;

namespace ActionSystem
{
    [Serializable]
    public enum DropActionFailCode
    {
        // For tool calls
        PosNotReachable,
        ObjNotFound,
        ObjNotHeld,
        
        // Others internal
        ObjNotDroppable,
        EffectorIsAlreadyActive,
    }
    
    [Serializable]
    public class DropAction : AgentAction
    {
        [SerializeField] private EffectorType effectorType;
        [SerializeField] private Pickable pickableObj;
        [SerializeField] private Transform dropTransform;
        public Transform DropTransform => dropTransform;

        [SerializeField] private InteractionSystem interactionSystem;

        [SerializeField] private Interaction interaction;

        [SerializeField] private Vector3 stoppedPosition = Vector3.zero;
        public Vector3 StoppedPosition => stoppedPosition;

        public DropAction(Agent agent, Pickable pickableObj, Transform dropTransform, EffectorType effectorType)
        {
            Assert.IsNotNull(agent.InteractionSystem);
            
            // I do not check if pickableObj != null here because i check it in Setup() to set the Action FailCode accordingly 
            
            Assert.IsNotNull(dropTransform);

            this.interactionSystem = agent.InteractionSystem;
            this.pickableObj = pickableObj;
            this.dropTransform = dropTransform;
            this.effectorType = effectorType;
        }

        protected internal void PreStartCheck() //TODO: generalize for all actions
        {
            if (pickableObj == null)
            {
                SetLog($"Pickable object is null!");
                SetFailedState(DropActionFailCode.ObjNotFound);
                return;
            }
            
            if (!pickableObj.IsBeingCarried)
            {
                SetLog("Pickable object is not being carried right now");
                SetFailedState(DropActionFailCode.ObjNotHeld);
                return;
            }

            if (! interactionSystem.IsEffectorTotallyInactive(effectorType))
            {
                SetLog("Effector is not totally inactive!");
                SetFailedState(DropActionFailCode.EffectorIsAlreadyActive);
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

            interaction = interactionSystem.StartReachInteraction(dropTransform, effectorType);

            interaction.OnInteractionStarted += OnInteractionStarted;
            interaction.OnInteractionHolded += OnInteractionHolded;
            interaction.OnInteractionCompleted += OnInteractionCompleted;
            interaction.OnInteractionFailed += OnInteractionFailed;
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
                SetLog($"Drop failed, maximum extension is at {stoppedPosition}");
                SetFailedState(DropActionFailCode.PosNotReachable);
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

        private void OnInteractionHolded(Interaction interaction)
        {
            interaction.OnInteractionHolded -= OnInteractionHolded;
            
            interactionSystem.RiggingController.OnPostUpdate += UnCarry;
            
            //TODO: probably better to move UnCarry logic to DropCompleted?
            //TODO: Or Maybe not, because of OnComplete is called when the effector is resting
        }

        private void UnCarry()
        {
            pickableObj.transform.SetParent(null, true);
            pickableObj.OnDrop();
            pickableObj.transform.position = dropTransform.position;
        }

        private void OnInteractionCompleted(Interaction interaction)
        {
            interactionSystem.RiggingController.OnPostUpdate -= UnCarry;
            
            interaction.OnInteractionCompleted -= OnInteractionCompleted;
            interaction.OnInteractionHolded -= OnInteractionHolded;
            interaction.OnInteractionFailed -= OnInteractionFailed;

            this.SetState(ActionState.Completed);
        }

        private void OnInteractionFailed(Interaction interaction)
        {
            interaction.OnInteractionCompleted -= OnInteractionCompleted;
            interaction.OnInteractionHolded -= OnInteractionHolded;
            interaction.OnInteractionFailed -= OnInteractionFailed;
        }
    }
}