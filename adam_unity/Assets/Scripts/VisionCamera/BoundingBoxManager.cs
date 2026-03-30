using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Assertions;

[RequireComponent(typeof(Camera))]
public class BoundingBoxManager : MonoBehaviour
{
    // --- HELPER CLASS TO HOLD TARGET DATA ---
    private class TrackedTarget
    {
        public ObjectTag ObjectTag;
        public RectTransform VisualizerRect;
        public TextMeshProUGUI IDLabel;
        public Renderer TargetRenderer;
        public Collider TargetCollider;
    }

    [Header("Setup")]
    [Tooltip("The UI Panel prefab used to visualize the bounding box.")]
    public GameObject boundingBoxPrefab;
    [Tooltip("The canvas that will hold the bounding box visualizers.")]
    public Canvas parentCanvas;

    [Header("Features")]
    [Tooltip("If enabled, uses Raycasting to hide boxes for objects blocked by other geometry.")]
    public bool checkOcclusion = true;
    [Tooltip("Set this to the layer(s) that contain your occluding objects (e.g., walls, environment).")]
    public LayerMask occlusionLayers;

    // --- UPDATED LIST TO USE THE HELPER CLASS ---
    private readonly List<TrackedTarget> _trackedTargets = new List<TrackedTarget>();
    private Camera _screenCamera;

    private void Awake()
    {
        _screenCamera = this.GetComponent<Camera>();
        
        Assert.IsNotNull(parentCanvas);
        
        if (parentCanvas == null || boundingBoxPrefab == null)
        {
            Debug.LogError("BoundingBoxManager is missing required references and will be disabled.");
            enabled = false;
            return;
        }

        ObjectTag[] targets = FindObjectsByType<ObjectTag>(FindObjectsSortMode.None);

        foreach (var target in targets)
        {
            Renderer targetRenderer = target.GetComponentInChildren<Renderer>();
            Collider targetCollider = target.GetComponentInChildren<Collider>(); // Find the collider

            if (targetRenderer == null)
            {
                Debug.LogWarning($"Object '{target.gameObject.name}' has an ObjectTag but no Renderer. Skipping.");
                continue;
            }

            GameObject visualizerInstance = Instantiate(boundingBoxPrefab, parentCanvas.transform);
            visualizerInstance.name = $"BBox_{target.PersistentId}_{target.type}";
            TextMeshProUGUI visualizerText = visualizerInstance.GetComponentInChildren<TextMeshProUGUI>();
            
            if(BenchmarkManager.Instance.CurrentRun.objectIdentifier == ObjectIdentifier.OPAQ)
                visualizerText.text = $"{target.PersistentId}";
            else
                visualizerText.text = $"{target.type}_{target.PersistentId}";
            
            RectTransform visualizerRectTransform = visualizerInstance.GetComponent<RectTransform>();

            // Add all info to our list
            _trackedTargets.Add(new TrackedTarget
            {
                ObjectTag = target,
                TargetRenderer = targetRenderer,
                IDLabel = visualizerText,
                TargetCollider = targetCollider, // Store the collider
                VisualizerRect = visualizerRectTransform
            });
        }
    }

    private void Start()
    {
        switch (BenchmarkManager.Instance.CurrentRun.objectIdentifier)
        {
            case ObjectIdentifier.SEM:
            case ObjectIdentifier.OPAQ:
                boundingBoxPrefab.gameObject.GetComponentInChildren<TextMeshProUGUI>().enabled = true;
                break;
            default:
                boundingBoxPrefab.gameObject.GetComponentInChildren<TextMeshProUGUI>().enabled = false;
                break;
        }
    }

    private void LateUpdate()
    {
        // Update the global visible objects list for this camera
        // ObjectVisibilityUtility.GetVisibleObjects(_screenCamera, occlusionLayers, checkOcclusion);
        
        for (int i = _trackedTargets.Count - 1; i >= 0; i--)
        {
            TrackedTarget target = _trackedTargets[i];

            if (target.TargetRenderer == null)
            {
                if (target.VisualizerRect != null) Destroy(target.VisualizerRect.gameObject);
                _trackedTargets.RemoveAt(i);
                continue;
            }

            UpdateVisualizerBounds(target);
        }
    }

    private void UpdateVisualizerBounds(TrackedTarget target)
    {
        // Use the utility for visibility
        bool isVisible = CameraManager.Instance.IsVisible(target.ObjectTag);
        if (!isVisible)
        {
            target.VisualizerRect.gameObject.SetActive(false);
            return;
        }

        // If visible, update the bounding box UI as before
        Bounds bounds = target.TargetRenderer.bounds;
        Vector3[] corners = {
            bounds.min, bounds.max, new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.min.z), new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.max.z), new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.min.z)
        };

        Vector2 minScreenPoint = Vector2.positiveInfinity;
        Vector2 maxScreenPoint = Vector2.negativeInfinity;
        foreach (var corner in corners)
        {
            Vector3 screenPoint = _screenCamera.WorldToScreenPoint(corner);
            if (screenPoint.z > 0)
            {
                minScreenPoint = Vector2.Min(minScreenPoint, screenPoint);
                maxScreenPoint = Vector2.Max(maxScreenPoint, screenPoint);
            }
        }

        target.VisualizerRect.gameObject.SetActive(true);

        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, minScreenPoint, parentCanvas.worldCamera, out Vector2 localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, maxScreenPoint, parentCanvas.worldCamera, out Vector2 localMax);

        target.VisualizerRect.anchoredPosition = (localMin + localMax) / 2f;
        target.VisualizerRect.sizeDelta = localMax - localMin;
    }
}