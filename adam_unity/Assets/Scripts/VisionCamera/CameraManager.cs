// CameraVisibilityManager.cs

using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Assertions;

[RequireComponent(typeof(Camera))]
public class CameraManager : MonoBehaviour
{
    static CameraManager mInstance;
    public static CameraManager Instance => mInstance;
    
    public Camera cam;
    public List<ObjectTag> visibleObjs = new List<ObjectTag>();
    public List<ObjectTag> allObjs = new List<ObjectTag>();
    
    public LayerMask occlusionLayers;

    [SerializeField] private bool enableDebugging = false;
    [SerializeField] private Transform lastCameraPov;
    
    private ScreenGridRaycaster cameraRaycaster;
    public List<SpatialPointTag> spatialPoints = new List<SpatialPointTag>();
    
    void Awake()
    {
        if(mInstance == null)
            mInstance = this;
        else
            Destroy(this);
        
        cam = GetComponent<Camera>(); 
        
        allObjs = new List<ObjectTag>(FindObjectsByType<ObjectTag>(FindObjectsSortMode.None));
    }

    private void Start()
    {
        lastCameraPov = GameObject.Find("CapturePOV_Transform").transform;
        Assert.IsNotNull(lastCameraPov);
        
        cameraRaycaster = GetComponent<ScreenGridRaycaster>();
        Assert.IsNotNull(cameraRaycaster);
    }

    // Using LateUpdate to ensure it runs after OnWillRenderObject calls
    void LateUpdate()
    {
        if(enableDebugging)
            foreach (var obj in allObjs)
            {
                if (IsVisible(obj))
                    Debug.DrawLine(cam.transform.position, obj.CenterPosition, Color.green);
                else
                    if(IsInFrustum(obj))
                        Debug.DrawLine(cam.transform.position, obj.CenterPosition, Color.red);
            } 
    }

    public Texture2D Capture()
    {
        int width = 1920;
        int height = 1080;
        RenderTexture rt = new RenderTexture(width, height, 24);
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        cam.targetTexture = rt;
        RenderTexture.active = rt;

        cam.Render();
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        
        cam.targetTexture = null;
        RenderTexture.active = null;
        
        // Save Camera transforms when capturing the screen
        SetLastCameraPovToCurrentCameraTransform();

        // Save image to FileSystem
        SaveImageToFileSystem(
            tex, 
            BenchmarkManager.Instance.CurrentRunFolderName, 
            (BenchmarkManager.Instance.CurrentRun.repIndex + 1).ToString());
        
        return tex;
    }

    public void ProjectScreenGrid(out List<SpatialPointTag> spatialPoints)
    {
        spatialPoints = cameraRaycaster.ProjectGridWithTemporaryColliders();
    }

    public void ClearScreenGrid()
    {
        cameraRaycaster.ClearPreviousResults();
    }

    public void SaveImageToFileSystem(Texture2D tex, string folderPath, string imageId)
    {
        // Converte in JPG
        byte[] bytes = EncodeToJPGCustom(tex);
        
        string imageFolderPath = Path.Combine(folderPath, "CameraCaptures/");
        
        // Path di salvataggio (es. nella cartella persistente dell'app)
        string imageRelPath = Path.Combine(imageFolderPath,
            $"rep{imageId}_{System.DateTime.Now.ToString("[MM-dd][hh-mm-ss]")}.jpg");
        //Debug.Log(imageRelPath);
        
        // Path di salvataggio (es. nella cartella persistente dell'app)
        string path = Path.Combine(Application.dataPath, imageRelPath);
        
        //Debug.Log(path);
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        File.WriteAllBytes(path, bytes);
        
        //Debug.Log($"Screenshot salvato in: {path}");
    }

    // public void CaptureBase64(out string base64, out List<ObjectTag> capturedObjs)
    // {
    //     Texture2D tex = Capture();
    //     
    //     base64 = Convert.ToBase64String(tex.EncodeToJPG());
    //     capturedObjs = GetVisibleObjects();
    // }
    
    // public void CaptureBase64(out string base64, out UnityGameObjectData[] capturedObjsData)
    // {
    //     Texture2D tex = Capture();
    //     
    //     base64 = Convert.ToBase64String(tex.EncodeToJPG());
    //     capturedObjsData = AdamAgentClient.ConstructUnityGameObjectData(GetVisibleObjects());
    // }
    //
    public void CaptureBase64(out string base64, out List<UnityGameObjectData> capturedObjsData)
    {
        Texture2D tex = Capture();
        
        base64 = Convert.ToBase64String(EncodeToJPGCustom(tex));
        capturedObjsData = new List<UnityGameObjectData>(AdamAgentClient.ConstructUnityGameObjectData(GetVisibleObjects()));
        
        //Set where it took the screenshot
        //SetLastCameraPovToCurrentCameraTransform();
    }

    public byte[] EncodeToJPGCustom(Texture2D tex, int quality=100)
    {
        return tex.EncodeToJPG(quality);
    }
    
    /// <summary>
    /// Updates the list of visible ObjectTag objects for the given camera.
    /// </summary>
    public List<ObjectTag> GetVisibleObjects(bool checkOcclusion = true)
    {
        List<ObjectTag> _visibleObjects = new List<ObjectTag>();
        
        ObjectTag[] allTags = UnityEngine.Object.FindObjectsByType<ObjectTag>(FindObjectsSortMode.None);
        
        foreach (var obj in allTags)
        {
            if (IsVisible(obj))
            {
                _visibleObjects.Add(obj);
            }
        }

        return _visibleObjects;
    }

    /// <summary>
    /// Checks if the given ObjectTag is visible from the camera (in frustum and not occluded).
    /// </summary>
    public bool IsVisible(ObjectTag obj, bool checkOcclusion = true)
    {
        if(checkOcclusion)
            return (!IsOccluded(obj) && IsInFrustum(obj));
        else
        {
            return IsInFrustum(obj);
        }
    }

    public bool IsOccluded(ObjectTag obj)
    {
        var objRenderer = obj.GetComponentInChildren<Renderer>();
        var objCollider = obj.GetComponentInChildren<Collider>();
        
        Bounds bounds = objRenderer.bounds;
        
        // Occlusion check
        Vector3 origin = cam.transform.position;
        Vector3 targetCenter = bounds.center;
        Vector3 direction = targetCenter - origin;
        float distance = direction.magnitude;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, occlusionLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != objCollider)
                return true; // Occluded by something else
        }
        return false;
    }

    public bool IsInFrustum(ObjectTag obj)
    {
        var objRenderer = obj.GetComponentInChildren<Renderer>();
        
        //Frustum check
        // Get the six camera frustum planes
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        Bounds bounds = objRenderer.bounds;

        // Check if the bounds do not intersects the frustum planes
        if (! GeometryUtility.TestPlanesAABB(planes, bounds))
            // The object is outside the frustum
            return false;
        else
            return true;
    }

    public void SetLastCameraPovToCurrentCameraTransform()
    {
        lastCameraPov.transform.position = cam.transform.position;
        lastCameraPov.transform.rotation = cam.transform.rotation;
    }
    
    public Vector3 GetLastCaptureRelativePosition(Vector3 globalPointPos)
    {
        return mInstance.lastCameraPov.InverseTransformPoint(globalPointPos);
    }

    public Vector3 GetLastCaptureGlobalPosition(Vector3 localPointPos)
    {
        return mInstance.lastCameraPov.TransformPoint(localPointPos);
    }
}