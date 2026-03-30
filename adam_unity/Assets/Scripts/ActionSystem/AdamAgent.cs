using System;
using System.Collections.Generic;
using ActionSystem;
using UnityEngine;
using UnityEngine.Serialization;

public class AdamAgent : SimpleAgent
{
    [SerializeField] CameraManager cameraManager;
    public List<AgentAction> PastActions => pastActions;

    public void AddAction(AgentAction compositeAction)
    {
        this.EnqueueAction(compositeAction);
    }

    public void SetCurrentRunDataActions()
    {
        foreach (AgentAction action in pastActions)
        {
            switch (action.State)
            {
                case ActionState.Completed:
                    BenchmarkManager.Instance.CurrentRun.IncrementCompletedToolCount();
                    continue;
                case ActionState.Failed:
                    BenchmarkManager.Instance.CurrentRun.IncrementFailedToolCount();
                    continue;
                case ActionState.Stopped:
                    BenchmarkManager.Instance.CurrentRun.IncrementStoppedToolCount();
                    continue;
                case ActionState.Idle:
                case ActionState.Updating:
                default:
                    Debug.LogError("This should never happen!");
                    continue;
            }
        }
    }
}
