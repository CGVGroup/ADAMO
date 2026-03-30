using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections;
using System.Collections.Generic;
using RootMotion.FinalIK;
using UnityEngine.Assertions;
using UnityEngine.Playables;

namespace HumanoidInteraction
{
    /// <summary>
    /// Manages Animation Rigging components and provides effector control
    /// </summary>
    [RequireComponent(typeof(InteractionSystem))]
    public class AnimationRiggingController : MonoBehaviour
    {
        [Header("Rigging Components")]
        // [SerializeField] private RigBuilder rigBuilder;
        // [SerializeField] private Rig rightArmRig;
        // [SerializeField] private Rig leftArmRig;
        // [SerializeField] private Rig rightLegRig;
        // [SerializeField] private Rig leftLegRig;
        // [SerializeField] private Rig headRig;
        // [SerializeField] private Rig spineRig;
        [SerializeField] private FullBodyBipedIK fullBodyIk;
        [SerializeField] private LookAtIK lookAtIK;

        public Action OnPostUpdate;

        [Header("IK Targets")]
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightFootTarget;
        [SerializeField] private Transform leftFootTarget;
        [SerializeField] private Transform lookAtTarget;

        [Header("Rest Positions")]
        [SerializeField] private Transform rightHandRest;
        [SerializeField] private Transform leftHandRest;
        [SerializeField] private Transform rightFootRest;
        [SerializeField] private Transform leftFootRest;
        [SerializeField] private Transform lookAtRest;
        
        [Header("Stopped Positions")]
        [SerializeField] private Transform rightHandStoppedPos;
        [SerializeField] private Transform leftHandStoppedPos;
        // [SerializeField] private Transform rightFootStoppedPos;
        // [SerializeField] private Transform leftFootStoppedPos;
        
        [Header("Effector Attachments")]
        [SerializeField] private Transform rightHandAttach;
        [SerializeField] private Transform leftHandAttach;
        /*[SerializeField] private Transform rightFootAttach;
        [SerializeField] private Transform leftFootAttach;*/

        /*[Header("Hand Bones")]
        [SerializeField] private Transform rightHandBone;
        [SerializeField] private Transform leftHandBone;*/

        [Header("Animation Settings")]
        [SerializeField] private float blendSpeed = 2.0f;
        /*[SerializeField] private float moveSpeed = 3.0f;
        [SerializeField] private float lookSpeed = 5.0f;*/

        private InteractionEffector rightHandEffector;
        private InteractionEffector leftHandEffector;
        private InteractionEffector rightFootEffector;
        private InteractionEffector leftFootEffector;
        private InteractionEffector lookEffector;
        
        private Dictionary<InteractionEffector, Rig> rigMap;

        private InteractionSystem interactionSystem;
        private LocomotionSystem locomotionSystem;
        
        private void Awake()
        {
            // InitializeEffectorMaps();
            //SetAllRigWeights(0f);
        }

        private void Start()
        {
            Assert.IsNotNull(fullBodyIk);
            Assert.IsNotNull(lookAtIK);
            Assert.IsNotNull(rightHandStoppedPos);
            Assert.IsNotNull(leftHandStoppedPos);
            
            interactionSystem = this.GetComponent<InteractionSystem>();
            locomotionSystem = this.GetComponent<LocomotionSystem>();

            fullBodyIk.solver.OnPostUpdate += OnPostUpdateIKCallback;
            lookAtIK.solver.OnPostUpdate += OnPostUpdateIKCallback;
        }

        // private void InitializeEffectorMaps()
        // {
        //     rightHandEffector = new InteractionEffector(EffectorType.RightHand, rightHandTarget, rightHandRest, rightHandAttach);
        //     leftHandEffector = new InteractionEffector(EffectorType.LeftHand, leftHandTarget, leftHandRest, leftHandAttach);
        //     rightFootEffector = new InteractionEffector(EffectorType.RightFoot, rightFootTarget, rightFootRest, null);
        //     leftFootEffector = new InteractionEffector(EffectorType.LeftFoot, leftFootTarget, leftFootRest, null);
        //     
        //     lookEffector = new InteractionEffector(EffectorType.HeadLook, lookAtTarget, lookAtRest, null);
        //
        //     rigMap = new Dictionary<InteractionEffector, Rig>
        //     {
        //         { rightHandEffector, rightArmRig },
        //         { leftHandEffector, leftArmRig },
        //         { rightFootEffector, rightLegRig },
        //         { leftFootEffector, leftLegRig },
        //         { lookEffector, headRig }
        //     };
        // }

