using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class InteractiveLegacyRetargeter : EditorWindow
{
    private GameObject sourceRig;
    private GameObject targetRig;
    private AnimationClip sourceClip;

    private Dictionary<string, string> bonePathMapping = new Dictionary<string, string>();
    private List<string> missingBones = new List<string>();
    private Vector2 scrollPosition;
    private bool hasAnalyzed = false;

    // Create a menu item to open this window
    [MenuItem("Tools/Animation/Interactive Legacy Retargeter")]
    public static void ShowWindow()
    {
        GetWindow<InteractiveLegacyRetargeter>("Legacy Retargeter");
    }

    // This method draws the window's UI
    void OnGUI()
    {
        EditorGUILayout.LabelField("1. Assign Objects", EditorStyles.boldLabel);
        sourceRig = (GameObject)EditorGUILayout.ObjectField("Source Rig", sourceRig, typeof(GameObject), true);
        targetRig = (GameObject)EditorGUILayout.ObjectField("Target Rig", targetRig, typeof(GameObject), true);
        sourceClip = (AnimationClip)EditorGUILayout.ObjectField("Source Animation Clip", sourceClip, typeof(AnimationClip), false);

        EditorGUILayout.Space(10);

        // Enable the "Analyze" button only if all inputs are present
        GUI.enabled = sourceRig != null && targetRig != null;
        if (GUILayout.Button("2. Analyze and Map Bones"))
        {
            AnalyzeRigs();
        }
        GUI.enabled = true;

        EditorGUILayout.Space(10);

        // Display the results of the analysis
        if (hasAnalyzed)
        {
            EditorGUILayout.LabelField("3. Review Bone Mapping", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            foreach (var mapping in bonePathMapping)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = Color.green;
                GUILayout.Label("✓ " + mapping.Key);
                GUI.color = Color.white;
                GUILayout.Label("➡️ " + mapping.Value);
                EditorGUILayout.EndHorizontal();
            }

            if (missingBones.Any())
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("The following animated bones are missing from the Target Rig:", EditorStyles.boldLabel);
                foreach (string missingBone in missingBones)
                {
                    GUILayout.Label("✗ " + missingBone);
                }
                GUI.color = Color.white;
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(10);
        
        // Enable the "Retarget" button only if analysis is complete and a clip is assigned
        GUI.enabled = hasAnalyzed && sourceClip != null;
        if (GUILayout.Button("4. Create Retargeted Animation Clip"))
        {
            RetargetAnimation();
        }
        GUI.enabled = true;
    }

    private void AnalyzeRigs()
    {
        bonePathMapping.Clear();
        missingBones.Clear();
        hasAnalyzed = false;

        if (sourceRig == null || targetRig == null) return;
        
        // Get all transforms from both rigs for efficient lookup
        var sourceTransforms = sourceRig.GetComponentsInChildren<Transform>(true);
        var targetTransforms = targetRig.GetComponentsInChildren<Transform>(true);

        foreach (var sourceBone in sourceTransforms)
        {
            // Find a bone with the same name in the target rig
            var targetBone = targetTransforms.FirstOrDefault(t => t.name == sourceBone.name);

            if (targetBone != null)
            {
                // Store the full path from the root of the rig
                string sourcePath = AnimationUtility.CalculateTransformPath(sourceBone, sourceRig.transform);
                string targetPath = AnimationUtility.CalculateTransformPath(targetBone, targetRig.transform);
                bonePathMapping[sourcePath] = targetPath;
            }
        }
        
        // Now, check if any bones animated in the clip are missing from our mapping
        if(sourceClip != null)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(sourceClip))
            {
                if (!bonePathMapping.ContainsKey(binding.path))
                {
                    if(!missingBones.Contains(binding.path))
                    {
                        missingBones.Add(binding.path);
                    }
                }
            }
        }
        
        hasAnalyzed = true;
        Debug.Log($"Analysis complete. Found {bonePathMapping.Count} matching bones. Found {missingBones.Count} missing animated bones.");
    }

    private void RetargetAnimation()
    {
        // Create a brand new animation clip; this is safer than modifying the original
        AnimationClip newClip = new AnimationClip();
        newClip.name = $"{sourceClip.name}_{targetRig.name}_Retargeted";

        // Get all animated properties (curves) from the source clip
        EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(sourceClip);

        foreach (EditorCurveBinding binding in curveBindings)
        {
            // Check if we have a mapping for this bone's path
            if (bonePathMapping.TryGetValue(binding.path, out string newPath))
            {
                // Get the animation curve from the source clip
                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                
                // Create a new binding with the updated path
                EditorCurveBinding newBinding = new EditorCurveBinding
                {
                    path = newPath,
                    propertyName = binding.propertyName,
                    type = binding.type
                };

                // Apply the curve to our new clip with the new binding
                AnimationUtility.SetEditorCurve(newClip, newBinding, curve);
            }
        }
        
        // Ask the user where to save the new clip
        string path = EditorUtility.SaveFilePanelInProject("Save New Animation Clip", newClip.name, "anim", "Please enter a file name to save the new animation to.");

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(newClip, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newClip;
            Debug.Log($"✅ Successfully created new animation clip at: {path}");
        }
    }
}