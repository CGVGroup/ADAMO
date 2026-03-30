using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ScreenGridRaycaster: MonoBehaviour
{
    /// <summary>
    /// Helper class to link a 3D world object to its corresponding UI element.
    /// This makes updating positions much cleaner and more reliable.
    /// </summary>
    private class UIPointMapping
    {
        public int ID;
        public Transform WorldPointTransform;
        public RectTransform LabelRectTransform;
    }
    
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 16 * 3;
    [SerializeField] private int gridHeight = 9 * 3;

    [Header("Raycast Settings")]
    private Camera projectionCamera;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private float maxDistance = 100f;
    
    [Header("UI Label Settings")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameObject uiLabelPrefab;
    [SerializeField] private GameObject spatialPointPrefab;
    [SerializeField] private float minLabelScale = 0.5f;
    [SerializeField] private float maxLabelScale = 1.5f;
    [SerializeField] private float maxScaleDistance = 50f;
    
    // This new list holds the links between 3D spheres and 2D labels.
    private readonly List<UIPointMapping> pointMappings = new List<UIPointMapping>();

    void Awake()
    {
        if (projectionCamera == null) projectionCamera = Camera.main;
    }

    private void Start()
    {
        if (!TryGetComponent<Camera>(out projectionCamera))
        {
            projectionCamera = Camera.main;
        }
        Assert.IsNotNull(projectionCamera, "A Camera is required for ScreenGridRaycaster.");
        Assert.IsNotNull(targetCanvas, "Target Canvas is not set.");
        Assert.IsNotNull(uiLabelPrefab, "UI Label Prefab is not set.");
        Assert.IsNotNull(spatialPointPrefab, "Spatial Point Prefab is not set.");
        
        Assert.IsNotNull(spatialPointPrefab.GetComponentInChildren<SpatialPointTag>() , "Spatial Point Prefab has no SpatialPointTag!");
        
        if (targetCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            Assert.IsNotNull(targetCanvas.worldCamera, "The Target Canvas is set to 'Screen Space - Camera' but its 'Render Camera' is not assigned.");
        }
    }
    
    /// <summary>
    /// The Update loop is now used to continuously reposition the UI labels.
    /// </summary>
    void Update()
    {
        UpdateLabelPositions();
    }

    public List<SpatialPointTag> ProjectGridWithTemporaryColliders()
    {
        Plane[] cameraFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(projectionCamera);
        List<Collider> tempColliders = new List<Collider>();
        var allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var renderer in allRenderers)
        {
            if (GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, renderer.bounds))
            {
                if (renderer.GetComponent<Collider>() == null)
                {
                    // Get the mesh from the MeshFilter component.
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;

                    if (mesh != null)
                    {
                        // THIS IS THE FIX: A dummy access to the mesh data.
                        // This line forces Unity to include the readable mesh data in the build,
                        // even though we don't use the 'dummy' variable for anything.
                        int dummy = mesh.vertexCount;

                        // Now, this line will work correctly in the build.
                        MeshCollider meshCollider = renderer.gameObject.AddComponent<MeshCollider>();
                        tempColliders.Add(meshCollider);
                    }
                }
            }
        }
        
        ClearPreviousResults();
        
        int pointId = 0;
        for (int i = 0; i < gridWidth; i++)
        {
            for (int j = 0; j < gridHeight; j++)
            {
                float screenX = (float)(i+0.5f) / (gridWidth) * Screen.width;
                float screenY = (float)(j+0.5f) / (gridHeight) * Screen.height;
                Vector2 screenPos = new Vector2(screenX, screenY);
                Ray ray = projectionCamera.ScreenPointToRay(screenPos);

                if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, ~layerMask))
                {
                    // --- Instantiation logic is now combined here ---
                    
                    // 1. Create the 3D sphere
                    GameObject sphereInstance = Instantiate(spatialPointPrefab, hit.point, Quaternion.identity);
                    sphereInstance.GetComponentInChildren<SpatialPointTag>().SetId(pointId);

                    // 2. Create the 2D UI Label
                    GameObject labelInstance = Instantiate(uiLabelPrefab, targetCanvas.transform);
                    RectTransform rectTransform = labelInstance.GetComponent<RectTransform>();
                    TextMeshProUGUI textComponent = labelInstance.GetComponentInChildren<TextMeshProUGUI>();
                    if (textComponent != null)
                    {
                        textComponent.text = "P" + pointId.ToString();
                    }

                    // 3. Create the mapping to link them
                    pointMappings.Add(new UIPointMapping
                    {
                        ID = pointId,
                        WorldPointTransform = sphereInstance.transform,
                        LabelRectTransform = rectTransform
                    });
                    
                    pointId++;
                }
            }
        }
        
        // Initial position update
        UpdateLabelPositions();

        Debug.Log($"Projection complete. Found {pointMappings.Count} valid points.");
        
        foreach (var collider in tempColliders)
        {
            Destroy(collider);
        }

        return pointMappings.Select(mapping => mapping.WorldPointTransform.GetComponentInChildren<SpatialPointTag>()).ToList();
    }
    
    /// <summary>
    /// NEW: This method runs every frame to update the UI labels' positions.
    /// </summary>
    private void UpdateLabelPositions()
    {
        if (pointMappings.Count == 0) return;

        foreach (var mapping in pointMappings)
        {
            // Get the 3D position of the sphere
            Vector3 worldPosition = mapping.WorldPointTransform.position;
            
            // Convert world position to screen position
            Vector3 screenPoint = projectionCamera.WorldToScreenPoint(worldPosition, Camera.MonoOrStereoscopicEye.Mono);

            // Hide the label if the point is behind the camera
            bool isVisible = screenPoint.z > 0;
            mapping.LabelRectTransform.gameObject.SetActive(isVisible);

            if (isVisible)
            {
                // Convert the screen point to a local position within the Canvas's RectTransform
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetCanvas.GetComponent<RectTransform>(), 
                    screenPoint, 
                    targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera, 
                    out localPoint
                );

                // Update the label's position
                mapping.LabelRectTransform.anchoredPosition = localPoint;
                
                // Update the label's scale based on distance
                float distance = Vector3.Distance(projectionCamera.transform.position, worldPosition);
                float t = Mathf.InverseLerp(maxScaleDistance, 0, distance);
                float scale = Mathf.Lerp(minLabelScale, maxLabelScale, t);
                mapping.LabelRectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }

    /// <summary>
    /// Updated to use the new pointMappings list.
    /// </summary>
    public void ClearPreviousResults()
    {
        foreach (var mapping in pointMappings)
        {
            if (mapping.LabelRectTransform != null)
                DestroyImmediate(mapping.LabelRectTransform.gameObject);
            if (mapping.WorldPointTransform != null)
                DestroyImmediate(mapping.WorldPointTransform.gameObject);
        }
        pointMappings.Clear();
    }
}