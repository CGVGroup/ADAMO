using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HumanoidInteraction
{
    /// <summary>
    /// Editor-based setup component for humanoid NPC interaction system
    /// Use the buttons in the inspector to configure the rig offline
    /// </summary>
    public class NPCInteractionSetup : MonoBehaviour
    {
        [Header("Setup Options")]
        [SerializeField] private bool createInteractionPoints = true;
        [SerializeField] private bool setupRigging = true;
        [SerializeField] private bool setupComponents = true;
        
        [Header("Animation Settings")]
        [SerializeField] private float blendSpeed = 4f;
        [SerializeField] private float moveSpeed = 3f;
        
        [Header("Rest Position Offsets")]
        [SerializeField] private Vector3 rightHandRestOffset = new Vector3(0.2f, 0.15f, 0.15f);
        [SerializeField] private Vector3 leftHandRestOffset = new Vector3(-0.2f, 0.15f, 0.15f);
        [SerializeField] private Vector3 rightFootRestOffset = new Vector3(0.1f, 0f, 0.1f);
        [SerializeField] private Vector3 leftFootRestOffset = new Vector3(-0.1f, 0f, 0.1f);
        [SerializeField] private Vector3 lookAtRestOffset = new Vector3(0f, 1.5f, 2f);
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private bool showGizmos = true;

        private Animator animator;
        private RigBuilder rigBuilder;
        private AnimationRiggingController riggingController;
        private InteractionSystem interactionSystem;
        private ExampleUsage exampleUsage;

        /// <summary>
        /// Main setup method - call this to configure everything
        /// </summary>
        [ContextMenu("Setup NPC Interaction System")]
        public void SetupNPCInteractionSystem()
        {
            if (enableDebugLogging)
                Debug.Log($"Setting up interaction system for {gameObject.name}");

            // Step 1: Validate character
            if (!ValidateCharacter())
            {
                Debug.LogError("Character validation failed. Make sure this is a humanoid character with an Animator.");
                return;
            }

            // Step 2: Setup components first (so we have the controllers)
            if (setupComponents)
            {
                SetupComponents();
            }

            // Step 3: Setup rigging
            if (setupRigging)
            {
                SetupRiggingSystem();
            }

            // Step 4: Create interaction points
            if (createInteractionPoints)
            {
                CreateInteractionPoints();
            }

            // Step 5: Setup example usage
            SetupExampleUsage();

            if (enableDebugLogging)
                Debug.Log($"Interaction system setup complete for {gameObject.name}");
        }

        /// <summary>
        /// Setup only the rigging system
        /// </summary>
        [ContextMenu("Setup Rigging Only")]
        public void SetupRiggingOnly()
        {
            if (!ValidateCharacter()) return;
            
            // Ensure components are set up first
            SetupComponents();
            
            SetupRiggingSystem();
            Debug.Log("Rigging setup complete");
        }

        /// <summary>
        /// Setup only the components
        /// </summary>
        [ContextMenu("Setup Components Only")]
        public void SetupComponentsOnly()
        {
            SetupComponents();
            Debug.Log("Components setup complete");
        }

        /// <summary>
        /// Create only the interaction points
        /// </summary>
        [ContextMenu("Create Interaction Points Only")]
        public void CreateInteractionPointsOnly()
        {
            if (!ValidateCharacter()) return;
            CreateInteractionPoints();
            Debug.Log("Interaction points created");
        }

        /// <summary>
        /// Setup example usage component
        /// </summary>
        [ContextMenu("Setup Example Usage")]
        public void SetupExampleUsageOnly()
        {
            SetupExampleUsage();
            Debug.Log("Example usage setup complete");
        }

        /// <summary>
        /// Clean up all created objects
        /// </summary>
        [ContextMenu("Clean Up All")]
        public void CleanUpAll()
        {
            CleanUpRigging();
            CleanUpComponents();
            CleanUpInteractionPoints();
            Debug.Log("Clean up complete");
        }

        public void CleanUpRigging()
        {
            Rig[] rigs = GetComponentsInChildren<Rig>();
            foreach (Rig rig in rigs)
            {
                if (rig != null)
                {
                    DestroyImmediate(rig.gameObject);
                }
            }

            RigBuilder rb = GetComponent<RigBuilder>();
            if (rb != null)
            {
                DestroyImmediate(rb);
            }
        }

        public void CleanUpComponents()
        {
            AnimationRiggingController arc = GetComponent<AnimationRiggingController>();
            if (arc != null)
            {
                DestroyImmediate(arc);
            }

            InteractionSystem ie = GetComponent<InteractionSystem>();
            if (ie != null)
            {
                DestroyImmediate(ie);
            }

            ExampleUsage eu = GetComponent<ExampleUsage>();
            if (eu != null)
            {
                DestroyImmediate(eu);
            }
        }

        public void CleanUpInteractionPoints()
        {
            Transform interactionPoints = transform.Find("Interaction Points");
            if (interactionPoints != null)
            {
                DestroyImmediate(interactionPoints.gameObject);
            }

            // Clean up individual targets
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                if (child != null && (child.name.Contains("Target") || child.name.Contains("Rest") || child.name.Contains("Hint")))
                {
                    if (child.parent == transform || child.parent?.parent == transform)
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        private bool ValidateCharacter()
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("No Animator component found. Please add an Animator to your character.");
                return false;
            }

            if (!animator.isHuman)
            {
                Debug.LogError("Animator is not configured as humanoid. Please set the Animator to humanoid.");
                return false;
            }

            return true;
        }

        private void SetupRiggingSystem()
        {
            if (enableDebugLogging)
                Debug.Log("Setting up rigging system...");

            // Get or create RigBuilder
            rigBuilder = GetComponent<RigBuilder>();
            if (rigBuilder == null)
            {
                rigBuilder = gameObject.AddComponent<RigBuilder>();
            }

            // Create main rig container
            GameObject rigContainer = CreateOrFindChild("Interaction Rig");

            // Create rigs for each effector
            CreateArmRig(rigContainer, "Right Arm Rig", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, true);
            CreateArmRig(rigContainer, "Left Arm Rig", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, false);
            CreateLegRig(rigContainer, "Right Leg Rig", HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
            CreateLegRig(rigContainer, "Left Leg Rig", HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            CreateHeadRig(rigContainer, "Head Rig", HumanBodyBones.Head);
            CreateSpineRig(rigContainer, "Spine Rig", HumanBodyBones.Spine, HumanBodyBones.Chest);

            // Configure RigBuilder layers
            ConfigureRigBuilderLayers();
        }

        private void CreateArmRig(GameObject container, string rigName, HumanBodyBones upperArm, HumanBodyBones lowerArm, HumanBodyBones hand, bool isRight)
        {
            GameObject rigGO = CreateOrFindChild(rigName, container.transform);
            Rig rig = rigGO.GetComponent<Rig>();
            if (rig == null)
                rig = rigGO.AddComponent<Rig>();

            // Create IK constraint
            GameObject ikGO = CreateOrFindChild("IK Constraint", rigGO.transform);
            TwoBoneIKConstraint ik = ikGO.GetComponent<TwoBoneIKConstraint>();
            if (ik == null)
                ik = ikGO.AddComponent<TwoBoneIKConstraint>();

            // Setup IK data
            ik.data.root = animator.GetBoneTransform(upperArm);
            ik.data.mid = animator.GetBoneTransform(lowerArm);
            ik.data.tip = animator.GetBoneTransform(hand);

            // Create target and hint
            Transform target = CreateOrFindChild($"{rigName}_Target", transform).transform;
            Transform hint = CreateOrFindChild($"{rigName}_Hint", animator.GetBoneTransform(lowerArm)).transform;

            // Position hint
            Vector3 hintOffset = isRight ? new Vector3(0, 0, -0.5f) : new Vector3(0, 0, -0.5f);
            hint.localPosition = hintOffset;

            ik.data.target = target;
            ik.data.hint = hint;
            ik.data.targetPositionWeight = 1f;
            ik.data.targetRotationWeight = 1f;
            ik.data.hintWeight = 1f;

            // Store reference
            if (isRight)
                SetPrivateField(riggingController, "rightArmRig", rig);
            else
                SetPrivateField(riggingController, "leftArmRig", rig);
        }

        private void CreateLegRig(GameObject container, string rigName, HumanBodyBones upperLeg, HumanBodyBones lowerLeg, HumanBodyBones foot)
        {
            GameObject rigGO = CreateOrFindChild(rigName, container.transform);
            Rig rig = rigGO.GetComponent<Rig>();
            if (rig == null)
                rig = rigGO.AddComponent<Rig>();

            // Create IK constraint
            GameObject ikGO = CreateOrFindChild("IK Constraint", rigGO.transform);
            TwoBoneIKConstraint ik = ikGO.GetComponent<TwoBoneIKConstraint>();
            if (ik == null)
                ik = ikGO.AddComponent<TwoBoneIKConstraint>();

            // Setup IK data
            ik.data.root = animator.GetBoneTransform(upperLeg);
            ik.data.mid = animator.GetBoneTransform(lowerLeg);
            ik.data.tip = animator.GetBoneTransform(foot);

            // Create target and hint
            Transform target = CreateOrFindChild($"{rigName}_Target", transform).transform;
            Transform hint = CreateOrFindChild($"{rigName}_Hint", animator.GetBoneTransform(lowerLeg)).transform;

            // Position hint
            hint.localPosition = new Vector3(0, 0, 0.5f);

            ik.data.target = target;
            ik.data.hint = hint;
            ik.data.targetPositionWeight = 1f;
            ik.data.targetRotationWeight = 1f;
            ik.data.hintWeight = 1f;

            // Store reference
            if (rigName.Contains("Right"))
                SetPrivateField(riggingController, "rightLegRig", rig);
            else
                SetPrivateField(riggingController, "leftLegRig", rig);
        }

        private void CreateHeadRig(GameObject container, string rigName, HumanBodyBones head)
        {
            GameObject rigGO = CreateOrFindChild(rigName, container.transform);
            Rig rig = rigGO.GetComponent<Rig>();
            if (rig == null)
                rig = rigGO.AddComponent<Rig>();

            // Create aim constraint
            GameObject aimGO = CreateOrFindChild("Aim Constraint", rigGO.transform);
            MultiAimConstraint aim = aimGO.GetComponent<MultiAimConstraint>();
            if (aim == null)
                aim = aimGO.AddComponent<MultiAimConstraint>();

            // Setup aim data
            aim.data.constrainedObject = animator.GetBoneTransform(head);
            
            // Create look-at target
            Transform lookAtTarget = CreateOrFindChild("LookAt_Target", transform).transform;
            
            var sources = new WeightedTransformArray(1);
            sources.SetTransform(0, lookAtTarget);
            sources.SetWeight(0, 1f);
            aim.data.sourceObjects = sources;
            aim.data.aimAxis = MultiAimConstraintData.Axis.Z;

            // Store reference
            SetPrivateField(riggingController, "headRig", rig);
            SetPrivateField(riggingController, "lookAtTarget", lookAtTarget);
        }

        private void CreateSpineRig(GameObject container, string rigName, HumanBodyBones spine, HumanBodyBones chest)
        {
            GameObject rigGO = CreateOrFindChild(rigName, container.transform);
            Rig rig = rigGO.GetComponent<Rig>();
            if (rig == null)
                rig = rigGO.AddComponent<Rig>();

            // Create chain IK constraint
            GameObject chainIKGO = CreateOrFindChild("Chain IK Constraint", rigGO.transform);
            ChainIKConstraint chainIK = chainIKGO.GetComponent<ChainIKConstraint>();
            if (chainIK == null)
                chainIK = chainIKGO.AddComponent<ChainIKConstraint>();

            // Setup chain IK data
            chainIK.data.root = animator.GetBoneTransform(spine);
            chainIK.data.tip = animator.GetBoneTransform(chest);
            
            // Create spine target
            Transform spineTarget = CreateOrFindChild("Spine_Target", transform).transform;
            chainIK.data.target = spineTarget;
            chainIK.data.chainRotationWeight = 1f;
            chainIK.data.tipRotationWeight = 1f;

            // Store reference
            SetPrivateField(riggingController, "spineRig", rig);
            // Note: spineIKTarget is not a field in AnimationRiggingController, so we skip it
        }

        private void ConfigureRigBuilderLayers()
        {
            rigBuilder.layers.Clear();

            // Add all rigs to the builder
            Rig[] rigs = GetComponentsInChildren<Rig>();
            foreach (Rig rig in rigs)
            {
                rigBuilder.layers.Add(new RigLayer(rig));
            }
        }

        private void SetupComponents()
        {
            if (enableDebugLogging)
                Debug.Log("Setting up components...");

            // Setup AnimationRiggingController
            riggingController = GetComponent<AnimationRiggingController>();
            if (riggingController == null)
            {
                riggingController = gameObject.AddComponent<AnimationRiggingController>();
            }

            // Setup InteractionExecutor
            interactionSystem = GetComponent<InteractionSystem>();
            if (interactionSystem == null)
            {
                interactionSystem = gameObject.AddComponent<InteractionSystem>();
            }

            // Setup ExampleUsage
            exampleUsage = GetComponent<ExampleUsage>();
            if (exampleUsage == null)
            {
                exampleUsage = gameObject.AddComponent<ExampleUsage>();
            }

            // Configure settings
            SetPrivateField(riggingController, "blendSpeed", blendSpeed);
            SetPrivateField(riggingController, "moveSpeed", moveSpeed);
        }

        private void CreateInteractionPoints()
        {
            if (enableDebugLogging)
                Debug.Log("Creating interaction points...");

            // Create interaction points container
            GameObject pointsContainer = CreateOrFindChild("Interaction Points");

            // Create hand targets and rest positions
            CreateHandPoints(pointsContainer, "Right Hand", rightHandRestOffset, true);
            CreateHandPoints(pointsContainer, "Left Hand", leftHandRestOffset, false);

            // Create foot targets and rest positions
            CreateFootPoints(pointsContainer, "Right Foot", rightFootRestOffset, true);
            CreateFootPoints(pointsContainer, "Left Foot", leftFootRestOffset, false);

            // Create look-at rest position
            Transform lookAtRest = CreateOrFindChild("LookAt_Rest", pointsContainer.transform).transform;
            lookAtRest.position = transform.position + transform.forward * lookAtRestOffset.z + Vector3.up * lookAtRestOffset.y;
            SetPrivateField(riggingController, "lookAtRest", lookAtRest);
        }

        private void CreateHandPoints(GameObject container, string handName, Vector3 offset, bool isRight)
        {
            Transform handBone = animator.GetBoneTransform(isRight ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            
            // Create target
            Transform target = CreateOrFindChild($"{handName}_Target", container.transform).transform;
            target.position = handBone.position;
            target.rotation = handBone.rotation;

            // Create rest position
            Transform rest = CreateOrFindChild($"{handName}_Rest", container.transform).transform;
            rest.position = transform.position + transform.right * offset.x + Vector3.up * offset.y + transform.forward * offset.z;
            rest.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);

            // Store references
            if (isRight)
            {
                SetPrivateField(riggingController, "rightHandTarget", target);
                SetPrivateField(riggingController, "rightHandRest", rest);
                SetPrivateField(riggingController, "rightHandBone", handBone);
            }
            else
            {
                SetPrivateField(riggingController, "leftHandTarget", target);
                SetPrivateField(riggingController, "leftHandRest", rest);
                SetPrivateField(riggingController, "leftHandBone", handBone);
            }
        }

        private void CreateFootPoints(GameObject container, string footName, Vector3 offset, bool isRight)
        {
            Transform footBone = animator.GetBoneTransform(isRight ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);
            
            // Create target
            Transform target = CreateOrFindChild($"{footName}_Target", container.transform).transform;
            target.position = footBone.position;
            target.rotation = footBone.rotation;

            // Create rest position
            Transform rest = CreateOrFindChild($"{footName}_Rest", container.transform).transform;
            rest.position = transform.position + transform.right * offset.x + Vector3.up * offset.y + transform.forward * offset.z;
            rest.rotation = footBone.rotation;

            // Store references
            if (isRight)
            {
                SetPrivateField(riggingController, "rightFootTarget", target);
                SetPrivateField(riggingController, "rightFootRest", rest);
            }
            else
            {
                SetPrivateField(riggingController, "leftFootTarget", target);
                SetPrivateField(riggingController, "leftFootRest", rest);
            }
        }

        private void SetupExampleUsage()
        {
            if (enableDebugLogging)
                Debug.Log("Setting up example usage...");

            exampleUsage = GetComponent<ExampleUsage>();
            if (exampleUsage == null)
            {
                exampleUsage = gameObject.AddComponent<ExampleUsage>();
            }
        }

        private GameObject CreateOrFindChild(string childName, Transform parent = null)
        {
            Transform parentTransform = parent != null ? parent : transform;
            Transform child = parentTransform.Find(childName);
            
            if (child == null)
            {
                GameObject newChild = new GameObject(childName);
                newChild.transform.SetParent(parentTransform, false);
                return newChild;
            }
            
            return child.gameObject;
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
            {
                Debug.LogError($"Cannot set field '{fieldName}' - target is null");
                return;
            }

            var field = target.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                Debug.LogWarning($"Field '{fieldName}' not found on {target.GetType().Name}");
            }
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            // Draw interaction points
            Transform[] interactionPoints = GetComponentsInChildren<Transform>();
            foreach (Transform point in interactionPoints)
            {
                if (point.name.Contains("Target") || point.name.Contains("Rest"))
                {
                    Gizmos.color = point.name.Contains("Target") ? Color.green : Color.yellow;
                    Gizmos.DrawWireSphere(point.position, 0.1f);
                    
                    if (point.name.Contains("Hand"))
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawLine(point.position, transform.position);
                    }
                }
            }
        }

        /// <summary>
        /// Validate the setup and report any issues
        /// </summary>
        [ContextMenu("Validate Setup")]
        public void ValidateSetup()
        {
            Debug.Log("Validating NPC interaction setup...");

            bool isValid = true;

            // Check required components
            if (GetComponent<Animator>() == null)
            {
                Debug.LogError("Missing Animator component");
                isValid = false;
            }

            if (GetComponent<RigBuilder>() == null)
            {
                Debug.LogError("Missing RigBuilder component");
                isValid = false;
            }

            if (GetComponent<AnimationRiggingController>() == null)
            {
                Debug.LogError("Missing AnimationRiggingController component");
                isValid = false;
            }

            if (GetComponent<InteractionSystem>() == null)
            {
                Debug.LogError("Missing InteractionExecutor component");
                isValid = false;
            }

            // Check rigs
            Rig[] rigs = GetComponentsInChildren<Rig>();
            if (rigs.Length == 0)
            {
                Debug.LogError("No rigs found. Run setup first.");
                isValid = false;
            }

            if (isValid)
            {
                Debug.Log("Setup validation passed! NPC is ready for interactions.");
            }
        }
    }
} 