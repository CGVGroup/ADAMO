using UnityEditor;

#if UNITY_EDITOR
/// <summary>
/// Custom editor for the ObjectTag component.
/// This makes the 'sceneId' field visible but read-only in the Inspector.
/// </summary>
[CustomEditor(typeof(ObjectTag))]
public class ObjectTagEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Update the serializedObject representation to ensure it's in sync.
        serializedObject.Update();

        // Draw the 'type' property, which will remain editable.
        EditorGUILayout.PropertyField(serializedObject.FindProperty("type"));

        // Begin a disabled group. UI elements inside this group will be non-interactive.
        EditorGUI.BeginDisabledGroup(true);
        // Draw the 'sceneId' property. Because it's in a disabled group, it will be read-only.
        EditorGUILayout.PropertyField(serializedObject.FindProperty("persistentId"));
        // Draw the 'volume' property. Because it's in a disabled group, it will be read-only.
        EditorGUILayout.PropertyField(serializedObject.FindProperty("volume"));
        // End the disabled group.
        EditorGUI.EndDisabledGroup();

        // Apply any changes made in the inspector back to the serialized object.
        serializedObject.ApplyModifiedProperties();
    }
}
#endif