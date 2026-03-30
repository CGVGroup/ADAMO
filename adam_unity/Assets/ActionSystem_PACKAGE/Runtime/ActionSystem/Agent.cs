using System;
using System.Collections.Generic;
using HumanoidInteraction;
using UnityEngine;

namespace ActionSystem
{
    [RequireComponent(typeof(InteractionSystem))]
    public class Agent : MonoBehaviour
    {
        [SerializeField] protected AgentAction currentAction;
        [SerializeField] protected List<AgentAction> actionsQueue = new List<AgentAction>();
        [SerializeField] protected List<AgentAction> pastActions = new List<AgentAction>();
        
        [SerializeField] protected InteractionSystem interactionSystem;
        [SerializeField] protected LocomotionSystem locomotionSystem;
        
        public InteractionSystem InteractionSystem => interactionSystem;
        public LocomotionSystem LocomotionSystem => locomotionSystem;

        protected void Start()
        {
            this.interactionSystem = this.GetComponent<InteractionSystem>();
            this.locomotionSystem = this.GetComponent<LocomotionSystem>();
        }
        
        protected void Update()
        {
            if (currentAction == null && actionsQueue.Count == 0)
                return;

            if (currentAction != null)
                UpdateCurrentAction();
            else
                LoadNextAction();
        }

        public bool EnqueueAction(AgentAction action)
        {
            if (action != null)
            {
                actionsQueue.Add(action);
                return true;
            }
            else
                return false;
        }

        private bool LoadNextAction()
        {
            if(currentAction != null)
                pastActions.Add(currentAction);
            
            currentAction = null;
            
            if (actionsQueue.Count == 0)
                return false;
            else{
                currentAction = actionsQueue[0];
                actionsQueue.RemoveAt(0);
                return true;
            }
        }

        protected void UpdateCurrentAction()
        {
            switch (currentAction?.State)
            {
                case ActionState.Idle:
                    currentAction.Setup();
                    if (currentAction.State == ActionState.Updating)
                    {
                        //currentAction.SetStartTime(DateTime.Now);
                        currentAction.SetStartTime(Time.fixedTime * Time.timeScale);
                        currentAction.OnStart();
                    }
                    break;
                case ActionState.Updating:
                    currentAction.OnUpdate();
                    break;
                case ActionState.Completed:
                    //currentAction.SetEndTime(DateTime.Now);
                    currentAction.SetEndTime(Time.fixedTime * Time.timeScale);
                    currentAction.OnComplete();
                    LoadNextAction();
                    break;
                case ActionState.Stopped:
                    //currentAction.SetEndTime(DateTime.Now);
                    currentAction.SetEndTime(Time.fixedTime * Time.timeScale);
                    currentAction.OnStop();
                    LoadNextAction();
                    break;
                case ActionState.Failed:
                    //currentAction.SetEndTime(DateTime.Now);
                    currentAction.SetEndTime(Time.fixedTime * Time.timeScale);
                    currentAction.OnFail();
                    LoadNextAction();
                    break;
            }
        }

        public void StopCurrentAction()
        {
            currentAction.SetState(ActionState.Stopped);
        }
    }
}