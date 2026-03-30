using System;
using System.Collections.Generic;
using ActionSystem;
using UnityEngine;

// Generalizzabile mettendo un ciclo for su CaptureCameraAction per costruire una CompositeAction con X LookAction e CaptureCameraAction

[Serializable]
public class MultipleLookAtAndCaptureCompositeAction : AgentCompositeAction
{
    public StopLookAction stopLookAction;
    public StartLookAction startLookAction;
    public CaptureCameraAction cameraAction1, cameraAction2, cameraAction3;
    
    public MultipleLookAtAndCaptureCompositeAction(Agent agent, Vector3 lookPoint, float waitTimeBetweenActions = 0.5f) : base(waitTimeBetweenActions)
    {
        // First screenshot
        this.stopLookAction = new StopLookAction(agent);
        this.startLookAction = new StartLookAction(agent, lookPoint);
        this.cameraAction1 = new CaptureCameraAction(CameraManager.Instance);
        
        this.subActionsQueue.Add(stopLookAction);
        this.subActionsQueue.Add(startLookAction);
        this.subActionsQueue.Add(cameraAction1);
        
        
        // Second screenShot
        this.stopLookAction = new StopLookAction(agent);
        this.startLookAction = new StartLookAction(agent, lookPoint);
        this.cameraAction2 = new CaptureCameraAction(CameraManager.Instance);
        
        this.subActionsQueue.Add(stopLookAction);
        this.subActionsQueue.Add(startLookAction);
        this.subActionsQueue.Add(cameraAction2);
        
        
        // Third screenShot
        this.stopLookAction = new StopLookAction(agent);
        this.startLookAction = new StartLookAction(agent, lookPoint);
        this.cameraAction3 = new CaptureCameraAction(CameraManager.Instance);
        
        this.subActionsQueue.Add(stopLookAction);
        this.subActionsQueue.Add(startLookAction);
        this.subActionsQueue.Add(cameraAction3);
    }
}