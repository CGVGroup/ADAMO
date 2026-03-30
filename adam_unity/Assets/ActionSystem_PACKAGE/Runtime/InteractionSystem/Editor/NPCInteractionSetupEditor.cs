using UnityEngine;
using UnityEngine.Animations.Rigging;
#if UNITY_EDITOR
using UnityEditor;

namespace HumanoidInteraction
{
    /// <summary>
    /// Custom editor for NPCInteractionSetup with buttons for offline configuration
    /// </summary>
    [CustomEditor(typeof(NPCInteractionSetup))]
    public class NPCInteractionSetupEditor : Editor
    {
        private NPCInteractionSetup setup;
        private bool showValidation = true;
        private bool showSetupButtons = true;
        private bool showCleanupButtons = false;

        private void OnEnable()
        {
            setup = (NPCInteractionSetup)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // Validation Section
            showValidation = EditorGUILayout.Foldout(showValidation, "Validation", true);
            if (showValidation)
            {
                EditorGUILayout.BeginVertical("box");
                
                if (GUILayout.Button("Validate Character Setup", GUILayout.Height(25)))
                {
                    ValidateCharacterSetup();
                }

                if (GUILayout.Button("Check Required Components", GUILayout.Height(25)))
                {
                    CheckRequiredComponents();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);

            // Setup Buttons Section
            showSetupButtons = EditorGUILayout.Foldout(showSetupButtons, "Setup Options", true);
            if (showSetupButtons)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.LabelField("Full Setup:", EditorStyles.boldLabel);
                if (GUILayout.Button("Setup Complete System", GUILayout.Height(30)))
                {
                    SetupCompleteSystem();
                }

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Individual Setup:", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Setup Rigging", GUILayout.Height(25)))
                {
                    SetupRiggingOnly();
                }
                if (GUILayout.Button("Setup Components", GUILayout.Height(25)))
                {
                    SetupComponentsOnly();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Create Interaction Points", GUILayout.Height(25)))
                {
                    CreateInteractionPointsOnly();
                }
                if (GUILayout.Button("Setup Example Usage", GUILayout.Height(25)))
                {
                    SetupExampleUsageOnly();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);

            // Cleanup Buttons Section
            showCleanupButtons = EditorGUILayout.Foldout(showCleanupButtons, "Cleanup Options", true);
            if (showCleanupButtons)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.HelpBox("Warning: These actions will permanently delete created objects!", MessageType.Warning);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clean Up All", GUILayout.Height(25)))
                {
                    CleanUpAll();
                }
                if (GUILayout.Button("Clean Up Rigging", GUILayout.Height(25)))
                {
                    CleanUpRiggingOnly();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clean Up Components", GUILayout.Height(25)))
                {
                    CleanUpComponentsOnly();
                }
                if (GUILayout.Button("Clean Up Interaction Points", GUILayout.Height(25)))
                {
                    CleanUpInteractionPointsOnly();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // Help Section
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.HelpBox(
                "Setup Instructions:\n" +
                "1. Validate your character first\n" +
                "2. Setup rigging system\n" +
                "3. Setup components\n" +
                "4. Create interaction points\n" +
                "5. Setup example usage\n\n" +
                "Use the buttons above to configure your NPC offline in the editor.",
                MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void ValidateCharacterSetup()
        {
            Animator animator = setup.GetComponent<Animator>();
            if (animator == null)
            {
                EditorUtility.DisplayDialog("Validation Failed", "No Animator component found. Please add an Animator to your character.", "OK");
                return;
            }

            if (!animator.isHuman)
            {
                EditorUtility.DisplayDialog("Validation Failed", "Animator is not configured as humanoid. Please set the Animator Controller Type to Humanoid.", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Validation Success", "Character setup is valid! You can proceed with the rigging setup.", "OK");
        }

        private void CheckRequiredComponents()
        {
            string missingComponents = "";

            if (setup.GetComponent<Animator>() == null)
                missingComponents += "• Animator\n";

            if (setup.GetComponent<RigBuilder>() == null)
                missingComponents += "• RigBuilder\n";

            if (setup.GetComponent<AnimationRiggingController>() == null)
                missingComponents += "• AnimationRiggingController\n";

            if (setup.GetComponent<InteractionSystem>() == null)
                missingComponents += "• InteractionExecutor\n";

            if (string.IsNullOrEmpty(missingComponents))
            {
                EditorUtility.DisplayDialog("Components Check", "All required components are present!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Missing Components", 
                    "The following components are missing:\n\n" + missingComponents + 
                    "\nUse the setup buttons to add them.", "OK");
            }
        }

        private void SetupCompleteSystem()
        {
            if (EditorUtility.DisplayDialog("Setup Complete System", 
                "This will set up the entire interaction system. Continue?", "Yes", "No"))
            {
                Undo.RecordObject(setup, "Setup Complete System");
                setup.SetupNPCInteractionSystem();
                EditorUtility.SetDirty(setup);
            }
        }

        private void SetupRiggingOnly()
        {
            if (EditorUtility.DisplayDialog("Setup Rigging", 
                "This will set up the Animation Rigging system. Continue?", "Yes", "No"))
            {
                Undo.RecordObject(setup, "Setup Rigging");
                setup.SetupRiggingOnly();
                EditorUtility.SetDirty(setup);
            }
        }

        private void SetupComponentsOnly()
        {
            if (EditorUtility.DisplayDialog("Setup Components", 
                "This will add the required components. Continue?", "Yes", "No"))
            {
                Undo.RecordObject(setup, "Setup Components");
                setup.SetupComponentsOnly();
                EditorUtility.SetDirty(setup);
            }
        }

        private void CreateInteractionPointsOnly()
        {
            if (EditorUtility.DisplayDialog("Create Interaction Points", 
                "This will create the interaction points. Continue?", "Yes", "No"))
            {
                Undo.RecordObject(setup, "Create Interaction Points");
                setup.CreateInteractionPointsOnly();
                EditorUtility.SetDirty(setup);
            }
        }

        private void SetupExampleUsageOnly()
        {
            if (EditorUtility.DisplayDialog("Setup Example Usage", 
                "This will add the example usage component. Continue?", "Yes", "No"))
            {
                Undo.RecordObject(setup, "Setup Example Usage");
                setup.SetupExampleUsageOnly();
                EditorUtility.SetDirty(setup);
            }
        }

        private void CleanUpAll()
        {
            if (EditorUtility.DisplayDialog("Clean Up All", 
                "This will permanently delete all created objects. This action cannot be undone!\n\nContinue?", "Yes", "No"))
            {
                Undo.RecordObject(setup, "Clean Up All");
                setup.CleanUpAll();
                EditorUtility.SetDirty(setup);
            }
        }

        private void CleanUpRiggingOnly()
        {
            if (EditorUtility.DisplayDialog("Clean Up Rigging", 
                "This will permanently delete all rigging objects. Continue?", "Yes", "No"))
            {
                Undo.RecordObject(setup, "Clean Up Rigging");
                setup.CleanUpRigging();
                EditorUtility.SetDirty(setup);
            }
        }

        private void CleanUpComponentsOnly()
        {
            if (EditorUtility.DisplayDialog("Clean Up Components", 
                "This will permanently delete all interaction components. Continue?", "Yes", "No"))
            {
                Undo.RecordObject(setup, "Clean Up Components");
                setup.CleanUpComponents();
                EditorUtility.SetDirty(setup);
            }
        }

        private void CleanUpInteractionPointsOnly()
        {
            if (EditorUtility.DisplayDialog("Clean Up Interaction Points", 
                "This will permanently delete all interaction points. Continue?", "Yes", "No"))
            {
                Undo.RecordObject(setup, "Clean Up Interaction Points");
                setup.CleanUpInteractionPoints();
                EditorUtility.SetDirty(setup);
            }
        }
    }
}
#endif 