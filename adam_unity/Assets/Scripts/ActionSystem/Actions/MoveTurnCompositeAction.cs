using System;
using System.Collections.Generic;
using ActionSystem;
using UnityEngine;

[Serializable]
public class MoveTurnCompositeAction : AgentCompositeAction
{
    public StopLookAction stopLookAction;
    public MoveTurnAction moveTurnAction;
    
    //public MoveTurnLookCompositeAction(Agent agent, Vector3 reqMovement, Vector3 lookPointLocal, float waitTimeBetweenActions = 0.5f) : base(waitTimeBetweenActions)
    public MoveTurnCompositeAction(Agent agent, Vector3 moveToPoint, Vector3 lookPoint, float waitTimeBetweenActions = 0.5f) : base(waitTimeBetweenActions)
    {
        this.stopLookAction = new StopLookAction(agent);
        this.moveTurnAction = new MoveTurnAction(agent, moveToPoint, lookPoint);
        
        this.subActionsQueue.Add(stopLookAction);
        this.subActionsQueue.Add(moveTurnAction);
    }
}

