using HumanoidInteraction;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Assertions;

namespace ActionSystem
{
    public class StartLookAction : AgentAction
    {
        [SerializeField] private InteractionSystem interactionSystem;

        [SerializeField] private MultiAimConstraint aimConstraint;
        [SerializeField] private Vector3 lookPosition;

        //[SerializeField] private Transform origin; 
        [SerializeField] private Transform lookTransform;

        //public StartLookAction(Agent agent, Transform origin, Vector3 lookPosition)
        public StartLookAction(Agent agent, Vector3 lookPosition)
        {
            Assert.IsNotNull(agent.InteractionSystem);
            //Assert.IsNotNull(origin);

            //this.origin = origin;
            this.interactionSystem = agent.InteractionSystem;
            this.lookPosition = lookPosition;
        }

        protected internal override void Setup()
        {
            SetState(ActionState.Updating);
        }

        protected internal override void OnStart()
        {
            lookTransform = GameObject.Find("LookAction_Transform").transform;
            
            //lookTransform.position = origin.TransformPoint(lookPosition);
            lookTransform.position = lookPosition;
            
            interactionSystem.StartLook(lookTransform);
        }

        protected internal override void OnUpdate()
        {
            if (interactionSystem.IsEffectorTotallyActive(EffectorType.HeadLook))
                SetState(ActionState.Completed);
        }

        protected internal override void OnComplete()
        {
            //Debug.Log("Pick completed");
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
