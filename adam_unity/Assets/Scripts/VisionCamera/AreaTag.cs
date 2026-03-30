using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor; // Required for editor-specific functions
#endif


//[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Collider))]
[Serializable]
[ExecuteInEditMode] // Ensures OnValidate is called reliably, especially on scene load and object creation.
public class AreaTag : MonoBehaviour
{
    // A unique integer identifier for this object instance in the scene.
    // It is serialized to be saved with the scene. We removed HideInInspector so the custom editor can show it.
    [SerializeField]
    private int persistentId = -1;
    public int PersistentId => this.persistentId;
    
    private BoxCollider areaCollider;
    public BoxCollider Collider => areaCollider;

    void Awake()
    {
        // This check prevents editor-only logic from running in a build.
        if (!Application.isEditor)
        {
            // We can destroy this component in a build if it's only for the editor.
            // Or, just let it exist without the editor-only functionality.
            // For this example, we'll let it exist but it won't do the ID check.
        }
    }
    
    void Start()
    {
        areaCollider = gameObject.GetComponent<BoxCollider>();
        
        Assert.IsNotNull(areaCollider);
    }

    public string GetStringId()
    {
        return $"AreaCollider_{persistentId}";
    }

    public static AreaTag GetAreaTag(int reqId)
    {
        // Find all other ObjectTag components in the same scene.
        AreaTag[] allTags = FindObjectsByType<AreaTag>(FindObjectsSortMode.None);
        
        foreach (AreaTag areaTag in allTags)
        {
            if (areaTag.PersistentId == reqId)
                return areaTag;
        }

        Debug.LogError($"No AreaTag found for PersistentId={reqId}");
        return null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// This is an editor-only callback that Unity calls when the script is loaded or a value is changed in the Inspector.
    /// We use it to assign a persistent, unique integer ID.
    /// </summary>
    private void OnValidate()
    {
        // Do nothing if this is a prefab asset in the Project view. We only want IDs on scene instances.
        if (PrefabUtility.IsPartOfPrefabAsset(this))
        {
            persistentId = -1; // Reset ID on the prefab template.
            return;
        }
        
        // If the scene is not yet loaded, we can't search it.
        if (gameObject.scene.rootCount == 0)
        {
            return;
        }

        bool hasDuplicate = false;
        int maxId = -1;

        // Find all other ObjectTag components in the same scene.
        AreaTag[] allTags = FindObjectsByType<AreaTag>(FindObjectsSortMode.None);
        
        foreach (AreaTag other in allTags)
        {
            // Keep track of the highest ID we find.
            if (other.persistentId > maxId)
            {
                maxId = other.persistentId;
            }

            // Check for duplicates, ignoring self.
            if (other != this && other.persistentId == this.persistentId)
            {
                hasDuplicate = true;
            }
        }

        // Assign a new ID if this one is unassigned (-1) or if it's a duplicate.
        // A duplicate can happen when you copy-paste an object.
        if (this.persistentId == -1 || hasDuplicate)
        {
            this.persistentId = maxId + 1;
            // Mark this component as "dirty" to ensure the new ID is saved to the scene file.
            EditorUtility.SetDirty(this);
        }
    }
#endif

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!this.isActiveAndEnabled)
            return;
        
        Color color = Color.cyan;
        
        Gizmos.color = color;
        Gizmos.DrawWireCube(areaCollider.bounds.center, areaCollider.bounds.size);
        
        // Also draw the ID as text above the object in the scene view.
        GUIStyle style = new GUIStyle();
        style.normal.textColor = color;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 16;
        Handles.Label(areaCollider.bounds.center + Vector3.up * areaCollider.bounds.extents.y / 2, GetStringId(), style);
    }
#endif
}