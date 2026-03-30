using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor; // Required for editor-specific functions
#endif

[Serializable]
public enum ObjectType
{
    Other,
    Door,
    Vase,
    Chair,
    Table,
    Bench,
    Environment,
    Bottle,
    Glass,
    Battery,
    IndoorPlant,
    Cup,
    Tape,
    Binoculars,
    Plate,
    PeanutButter,
    Flashlight,
    Screwdriver,
    Pills,
    Lock,
    Painkiller,
    TomatoSoup,
    WineBottle,
    Key,
    Lighter


}

//[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Pickable))]
[RequireComponent(typeof(Renderer))]
[Serializable]
[ExecuteInEditMode] // Ensures OnValidate is called reliably, especially on scene load and object creation.
public class ObjectTag : MonoBehaviour
{
    public ObjectType type = ObjectType.Other;

    // A unique integer identifier for this object instance in the scene.
    // It is serialized to be saved with the scene.
    [SerializeField]
    private int persistentId = -1;
    public int PersistentId => this.persistentId;

    private CameraManager m_cameraManager;
    private Renderer objectRenderer;
    private Collider objectCollider;
    public Collider ObjectCollider => objectCollider;
    
    public Vector3 CenterPosition => objectRenderer.bounds.center;
    public float Volume => volume;
    
    [SerializeField]
    private float volume;

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
        objectRenderer = gameObject.GetComponent<Renderer>();
        objectCollider = gameObject.GetComponent<Collider>();
        
        Assert.IsNotNull(objectRenderer);
        Assert.IsNotNull(objectCollider);
        
        if (Application.isPlaying)
        {
            m_cameraManager = CameraManager.Instance;
        }

        volume = MeshGeometryUtility.BoundingBoxVolumeOfMeshRenderer(objectRenderer);
        
        if(volume == -1f)
            Debug.LogWarning($"{this} Cannot calculate volume. The mesh is not watertight!");
    }

    public string GetStringId()
    {
        return $"{type}_{persistentId}";
    }

    public static ObjectTag GetObjectTagFromStringID(string stringId)
    {
        int reqId;
        if (BenchmarkManager.Instance.CurrentRun.objectIdentifier == ObjectIdentifier.SEM)
        {
            var splits = stringId.Split("_");
            if(splits.Length == 2)
                reqId = int.Parse(splits[1]);
            else
                reqId = int.Parse(splits[0]);
        }
        else
            reqId = int.Parse(stringId);
        
        return GetObjectTagFromPersistentID(reqId);
    }
    
    private static ObjectTag GetObjectTagFromPersistentID(int persistentId)
    {
        // Find all other ObjectTag components in the same scene.
        ObjectTag[] allTags = FindObjectsByType<ObjectTag>(FindObjectsSortMode.None);
        
        foreach (ObjectTag objTag in allTags)
            if (objTag.PersistentId == persistentId)
                return objTag;
        
        Debug.LogError($"No ObjectTag found for PersistentId={persistentId}");
        return null;
    }

    void OnWillRenderObject()
    {
        // This logic is for runtime, so it's fine as is.
        if (Application.isPlaying && m_cameraManager != null && Camera.current == m_cameraManager.cam)
        {
            if (!m_cameraManager.visibleObjs.Contains(this))
            {
                m_cameraManager.visibleObjs.Add(this);
            }
        }
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
        ObjectTag[] allTags = FindObjectsByType<ObjectTag>(FindObjectsSortMode.None);
        
        foreach (ObjectTag other in allTags)
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
        
        if (objectRenderer == null)
        {
            objectRenderer = GetComponent<Renderer>();
        }

        Color color = Color.red;

        if (CameraManager.Instance != null && CameraManager.Instance.IsVisible(this))
            color = Color.green;
        
        Gizmos.color = color;
        Gizmos.DrawSphere(objectRenderer.bounds.center, 0.01f);
        Gizmos.DrawWireCube(objectRenderer.bounds.center, objectRenderer.bounds.size);
        
        // Also draw the ID as text above the object in the scene view.
        GUIStyle style = new GUIStyle();
        style.normal.textColor = color;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 16;
        Handles.Label(objectRenderer.bounds.center + Vector3.up * objectRenderer.bounds.extents.y / 2, $"{persistentId}\n{type}", style);
    }
#endif
}