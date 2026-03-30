using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
/// <summary>
/// An editor window to manage all ObjectTag components in the current scene.
/// It allows for viewing, bulk editing, and removing components with undo/redo support.
/// </summary>
public class ObjectTagManagerWindow : EditorWindow
{
    private List<ObjectTag> sceneTags = new List<ObjectTag>();
    private List<bool> selection = new List<bool>();
    private Vector2 scrollPosition;
    
    private bool selectAll;

    // Add a menu item to open this window
    [MenuItem("Tools/Object Tag Manager")]
    public static void ShowWindow()
    {
        GetWindow<ObjectTagManagerWindow>("Object Tag Manager");
    }

    /// <summary>
    /// Called when the window is enabled or the project structure changes.
    /// </summary>
    private void OnEnable()
    {
        RefreshTagList();
    }

    /// <summary>
    /// Called when the window gains focus. Good for refreshing data.
    /// </summary>
    private void OnFocus()
    {
        RefreshTagList();
    }

    /// <summary>
    /// Finds all active ObjectTag components in the scene and rebuilds the list.
    /// </summary>
    private void RefreshTagList()
    {
        sceneTags = FindObjectsOfType<ObjectTag>().OrderBy(t => t.gameObject.name).ToList();
        selection = new List<bool>(new bool[sceneTags.Count]);
        selectAll = false;
        Repaint(); // Redraw the window
    }

    /// <summary>
    /// Main GUI loop for the window.
    /// </summary>
    private void OnGUI()
    {
        DrawToolbar();
        DrawHeader();
        DrawTagList();
    }

    /// <summary>
    /// Draws the top toolbar with a refresh button.
    /// </summary>
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
        {
            RefreshTagList();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws the header row for the table.
    /// </summary>
    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        
        // "Select All" toggle
        EditorGUI.BeginChangeCheck();
        selectAll = EditorGUILayout.Toggle(selectAll, GUILayout.Width(20));
        if (EditorGUI.EndChangeCheck())
        {
            for (int i = 0; i < selection.Count; i++)
            {
                selection[i] = selectAll;
            }
        }

        GUILayout.Label("GameObject", EditorStyles.boldLabel);
        GUILayout.Label("Type", EditorStyles.boldLabel, GUILayout.Width(120));
        GUILayout.Label("ID", EditorStyles.boldLabel, GUILayout.Width(40));
        GUILayout.Label("Enabled", EditorStyles.boldLabel, GUILayout.Width(60));
        GUILayout.Label("Actions", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws the main scrollable list of ObjectTags.
    /// </summary>
    private void DrawTagList()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < sceneTags.Count; i++)
        {
            ObjectTag tag = sceneTags[i];
            if (tag == null) continue; // Skip if the object was deleted

            EditorGUILayout.BeginHorizontal();

            // Selection toggle
            selection[i] = EditorGUILayout.Toggle(selection[i], GUILayout.Width(20));

            // --- GameObject Name (Clickable to ping) ---
            if (GUILayout.Button(tag.gameObject.name, EditorStyles.label))
            {
                EditorGUIUtility.PingObject(tag.gameObject);
            }

            // Get all selected tags for bulk editing logic
            var selectedTags = GetSelectedTags();

            // --- Object Type Dropdown ---
            DrawTypePopup(tag, selectedTags);

            // --- ID (Read-only) ---
            EditorGUILayout.LabelField(tag.PersistentId.ToString(), GUILayout.Width(40));

            // --- Enabled Toggle ---
            DrawEnabledToggle(tag, selectedTags);

            // --- Delete Button ---
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Delete Component?", $"Are you sure you want to remove the ObjectTag from '{tag.gameObject.name}'?", "Yes", "No"))
                {
                    // Using Undo.DestroyObjectImmediate makes the action undoable
                    Undo.DestroyObjectImmediate(tag);
                    RefreshTagList(); // Rebuild the list after deletion
                    GUIUtility.ExitGUI(); // Exit GUI to prevent errors after list modification
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTypePopup(ObjectTag currentTag, List<ObjectTag> selectedTags)
    {
        EditorGUI.BeginChangeCheck();
        
        ObjectType currentType = currentTag.type;
        bool isMultiEditing = selectedTags.Count > 1 && selection[sceneTags.IndexOf(currentTag)];
        
        // Show mixed value if selected types are different
        if (isMultiEditing)
        {
            ObjectType firstType = selectedTags[0].type;
            if (selectedTags.Any(t => t.type != firstType))
            {
                EditorGUI.showMixedValue = true;
            }
        }

        ObjectType newType = (ObjectType)EditorGUILayout.EnumPopup(currentType, GUILayout.Width(120));
        EditorGUI.showMixedValue = false; // Always reset after drawing

        if (EditorGUI.EndChangeCheck())
        {
            if (isMultiEditing)
            {
                // Apply to all selected items
                Undo.RecordObjects(selectedTags.ToArray(), "Bulk Change Object Type");
                foreach (var selectedTag in selectedTags)
                {
                    selectedTag.type = newType;
                }
            }
            else
            {
                // Apply to just the one
                Undo.RecordObject(currentTag, "Change Object Type");
                currentTag.type = newType;
            }
        }
    }

    private void DrawEnabledToggle(ObjectTag currentTag, List<ObjectTag> selectedTags)
    {
        EditorGUI.BeginChangeCheck();
        
        bool isEnabled = currentTag.enabled;
        bool isMultiEditing = selectedTags.Count > 1 && selection[sceneTags.IndexOf(currentTag)];

        // Show mixed value if enabled states are different
        if (isMultiEditing)
        {
            bool firstEnabled = selectedTags[0].enabled;
            if (selectedTags.Any(t => t.enabled != firstEnabled))
            {
                EditorGUI.showMixedValue = true;
            }
        }

        bool newEnabledState = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(60));
        EditorGUI.showMixedValue = false; // Always reset

        if (EditorGUI.EndChangeCheck())
        {
            if (isMultiEditing)
            {
                // Apply to all selected items
                Undo.RecordObjects(selectedTags.ToArray(), "Bulk Change Enabled State");
                foreach (var selectedTag in selectedTags)
                {
                    selectedTag.enabled = newEnabledState;
                }
            }
            else
            {
                // Apply to just the one
                Undo.RecordObject(currentTag, "Change Enabled State");
                currentTag.enabled = newEnabledState;
            }
        }
    }

    /// <summary>
    /// Helper method to get a list of the currently selected ObjectTags.
    /// </summary>
    private List<ObjectTag> GetSelectedTags()
    {
        var selected = new List<ObjectTag>();
        for (int i = 0; i < sceneTags.Count; i++)
        {
            if (selection[i] && sceneTags[i] != null)
            {
                selected.Add(sceneTags[i]);
            }
        }
        return selected;
    }
}
#endif