        /// <summary>
        /// Move an effector to a target position with animation
        /// </summary>
        public IEnumerator MoveEffectorTargetToDestination(EffectorType effectorType, Transform destination, float duration, AnimationCurve curve)
        {
            if (interactionSystem.GetEffector(effectorType) == null)
            {
                Debug.LogError($"Effector {effectorType} not found in effector map!");
                yield break;
            }

            // Enable the rig
            StartCoroutine(BlendEffectorWeight(GetEffector(effectorType), 1.0f));
            // Move the target
            yield return StartCoroutine(MoveTarget(GetEffector(effectorType).target, destination, duration, curve));

            // WHY???
            // Disable the rig
            // yield return StartCoroutine(BlendRigWeight(rigMap[effector], 0.0f));
        }

        /// <summary>
        /// Move an effector back to its rest position
        /// </summary>
        public IEnumerator ReturnEffectorTargetToRest(EffectorType effectorType, float duration, AnimationCurve curve)
        {
            if (interactionSystem.GetEffector(effectorType) == null)
            {
                Debug.LogError($"Effector {effectorType} not found in effector map!");
                yield break;
            }

            // WHY???
            // Enable the rig
            // yield return StartCoroutine(BlendRigWeight(rigMap[effector], 1.0f));

            // Disable the rig
            StartCoroutine(BlendEffectorWeight(GetEffector(effectorType), 0.0f));
            // Move back to rest
            yield return StartCoroutine(MoveTarget(GetEffector(effectorType).target, GetEffectorRestTransform(effectorType), duration, curve));
        }

        /// <summary>
        /// Make the character look at a target
        /// </summary>
        public IEnumerator LookAtTarget(Transform target, float duration)
        {
            if (lookAtTarget == null) yield break;
            
            
            StartCoroutine(BlendLookAtSolverWeight(lookAtIK.solver, 1.0f));
            yield return StartCoroutine(MoveTarget(lookAtTarget, target, duration, AnimationCurve.EaseInOut(0, 0, duration, 1)));
        }

        /// <summary>
        /// Return gaze to rest position
        /// </summary>
        public IEnumerator ReturnGazeToRest(float duration)
        {
            if (lookAtIK == null || lookAtTarget == null || lookAtRest == null) yield break;

            StartCoroutine(BlendLookAtSolverWeight(lookAtIK.solver, 0.0f));
            yield return StartCoroutine(MoveTarget(lookAtTarget, lookAtRest, duration, AnimationCurve.EaseInOut(0, 0, duration, 1)));
        }
        
