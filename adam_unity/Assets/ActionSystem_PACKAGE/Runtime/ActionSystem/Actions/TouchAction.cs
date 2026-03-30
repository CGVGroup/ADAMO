using System;
using HumanoidInteraction;
using UnityEngine;
using UnityEngine.Assertions;

namespace ActionSystem
{
    [Serializable]
    public class TouchAction : AgentAction
    {
        [SerializeField] private EffectorType effectorType;
        [SerializeField] private Interactable target;

        [SerializeField] private InteractionSystem interactionSystem;

        [SerializeField] private Interaction interaction;

        [SerializeField] private Vector3 stoppedPosition = Vector3.zero;

        public TouchAction(Agent agent, EffectorType effectorType, Interactable target)
        {
            this.interactionSystem = agent.InteractionSystem;
            this.effectorType = effectorType;
            this.target = target;

            Assert.IsNotNull(this.interactionSystem);
            Assert.IsNotNull(target);
        }

        protected internal override void Setup()
        {
            if (!target.CanInteract)
            {
                SetLog("Not interactable at the moment");
                SetState(ActionState.Failed);
                return;
            }

            if (! interactionSystem.IsEffectorTotallyInactive(effectorType))
            {
                SetLog("Effector is not totally inactive!");
                SetState(ActionState.Failed);
                return;
            }

            SetState(ActionState.Updating);
        }

        protected internal override void OnStart()
        {
            //Debug.Log("Touch started");

            if (target != null && target.CanInteract)
            {
                interaction = interactionSystem.StartSimpleTouchInteraction(target, effectorType);

                interaction.OnInteractionStarted += OnInteractionStarted;
                interaction.OnInteractionCompleted += OnInteractionCompleted;
                interaction.OnInteractionFailed += OnInteractionFailed;
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
                SetState(ActionState.Failed);
            }
        }

        protected internal override void OnComplete()
        {
            //Debug.Log("Touch completed");
        }

        protected internal override void OnStop()
        {
            throw new NotImplementedException();
        }

        protected internal override void OnFail()
        {
            throw new NotImplementedException();
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
            interaction.OnInteractionStarted -= OnInteractionStarted;
            interaction.OnInteractionCompleted -= OnInteractionCompleted;
            interaction.OnInteractionFailed -= OnInteractionFailed;

            this.SetState(ActionState.Failed);
        }
    }
}