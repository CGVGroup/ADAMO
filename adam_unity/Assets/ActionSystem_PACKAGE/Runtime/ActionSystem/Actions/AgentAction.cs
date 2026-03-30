using System;
using System.Globalization;
using UnityEngine;

using HumanoidInteraction;
using JetBrains.Annotations;
using UnityEngine.Serialization;

namespace ActionSystem
{
    
    [Serializable]
    public enum ActionState
    {
        Idle,
        Updating,
        Completed,
        Stopped,
        Failed,
    }

    [Serializable]
    public abstract class AgentAction
    {
        // private System.DateTime m_startDateTime = System.DateTime.MinValue;
        // private System.DateTime m_endDateTime = System.DateTime.MinValue;
        private float m_startDateTime = -1f;
        private float m_endDateTime = -1f;
        
        [SerializeField] private ActionState state = ActionState.Idle;
        [SerializeField] private Enum m_completionCode;
        [SerializeField] private Enum m_failCode;
        [SerializeField] private string log = "None";

        public Action<Interaction> OnActionStarted;
        public Action<Interaction> OnActionReached;
        public Action<Interaction> OnActionHolded;
        public Action<Interaction> OnActionCompleted;
        public Action<Interaction> OnActionStopped;
        public Action<Interaction> OnActionFailed;
        
        protected internal abstract void Setup();
        
        protected internal abstract void OnStart();
        protected internal abstract void OnUpdate();
        protected internal abstract void OnComplete();

        protected internal abstract void OnStop();

        protected internal abstract void OnFail();

        protected internal void SetState(ActionState newState) => state = newState;
        public ActionState State => state;

        protected internal void SetFailedState(Enum newFailCode)
        {
            m_failCode = newFailCode;
            state = ActionState.Failed;
        }
        public Enum FailCode => m_failCode;
        
        protected internal void SetCompletionState(Enum newCompletionCode)
        {
            m_completionCode = newCompletionCode;
            state = ActionState.Completed;
        }
        public Enum CompletionCode => m_completionCode;
        
        protected internal void SetLog(string newLog) => log = newLog;
        public string Log => log;
        
        //protected internal void SetStartTime(System.DateTime time) => m_startDateTime = time;
        // public string StartTime
        // {
        //     get
        //     {
        //         if(m_startDateTime == System.DateTime.MinValue)
        //             return "None";
        //         return m_startDateTime.ToString("[MM-dd]_[HH-mm-ss]");
        //     }
        // }
        protected internal void SetStartTime(float newStartDateTime) => m_startDateTime = newStartDateTime;
        public string StartTime => m_startDateTime.ToString(CultureInfo.InvariantCulture);

        //protected internal void SetEndTime(System.DateTime time) => m_endDateTime = time;
        // public string EndTime
        // {
        //     get
        //     {
        //         if(m_endDateTime == System.DateTime.MinValue)
        //             return "None";
        //         else
        //             return m_endDateTime.ToString("[MM-dd]_[HH-mm-ss]");
        //     }
        // }
        protected internal void SetEndTime(float newEndDateTime) => m_endDateTime = newEndDateTime;
        public string EndTime => m_endDateTime.ToString(CultureInfo.InvariantCulture);
    }
}
