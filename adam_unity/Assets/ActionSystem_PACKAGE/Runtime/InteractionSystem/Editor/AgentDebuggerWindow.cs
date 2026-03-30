using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using ActionSystem;

public class AgentDebuggerWindow : EditorWindow
{
    private Agent selectedAgent;
    private int selectedAgentInstanceId;
    private Vector2 scrollPosition;

    // A single dictionary to manage the foldout state for all actions.
    private readonly Dictionary<int, bool> foldoutStates = new Dictionary<int, bool>();

    // Dictionary to hold colors for different action states.
    private readonly Dictionary<ActionState, Color> stateColors = new Dictionary<ActionState, Color>
    {
        { ActionState.Idle, Color.white },
        { ActionState.Updating, Color.cyan },
        { ActionState.Completed, Color.green },
        { ActionState.Failed, new Color(1f, 0.5f, 0.5f) }, // A lighter red
        { ActionState.Stopped, new Color(1f, 0.75f, 0.4f) } // Orange
    };
    
    // Key to save the agent's ID across play-mode changes and assembly reloads.
    private const string SelectedAgentIdKey = "AgentDebugger_SelectedAgentId";

    [MenuItem("Window/Agent Action Debugger")]
    public static void ShowWindow()
    {
        GetWindow<AgentDebuggerWindow>("Agent Action Debugger");
    }

    private void OnEnable()
    {
        selectedAgentInstanceId = SessionState.GetInt(SelectedAgentIdKey, 0);
        if (selectedAgentInstanceId != 0)
        {
            selectedAgent = EditorUtility.InstanceIDToObject(selectedAgentInstanceId) as Agent;
        }
        Selection.selectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }
    
    private void OnSelectionChanged()
    {
        if (Selection.activeGameObject != null)
        {
            Agent agentInSelection = Selection.activeGameObject.GetComponent<Agent>();
            if (agentInSelection != null)
            {
                selectedAgent = agentInSelection;
                selectedAgentInstanceId = selectedAgent.GetInstanceID();
                SessionState.SetInt(SelectedAgentIdKey, selectedAgentInstanceId);
                Repaint();
            }
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Agent Action Inspector", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        selectedAgent = (Agent)EditorGUILayout.ObjectField("Selected Agent", selectedAgent, typeof(Agent), true);
        if (EditorGUI.EndChangeCheck())
        {
            if (selectedAgent != null)
            {
                selectedAgentInstanceId = selectedAgent.GetInstanceID();
                SessionState.SetInt(SelectedAgentIdKey, selectedAgentInstanceId);
            }
            else
            {
                selectedAgentInstanceId = 0;
                SessionState.EraseInt(SelectedAgentIdKey);
            }
        }

        EditorGUILayout.Space();

        if (selectedAgent == null)
        {
            EditorGUILayout.HelpBox("Select an Agent in the scene to inspect its action queue.", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        var currentAction = GetPrivateField<AgentAction>(selectedAgent, "currentAction");
        var actionsQueue = GetPrivateField<List<AgentAction>>(selectedAgent, "actionsQueue");
        var pastActions = GetPrivateField<List<AgentAction>>(selectedAgent, "pastActions");

        EditorGUILayout.LabelField("Current Action", EditorStyles.boldLabel);
        if (currentAction != null)
        {
            DisplayActionRecursive(currentAction, "Current");
        }
        else
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("None");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Actions Queue", EditorStyles.boldLabel);
        if (actionsQueue != null && actionsQueue.Count > 0)
        {
            for (int i = 0; i < actionsQueue.Count; i++)
            {
                DisplayActionRecursive(actionsQueue[i], $"Queue [{i}]");
            }
        }
        else
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Action queue is empty.");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Past Actions", EditorStyles.boldLabel);
        if (pastActions != null && pastActions.Count > 0)
        {
            for (int i = pastActions.Count - 1; i >= 0; i--)
            {
                DisplayActionRecursive(pastActions[i], $"Past [{i}]");
            }
        }
        else
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("No past actions recorded.");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndScrollView();
    }
    
    private void DisplayActionRecursive(AgentAction action, string label)
    {
        if (action == null) return;

        int key = action.GetHashCode();
        if (!foldoutStates.ContainsKey(key))
        {
            foldoutStates[key] = EditorGUI.indentLevel == 0; 
        }

        GUI.color = stateColors.ContainsKey(action.State) ? stateColors[action.State] : Color.white;
        var foldoutStyle = new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = action is AgentCompositeAction ? FontStyle.Bold : FontStyle.Normal,
            normal = { textColor = GUI.color },
            onNormal = { textColor = GUI.color },
            hover = { textColor = GUI.color },
            onHover = { textColor = GUI.color },
            focused = { textColor = GUI.color },
            onFocused = { textColor = GUI.color },
            active = { textColor = GUI.color },
            onActive = { textColor = GUI.color }
        };
        
        string displayName = $"{label}: {action.GetType().Name} [{action.State}]";
        foldoutStates[key] = EditorGUILayout.Foldout(foldoutStates[key], displayName, true, foldoutStyle);
        GUI.color = Color.white; // Reset color

        if (foldoutStates[key])
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.TextField("Log", action.Log);

            if (action is AgentCompositeAction compositeAction)
            {
                EditorGUILayout.Space(5);
                var currentSubAction = GetPrivateField<AgentAction>(compositeAction, "currentSubAction");
                var subActionsQueue = GetPrivateField<List<AgentAction>>(compositeAction, "subActionsQueue");
                var pastSubActions = GetPrivateField<List<AgentAction>>(compositeAction, "pastSubActions");

                if (currentSubAction != null) DisplayActionRecursive(currentSubAction, "Current Sub-Action");
                
                if (subActionsQueue != null && subActionsQueue.Count > 0)
                {
                    EditorGUILayout.LabelField("Sub-Actions Queue", EditorStyles.miniBoldLabel);
                    foreach (var subAction in subActionsQueue) DisplayActionRecursive(subAction, "Queued Sub-Action");
                }
                
                if (pastSubActions != null && pastSubActions.Count > 0)
                {
                    EditorGUILayout.LabelField("Past Sub-Actions", EditorStyles.miniBoldLabel);
                    foreach (var subAction in pastSubActions) DisplayActionRecursive(subAction, "Past Sub-Action");
                }
                EditorGUILayout.Space(5);
            }
            
            FieldInfo[] fields = action.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.DeclaringType == typeof(AgentAction) || (action is AgentCompositeAction && field.DeclaringType == typeof(AgentCompositeAction)))
                {
                    continue;
                }

                object value = field.GetValue(action);

                // *** CHANGE START ***
                // If the field is another action, display it recursively.
                if (value is AgentAction subActionField)
                {
                    DisplayActionRecursive(subActionField, field.Name);
                }
                // *** CHANGE END ***
                else if (value is Object unityObject)
                {
                    EditorGUILayout.ObjectField(field.Name, unityObject, field.FieldType, true);
                }
                else
                {
                    EditorGUILayout.LabelField(field.Name, value != null ? value.ToString() : "null");
                }
            }
            
            EditorGUI.indentLevel--;
        }
    }

    private T GetPrivateField<T>(object obj, string fieldName) where T : class
    {
        if (obj == null) return null;
        FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(obj) as T;
    }

    void OnInspectorUpdate()
    {
        if (Application.isPlaying)
        {
            // TODO actually always gets it everyframe, a bit expensive 
            var agents = FindObjectsByType<AdamAgent>(FindObjectsSortMode.None);
            
            if(agents != null && agents.Length > 0)
                selectedAgent = agents[0];
            
            Repaint();
        }
    }
}