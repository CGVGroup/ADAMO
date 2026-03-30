using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionSystem
{
    [Serializable]
    public class AgentCompositeAction : AgentAction
    {
        [SerializeField] protected AgentAction currentSubAction;
        [SerializeField] protected List<AgentAction> pastSubActions = new List<AgentAction>();
        [SerializeField] protected List<AgentAction> subActionsQueue = new List<AgentAction>();

        private float waitTime;
        private float elapsedTime;
        
        public AgentCompositeAction(float waitTimeBetweenActions = 0.5f)
        {
            //subActionsQueue = subActions;
            this.waitTime = waitTimeBetweenActions;
            this.elapsedTime = waitTime; // To not wait on the first SubAction
        }
        
        public AgentCompositeAction(List<AgentAction> subActions, float waitTimeBetweenActions = 0.5f)
        {
            subActionsQueue = subActions;
            
            this.waitTime = waitTimeBetweenActions;
            this.elapsedTime = waitTime; // To not wait on the first SubAction
        }
        
        protected internal override void Setup()
        {
            if (! (subActionsQueue.Count > 0))
            {
                Debug.LogError($"{this} has no sub actions!");
                SetState(ActionState.Failed);
            }
            
            SetState(ActionState.Updating);
        }

        protected internal override void OnStart()
        {
            LoadNextSubAction();
        }

        protected internal override void OnUpdate()
        {
            if (elapsedTime <= waitTime)
            {
                elapsedTime += Time.deltaTime;
                return;
            }

            switch (currentSubAction.State)
            {
                case ActionState.Idle:
                    SetState(ActionState.Updating);
                    UpdateCurrentSubAction(); //Make the first update of the subaction
                    break;
                case ActionState.Updating:
                    UpdateCurrentSubAction(); //Continue updating the action
                    break;
                case ActionState.Completed:
                case ActionState.Stopped:
                    UpdateCurrentSubAction(); // To make it call the subaction OnComplete or OnStop
                    if (subActionsQueue.Count == 0)
                        SetState(ActionState.Completed); //If sub action is completed and have no other sub actions
                    else
                        LoadNextSubAction(); //If sub action is completed and have other sub actions

                    break;
                case ActionState.Failed:
                    UpdateCurrentSubAction(); // To make it call the subaction OnFail
                    SetFailedState(currentSubAction.FailCode); // SEQUENTIAL LOGIC: if a subaction fails the CompositeAction fails
                    break;
                default:
                    Debug.LogError($"ActionState={currentSubAction.State} not implemented yet for subActions!");
                    break;
            }
        }

        protected internal override void OnComplete()
        {
            //
        }
        
        protected internal override void OnFail()
        {
            //
        }

        protected internal override void OnStop()
        {
            throw new NotImplementedException();
        }

        private bool LoadNextSubAction()
        {
            if(currentSubAction != null)
                pastSubActions.Add(currentSubAction);
            
            if (subActionsQueue.Count == 0)
                return false;
            else{
                currentSubAction = subActionsQueue[0];
                subActionsQueue.RemoveAt(0);
                
                elapsedTime = 0f;
                return true;
            }
        }

        private void UpdateCurrentSubAction()
        {
            switch (currentSubAction?.State)
            {
                case ActionState.Idle:
                    currentSubAction.Setup();
                    if (currentSubAction.State == ActionState.Updating)
                    {
                        currentSubAction.SetStartTime(Time.fixedTime * Time.timeScale);
                        currentSubAction.OnStart();
                    }
                    break;
                case ActionState.Updating:
                    currentSubAction.OnUpdate();
                    break;
                case ActionState.Completed:
                    if(currentSubAction.StartTime!="-1")
                        currentSubAction.SetEndTime(Time.fixedTime * Time.timeScale);
                    currentSubAction.OnComplete();
                    break;
                case ActionState.Stopped:
                    if(currentSubAction.StartTime!="-1")
                        currentSubAction.SetEndTime(Time.fixedTime * Time.timeScale);
                    currentSubAction.OnStop();
                    break;
                case ActionState.Failed:
                    if(currentSubAction.StartTime!="-1")
                        currentSubAction.SetEndTime(Time.fixedTime * Time.timeScale);
                    currentSubAction.OnFail();
                    break;
            }
        }

        public void StopCurrentSubAction()
        {
            currentSubAction.SetState(ActionState.Stopped);
        }

        public List<AgentAction> GetAllSubActions()
        {
            List<AgentAction> allSubActions = new List<AgentAction>();

            foreach (AgentAction action in pastSubActions)
                if (! allSubActions.Contains(action))
                    allSubActions.Add(action); 
            
            if (! allSubActions.Contains(currentSubAction))
                allSubActions.Add(currentSubAction);
                
            foreach (AgentAction action in subActionsQueue)
                if (! allSubActions.Contains(action))
                    allSubActions.Add(action);
                
            return allSubActions;
        }
        
        // public bool EnqueueSubAction(AgentAction action)
        // {
        //     if (action != null)
        //     {
        //         subActionsQueue.Add(action);
        //         return true;
        //     }
        //     else
        //         return false;
        // }
    }
}
