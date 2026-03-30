using System.IO;
using ActionSystem;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

namespace HumanoidInteraction
{
    /// <summary>
    /// Example script demonstrating how to use the interaction system
    /// </summary>
    public class ExampleUsageNew : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private AdamAgent agent;

        [Header("Test Objects")]
        [SerializeField] private Interactable interactableObj;
        [SerializeField] private Pickable pickableObj;
        [SerializeField] private Transform dropTransform;
        
        [Header("Test Movement")]
        [SerializeField] private Vector3 movementVector;
        [SerializeField] private Vector3 turnToPoint;
        
        [FormerlySerializedAs("lookingTarget")]
        [Header("Test Looking")]
        [SerializeField] private Transform cameraPov;
        [SerializeField] private Transform lookTarget;
        [SerializeField] private bool isLooking;

        [Header("Test Projection")] [SerializeField]
        private ScreenGridRaycaster screenRaycaster;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = true;

        //[SerializeField] private AgentAction lastAction = null;
        
        private void Start()
        {
            Assert.IsNotNull(cameraPov);
            
            if (agent == null)
                agent = GetComponent<AdamAgent>();
        }

        private void Update()
        {
            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            // Touch action
            if (Input.GetKeyDown(KeyCode.O)) TestTouchAction();
            
            // Pick action
            if (Input.GetKeyDown(KeyCode.P)) TestPickAction();
            
            // Drop action
            if (Input.GetKeyDown(KeyCode.D)) TestDropAction();
            
            // Walk action
            if (Input.GetKeyDown(KeyCode.W)) TestWalkAction();
            
            // Turn action
            if (Input.GetKeyDown(KeyCode.T)) TestTurnAction();
            
            // WalkTurnLookCapture
            if (Input.GetKeyDown(KeyCode.C)) TestCompositeAction();
            
            // Look
            if (Input.GetKeyDown(KeyCode.L)) TestToggleLook();
            
            // Explore
            if (Input.GetKeyDown(KeyCode.E)) TestExploreAction();
            
            // Screen Raycast
            if (Input.GetKeyDown(KeyCode.R)) TestScreenRaysProjection();
            
            // Stop current action
            if (Input.GetKeyDown(KeyCode.Backspace)) StopCurrentAction();
        }

        /// <summary>
        /// Example: Simple touch interaction
        /// </summary>
        public void TestTouchAction()
        {
                agent.Touch(interactableObj, EffectorType.RightHand);
        }

        /// <summary>
        /// Example: Pick action
        /// </summary>
        public void TestPickAction()
        {
            agent.Pick(pickableObj, EffectorType.LeftHand);
        }
        
        /// <summary>
        /// Example: Drop action
        /// </summary>
        public void TestDropAction()
        {
            agent.Drop(pickableObj, dropTransform, EffectorType.LeftHand);
        }
        
        /// <summary>
        /// Example: Walk action
        /// </summary>
        public void TestWalkAction()
        {
            GameObject destinationGO = GameObject.Find("WalkAction_Destination");
            if (destinationGO == null)
                destinationGO = new GameObject("WalkAction_Destination");
            
            agent.Walk(destinationGO.transform.position);
            //agent.Move(agent.transform, movementVector);
            //agent.MoveTurn(agent.transform, movementVector, turnToPoint);
        }
        
        /// <summary>
        /// Example: Turn action
        /// </summary>
        public void TestTurnAction()
        {
            GameObject turnToGO = GameObject.Find("TurnToPoint_Transform");
            if (turnToGO == null)
                turnToGO = new GameObject("TurnToPoint_Transform");
            
            agent.Turn(turnToGO.transform.position);
        }
        
        public void TestCompositeAction()
        {
            // GameObject destinationGO = GameObject.Find("WalkAction_Destination");
            // if (destinationGO == null)
            //     destinationGO = new GameObject("WalkAction_Destination");
            //
            // MoveTurnLookCompositeAction compositeAction = new MoveTurnLookCompositeAction(agent, movementVector, turnToPoint);
            // agent.AddAction(compositeAction);

            LookAndCaptureCompositeAction compositeAction = new LookAndCaptureCompositeAction(agent,lookTarget.position);
            agent.AddAction(compositeAction);
        }

        public void TestToggleLook()
        {
            isLooking = !isLooking;

            if (isLooking)
            {
                agent.Look(cameraPov, lookTarget.position);
            }
            else
            {
                agent.StopLook();
            }
        }

        public void TestExploreAction()
        {
            byte[] bytes = I360Render.Capture( 1920, true, CameraManager.Instance.cam);
            if( bytes != null )
            {
                string path = Path.Combine(Application.dataPath, "ExploreAction_360render" + ".jpeg");
                File.WriteAllBytes( path, bytes );
                Debug.Log( "ExploreAction : 360 render saved to " + path );
            }
        }

        public void TestScreenRaysProjection()
        {
            screenRaycaster.ProjectGridWithTemporaryColliders();
        }


        public void StopCurrentAction()
        {
            agent.StopCurrentAction();
        }

        private void OnInteractionStarted(Interaction interaction)
        {
            if (enableDebugLogging)
                Debug.Log($"Interaction started: {interaction.interactionType} with {interaction.target.Desc} using {interaction.effectorType}");
            
            interaction.OnInteractionStarted -= OnInteractionStarted;
        }

        private void OnInteractionCompleted(Interaction interaction)
        {
            if (enableDebugLogging)
                Debug.Log($"Interaction completed: {interaction.interactionType} with {interaction.target.Desc}");
            
            interaction.OnInteractionCompleted -= OnInteractionCompleted;
        }

        private void OnInteractionFailed(Interaction interaction)
        {
            if (enableDebugLogging)
                Debug.LogWarning($"Interaction failed: {interaction.interactionType} with {interaction.target.Desc}");
            
            interaction.OnInteractionFailed -= OnInteractionFailed;
        }
    }
} 