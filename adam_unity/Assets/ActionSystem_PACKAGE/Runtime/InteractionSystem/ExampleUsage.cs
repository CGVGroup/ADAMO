using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HumanoidInteraction
{
    /// <summary>
    /// Example script demonstrating how to use the interaction system
    /// </summary>
    public class ExampleUsage : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private InteractionSystem interactionSystem;
        
        [Header("Test Objects")]
        [SerializeField] private List<Pickable> testObjects;
        
        [Header("UI")]
        [SerializeField] private Button touchButton;
        [SerializeField] private Button grabButton;
        [SerializeField] private Button useButton;
        [SerializeField] private Button pushButton;
        [SerializeField] private Button stopButton;
        
        [Header("Keyboard Controls")]
        [SerializeField] private bool enableKeyboardControls = true;
        //[SerializeField] private bool showControlsOnStart = true;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = true;

        [SerializeField] private Interaction lastInteraction = null;
        
        private void Start()
        {
            if (interactionSystem == null)
                interactionSystem = GetComponent<InteractionSystem>();
        }

        private void SetupEvents(Interaction interaction)
        {
            if (interactionSystem != null)
            {
                interaction.OnInteractionStarted += OnInteractionStarted;
                interaction.OnInteractionCompleted += OnInteractionCompleted;
                interaction.OnInteractionFailed += OnInteractionFailed;
            }
        }

        private void Update()
        {
            if (!enableKeyboardControls || interactionSystem == null) return;

            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            // Touch interactions
            if (Input.GetKeyDown(KeyCode.T))
            {
                TestTouchInteraction();
            }
            if (Input.GetKeyDown(KeyCode.Y))
            {
                TestTouchInteractionLeft();
            }

            // Grab interactions
            if (Input.GetKeyDown(KeyCode.G))
            {
                TestGrabInteraction();
            }
            if (Input.GetKeyDown(KeyCode.H))
            {
                TestGrabInteractionRight();
            }

            // Use interactions
            if (Input.GetKeyDown(KeyCode.U))
            {
                TestUseInteraction();
            }
            if (Input.GetKeyDown(KeyCode.I))
            {
                TestUseInteractionLeft();
            }

            // Push interactions
            if (Input.GetKeyDown(KeyCode.P))
            {
                TestPushInteraction();
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
                TestPushInteractionLeft();
            }

            // Foot interactions
            if (Input.GetKeyDown(KeyCode.F))
            {
                TestFootInteraction();
            }

            // Stop current interaction
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                StopInteraction(lastInteraction);
            }

            // Toggle debug logging
            if (Input.GetKeyDown(KeyCode.D) && Input.GetKey(KeyCode.LeftControl))
            {
                ToggleDebugLogging();
            }
        }

        /// <summary>
        /// Toggle debug logging on/off
        /// </summary>
        private void ToggleDebugLogging()
        {
            enableDebugLogging = !enableDebugLogging;
            Debug.Log($"Debug logging {(enableDebugLogging ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Example: Simple touch interaction
        /// </summary>
        public void TestTouchInteraction()
        {
            if (testObjects.Count > 0)
            {
                var target = testObjects[0];
                if (target != null && target.CanInteract)
                {
                    if (enableDebugLogging)
                        Debug.Log($"Testing touch interaction with {target.Desc}");
                    
                    Interaction interaction = interactionSystem.StartSimpleTouchInteraction(target, EffectorType.RightHand);
                    SetupEvents(interaction);
                    lastInteraction = interaction;
                }
            }
        }

        /// <summary>
        /// Example: Grab interaction with custom settings
        /// </summary>
        public void TestGrabInteraction()
        {
            if (testObjects.Count > 1)
            {
                var target = testObjects[1];
                if (target != null && target.CanInteract)
                {
                    if (enableDebugLogging)
                        Debug.Log($"Testing grab interaction with {target.Desc}");
                    
                    Interaction interaction = interactionSystem.StartPickInteraction(target, EffectorType.RightHand);
                    SetupEvents(interaction);
                    lastInteraction = interaction;
                }
            }
        }
        
        public void TestUseInteraction()
        {
            if (testObjects.Count > 2)
            {
                var target = testObjects[2];
                if (target != null && target.CanInteract)
                {
                    if (enableDebugLogging)
                        Debug.Log($"Testing use interaction with {target.Desc}");
                    
                    var interaction = new Interaction(target, InteractionType.Use, EffectorType.RightHand)
                    {
                        reachDuration = 1.0f,
                        holdDuration = 2.0f, // Longer hold for using
                        returnDuration = 1.0f,
                        useLookAt = true,
                    };
                    
                    interactionSystem.StartInteraction(interaction);
                }
            }
        }
        
        public void TestPushInteraction()
        {
            if (testObjects.Count > 3)
            {
                var target = testObjects[3];
                if (target != null && target.CanInteract)
                {
                    if (enableDebugLogging)
                        Debug.Log($"Testing push interaction with {target.Desc}");
                    
                    var interaction = new Interaction(target, InteractionType.Push, EffectorType.RightHand)
                    {
                        reachDuration = 0.8f,
                        holdDuration = 0.3f,
                        returnDuration = 0.8f,
                        useLookAt = true,
                    };
                    
                    interactionSystem.StartInteraction(interaction);
                }
            }
        }

        /// <summary>
        /// Test touch interaction with left hand
        /// </summary>
        public void TestTouchInteractionLeft()
        {
            if (testObjects.Count > 0)
            {
                var target = testObjects[0];
                if (target != null && target.CanInteract)
                {
                    if (enableDebugLogging)
                        Debug.Log($"Testing touch interaction (left hand) with {target.Desc}");
                    
                    interactionSystem.StartSimpleTouchInteraction(target, EffectorType.LeftHand);
                }
            }
        }

        /// <summary>
        /// Test grab interaction with right hand
        /// </summary>
        public void TestGrabInteractionRight()
        {
            if (testObjects.Count > 1)
            {
                var target = testObjects[1];
                if (target != null && target.CanInteract)
                {
                    if (enableDebugLogging)
                        Debug.Log($"Testing grab interaction (right hand) with {target.Desc}");
                    
                    var interaction = new Interaction(target, InteractionType.Pick, EffectorType.RightHand)
                    {
                        reachDuration = 1.5f,
                        holdDuration = 0.8f,
                        returnDuration = 1.2f,
                        useLookAt = true,
                    };
                    
                    interactionSystem.StartInteraction(interaction);
                }
            }
        }

        /// <summary>
        /// Test use interaction with left hand
        /// </summary>
        public void TestUseInteractionLeft()
        {
            if (testObjects.Count > 2)
            {
                var target = testObjects[2];
                if (target != null && target.CanInteract)
                {
                    if (enableDebugLogging)
                        Debug.Log($"Testing use interaction (left hand) with {target.Desc}");
                    
                    var interaction = new Interaction(target, InteractionType.Use, EffectorType.LeftHand)
                    {
                        reachDuration = 1.0f,
                        holdDuration = 2.0f,
                        returnDuration = 1.0f,
                        useLookAt = true,
                    };
                    
                    interactionSystem.StartInteraction(interaction);
                }
            }
        }

        /// <summary>
        /// Test push interaction with left hand
        /// </summary>
        public void TestPushInteractionLeft()
        {
            if (testObjects.Count > 3)
            {
                var target = testObjects[3];
                if (target != null && target.CanInteract)
                {
                    if (enableDebugLogging)
                        Debug.Log($"Testing push interaction (left hand) with {target.Desc}");
                    
                    var interaction = new Interaction(target, InteractionType.Push, EffectorType.LeftHand)
                    {
                        reachDuration = 0.8f,
                        holdDuration = 0.3f,
                        returnDuration = 0.8f,
                        useLookAt = true,
                    };
                    
                    interactionSystem.StartInteraction(interaction);
                }
            }
        }

        /// <summary>
        /// Test foot interaction
        /// </summary>
        public void TestFootInteraction()
        {
            if (testObjects.Count > 4)
            {
                var target = testObjects[4];
                if (target != null && target.CanInteract)
                {
                    if (enableDebugLogging)
                        Debug.Log($"Testing foot interaction with {target.Desc}");
                    
                    var interaction = new Interaction(target, InteractionType.Touch, EffectorType.RightFoot)
                    {
                        reachDuration = 1.2f,
                        holdDuration = 0.5f,
                        returnDuration = 1.2f,
                        useLookAt = true,
                    };
                    
                    interactionSystem.StartInteraction(interaction);
                }
            }
        }

        public void StopInteraction(Interaction interaction)
        {
            if (enableDebugLogging)
                Debug.Log("Stopping  interaction");
            
            interactionSystem.StopInteraction(interaction);
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