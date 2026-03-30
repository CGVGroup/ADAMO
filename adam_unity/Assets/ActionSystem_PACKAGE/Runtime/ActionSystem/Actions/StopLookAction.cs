using HumanoidInteraction;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Assertions;

namespace ActionSystem
{
    public class StopLookAction : AgentAction
    {
        [SerializeField] private InteractionSystem interactionSystem;

        public StopLookAction(Agent agent)
        {
            Assert.IsNotNull(agent.InteractionSystem);
            
            this.interactionSystem = agent.InteractionSystem;
        }

        protected internal override void Setup()
        {
            SetState(ActionState.Updating);
        }

        protected internal override void OnStart()
        {
            interactionSystem.StopLook();
        }

        protected internal override void OnUpdate()
        {
            if (interactionSystem.IsEffectorTotallyInactive(EffectorType.HeadLook))
                SetState(ActionState.Completed);
        }

        protected internal override void OnComplete()
        {
            //Debug.Log("DisableLookAction completed");
        }

        protected internal override void OnStop()
        {
            throw new System.NotImplementedException();
        }

        protected internal override void OnFail()
        {
            throw new System.NotImplementedException();
        }
    }
}
