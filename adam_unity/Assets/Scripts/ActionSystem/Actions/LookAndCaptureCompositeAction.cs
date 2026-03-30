using System;
using System.Collections.Generic;
using ActionSystem;
using UnityEngine;

[Serializable]
public class LookAndCaptureCompositeAction : AgentCompositeAction
{
    public StopLookAction stopLookAction;
    public TurnAction turnAction;
    public StartLookAction startLookAction;
    public CaptureCameraAction captureCameraAction;
    
    public LookAndCaptureCompositeAction(Agent agent, Vector3 lookPoint, float waitTimeBetweenActions = 0.5f) : base(waitTimeBetweenActions)
    {
        this.stopLookAction = new StopLookAction(agent);
        this.turnAction = new TurnAction(agent, lookPoint);
        this.startLookAction = new StartLookAction(agent, lookPoint);
        this.captureCameraAction = new CaptureCameraAction(CameraManager.Instance);
        
        this.subActionsQueue.Add(stopLookAction);
        this.subActionsQueue.Add(turnAction);
        this.subActionsQueue.Add(startLookAction);
        this.subActionsQueue.Add(captureCameraAction);
    }
}

