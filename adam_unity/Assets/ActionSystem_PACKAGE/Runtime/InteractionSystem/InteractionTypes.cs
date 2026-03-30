using System;
using UnityEngine;

namespace HumanoidInteraction
{
    /// <summary>
    /// Defines which body part (effector) will be used for the interaction
    /// </summary>
    [Serializable]
    public enum EffectorType
    {
        RightHand,
        LeftHand,
        RightFoot,
        LeftFoot,
        HeadLook
    }

    [Serializable]
    public class InteractionEffector
    {
        private EffectorType type;
        public EffectorType Type => type;

        private Transform targetTransform;
        public Transform TargetTransform => targetTransform;
        private Transform restTransform;
        public Transform RestTransform => restTransform;
        private Transform attachTransform;
        public Transform AttachTransform => attachTransform;
        
        // To store the position in which the interaction has been stopped
        public Vector3 stoppedPosition = Vector3.zero; 
        
        public Interaction currentInteraction;
        
        public InteractionState CurrentState
        {
            get
            {
                if(currentInteraction != null)
                    return currentInteraction.currentState;
                else
                    return InteractionState.Idle;
            }
        }
        
        public InteractionEffector(EffectorType type, Transform targetTransform, Transform restTransform, Transform attachTransform)
        {
            this.type = type;
            this.targetTransform = targetTransform;
            this.restTransform = restTransform;
            this.attachTransform = attachTransform;
            
            currentInteraction = null;
        }

        public bool IsInteracting()
        {
            return CurrentState != InteractionState.Idle;
        }
    }
    
    /// <summary>
    /// Defines the type of interaction to perform
    /// </summary>
    [Serializable]
    public enum InteractionType
    {
        Touch,      // Simple touch interaction
        Pick,       // Pick up and carry
        Use,        // Use/activate an object
        Push,       // Push an object
        Pull        // Pull an object
    }

    /// <summary>
    /// Represents a complete interaction command
    /// </summary>
    [Serializable]
    public class Interaction
    {
        public Coroutine currentCoroutine;
        public InteractionState currentState;
        
        [Header("Target")]
        public IInteractable target;
        public InteractionType interactionType;
        public EffectorType effectorType;

        [Header("Timing")]
        public float reachDuration = 1.0f;
        public float holdDuration = 0.1f;
        public float returnDuration = 1.0f;

        [Header("Animation")]
        public AnimationCurve reachCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Events")]
        public Action<Interaction> OnInteractionStarted;
        public Action<Interaction> OnInteractionReached;
        public Action<Interaction> OnInteractionHolded;
        public Action<Interaction> OnInteractionCompleted;
        public Action<Interaction> OnInteractionStopped;
        public Action<Interaction> OnInteractionFailed;
        
        [Header("Advanced")]
        public bool useLookAt = true;

        public Interaction(IInteractable target, InteractionType type, EffectorType effectorType)
        {
            this.target = target;
            this.interactionType = type;
            this.effectorType = effectorType;
        }
    }

    /// <summary>
    /// Represents the current state of an interaction
    /// </summary>
    public enum InteractionState
    {
        Idle,
        Reaching,
        Holding,
        Returning,
    }
} 