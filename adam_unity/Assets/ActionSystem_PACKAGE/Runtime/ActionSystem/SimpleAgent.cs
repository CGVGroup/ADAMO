using System.Collections.Generic;
using HumanoidInteraction;
using UnityEngine;

namespace ActionSystem
{
    public class SimpleAgent : Agent
    {
        [SerializeField] protected bool enableDebugLogging;

        public TouchAction Touch(Interactable target, EffectorType effectorType)
        {
            if (enableDebugLogging)
                Debug.Log($"Adding TouchAction with {target.Desc}");

            TouchAction action = new TouchAction(this, effectorType, target);
            this.EnqueueAction(action);

            return action;
        }

        public ReachPickCompositeAction Pick(Pickable target, EffectorType effectorType)
        {
            if (enableDebugLogging)
                Debug.Log($"Adding PickAction with {target.Desc}");

            ReachPickCompositeAction action = new ReachPickCompositeAction(this, target); //TODO: generalize 
            this.EnqueueAction(action);

            return action;
        }

        public ReachDropCompositeAction Drop(Pickable pickableObj, Transform dropTransform, EffectorType effectorType)
        {
            if (enableDebugLogging)
                Debug.Log($"Adding DropAction with {pickableObj.Desc}");

            ReachDropCompositeAction action = new ReachDropCompositeAction(this, pickableObj, dropTransform); //TODO: generalize
            this.EnqueueAction(action);

            return action;
        }

        public AgentAction Walk(Vector3 destination)
        {
            if (enableDebugLogging)
                Debug.Log($"Adding WalkAction to {destination}");

            AgentAction action = new WalkAction(this, destination);
            this.EnqueueAction(action);

            return action;
        }
        
        public TurnAction Turn(Vector3 turnPoint)
        {
            if (enableDebugLogging)
                Debug.Log($"Adding TurnAction to {turnPoint}");

            TurnAction action = new TurnAction(this, turnPoint);
            this.EnqueueAction(action);

            return action;
        }

        public MoveAction Move(Transform cameraPov, Vector3 movement)
        {
            if (enableDebugLogging)
                Debug.Log($"Adding MoveAction to move of {movement}");

            MoveAction action = new MoveAction(this, cameraPov, movement);
            this.EnqueueAction(action);

            return action;
        }

        public MoveTurnAction MoveTurn(Transform cameraPov, Vector3 movement, Vector3 turnToPoint)
        {
            if (enableDebugLogging)
                Debug.Log($"Adding MoveTurn-Action to move of {movement} look at {turnToPoint}");

            MoveTurnAction action = new MoveTurnAction(this, movement, turnToPoint);
            this.EnqueueAction(action);

            return action;
        }

        public AgentCompositeAction MoveTurnAndTouch(Transform cameraPov, Vector3 movement, Vector3 turnToPoint,
            Interactable target, EffectorType effectorType)
        {
            if (enableDebugLogging)
                Debug.Log($"Adding MoveTurnAndTouch-CompositeAction to move of {movement} look at {turnToPoint} and touch {target}");

            List<AgentAction> subActions = new List<AgentAction>();
            subActions.Add(new MoveTurnAction(this, movement, turnToPoint));
            subActions.Add(new TouchAction(this, effectorType, target));
            AgentCompositeAction compositeAction = new AgentCompositeAction(subActions);
            
            this.EnqueueAction(compositeAction);

            return compositeAction;
        }
        
        public StartLookAction Look(Transform cameraPov, Vector3 lookPoint)
        {
            if (enableDebugLogging)
                Debug.Log($"Adding LookAction to look at {lookPoint}");

            StartLookAction action = new StartLookAction(this, lookPoint);
            this.EnqueueAction(action);

            return action;
        }
        
        public StopLookAction StopLook()
        {
            if (enableDebugLogging)
                Debug.Log($"Adding StopLookAction");

            StopLookAction action = new StopLookAction(this);
            this.EnqueueAction(action);

            return action;
        }
    }
}