        private IEnumerator BlendLookAtSolverWeight(IKSolverLookAt solver, float targetWeight)
        {
            if (solver == null) yield break;
            
            float startWeight = solver.IKPositionWeight;
            float elapsed = 0f;
            float duration = Mathf.Abs(targetWeight - startWeight) / blendSpeed;
            if (duration <= 0) duration = 0.1f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                solver.IKPositionWeight = Mathf.Lerp(startWeight, targetWeight, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            solver.IKPositionWeight = targetWeight;
        }

        private IEnumerator BlendEffectorWeight(IKEffector effector, float targetWeight)
        {
            if (effector == null) yield break;

            float startWeight = effector.positionWeight;
            float elapsed = 0f;
            float duration = Mathf.Abs(targetWeight - startWeight) / blendSpeed;
            if (duration <= 0) duration = 0.1f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                effector.positionWeight = Mathf.Lerp(startWeight, targetWeight, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            effector.positionWeight = targetWeight;
        }

        private IEnumerator MoveTarget(Transform target, Transform destination, float duration, AnimationCurve curve)
        {
            if (target == null || destination == null) yield break;

            Vector3 startPos = target.position;
            Quaternion startRot = target.rotation;
            Vector3 endPos = destination.position;
            Quaternion endRot = destination.rotation;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = curve.Evaluate(elapsed / duration);
                target.position = Vector3.Lerp(startPos, endPos, t);
                target.rotation = Quaternion.Slerp(startRot, endRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            target.position = endPos;
            target.rotation = endRot;
        }

        // private void SetAllRigWeights(float weight)
        // {
        //     if (rightArmRig != null) rightArmRig.weight = weight;
        //     if (leftArmRig != null) leftArmRig.weight = weight;
        //     if (rightLegRig != null) rightLegRig.weight = weight;
        //     if (leftLegRig != null) leftLegRig.weight = weight;
        //     if (headRig != null) headRig.weight = weight;
        //     if (spineRig != null) spineRig.weight = weight;
        // }

        /// <summary>
        /// Get the desired effector of the rig
        /// </summary>
        public IKEffector GetEffector(EffectorType type)
        {
            switch (type)
            {
                case EffectorType.RightHand:
                    return fullBodyIk.solver.rightHandEffector;
                case EffectorType.LeftHand:
                    return fullBodyIk.solver.leftHandEffector;
                case EffectorType.RightFoot:
                    return fullBodyIk.solver.rightFootEffector;
                case EffectorType.LeftFoot:
                    return fullBodyIk.solver.leftFootEffector;
                case EffectorType.HeadLook:
                    Debug.LogError("To change for Final IK API!");
                    return null;
                default:
                    return null;
            }
        }

        public Transform GetEffectorRestTransform(EffectorType type)
        {
            switch (type)
            {
                case EffectorType.RightHand:
                    return rightHandRest;
                case EffectorType.LeftHand:
                    return leftHandRest;
                case EffectorType.RightFoot:
                    return rightFootRest;
                case EffectorType.LeftFoot:
                    return leftFootRest;
                case EffectorType.HeadLook:
                    return lookAtRest;
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Check if an effector is currently being controlled by IK with weight that tends to 1f
        /// </summary>
        public bool IsEffectorTotallyActive(EffectorType type)
        {
            if (type == EffectorType.HeadLook)
                if (lookAtIK.solver.IKPositionWeight >= 1f) 
                    return true;
                else
                    return false;
            
            IKEffector effector = GetEffector(type);
            if (effector != null)
            {
                //return rigMap[effector].weight > 0.9999f;
                return effector.positionWeight >= 1f;
            }
            return false;
        }
        
        /// <summary>
        /// Check if an effector is currently being controlled by IK with weight that tends to 0f
        /// </summary>
        public bool IsEffectorTotallyInactive(EffectorType type)
        {
            if (type == EffectorType.HeadLook)
                if (lookAtIK.solver.IKPositionWeight <= 0f) 
                    return true;
                else
                    return false;
            
            IKEffector effector = GetEffector(type);
            if (effector != null)
            {
                //return rigMap[effector].weight < 0.00001f;
                return effector.positionWeight <= 0f;
            }
            return false;
        }

        /// <summary>
        /// Check if the actual effector ConstraintTip is too far away from the target
        /// </summary>
        public bool IsConstraintTipAwayFromTarget(EffectorType type)
        {
            IKEffector effector = GetEffector(type);
            
            Vector3 chainTip = GetEffectorTip(type).transform.position;
            Vector3 chaintarget = GetEffector(type).target.transform.position;
            
            bool isAway = (chainTip - chaintarget).magnitude > 0.1f;
            return isAway;
        }
        
        /// <summary>
        /// Get the reference of the ConstraintTip of the effector
        /// </summary>
        public Transform GetEffectorTip(EffectorType type)
        {
            switch (type)
            {
                case EffectorType.RightHand:
                    return fullBodyIk.references.rightHand;
                case EffectorType.LeftHand:
                    return fullBodyIk.references.leftHand;
                case EffectorType.RightFoot:
                    return fullBodyIk.references.rightFoot;
                case EffectorType.LeftFoot:
                    return fullBodyIk.references.leftFoot;
                case EffectorType.HeadLook:
                    Debug.LogError("To change for Final IK API!");
                    return null;
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Get the reference of the ConstraintTip of the effector
        /// </summary>
        public Transform GetEffectorStoppedTransform(EffectorType type)
        {
            switch (type)
            {
                case EffectorType.RightHand:
                    return rightHandStoppedPos;
                case EffectorType.LeftHand:
                    return leftHandStoppedPos;
                case EffectorType.RightFoot:
                case EffectorType.LeftFoot:
                case EffectorType.HeadLook:
                    Debug.LogError("To change for Final IK API!");
                    return null;
                default:
                    return null;
            }
        }

        public void OnPostUpdateIKCallback()
        {
            OnPostUpdate?.Invoke();
        }

        private void OnDestroy()
        {
            fullBodyIk.solver.OnPostUpdate -= OnPostUpdateIKCallback;
            lookAtIK.solver.OnPostUpdate -= OnPostUpdateIKCallback;
        }
    }
} 