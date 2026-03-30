using System;
using System.Collections.Generic;
using ActionSystem;
using UnityEngine;

[Serializable]
public class MoveTurnLookCompositeAction : AgentCompositeAction
{
    public StopLookAction stopLookAction;
    public MoveTurnAction moveTurnAction;
    public StartLookAction startLookAction;
    public CaptureCameraAction captureCameraAction;
    
    //public MoveTurnLookCompositeAction(Agent agent, Vector3 reqMovement, Vector3 lookPointLocal, float waitTimeBetweenActions = 0.5f) : base(waitTimeBetweenActions)
    public MoveTurnLookCompositeAction(Agent agent, Vector3 moveToPoint, Vector3 lookPoint, float waitTimeBetweenActions = 0.5f) : base(waitTimeBetweenActions)
    {
        // Possibile problema che il sistema di riferimento usato per ogni singola sotto azione evolve rispetto alla precedente
        // Ho il dubbio che il VLM ragioni in maniera progressiva o statica
        
        this.stopLookAction = new StopLookAction(agent);
        this.moveTurnAction = new MoveTurnAction(agent, moveToPoint, lookPoint);
        this.startLookAction = new StartLookAction(agent, lookPoint);
        this.captureCameraAction = new CaptureCameraAction(CameraManager.Instance);
        
        this.subActionsQueue.Add(stopLookAction);
        this.subActionsQueue.Add(moveTurnAction);
        this.subActionsQueue.Add(startLookAction);
        this.subActionsQueue.Add(captureCameraAction);
    }
}

