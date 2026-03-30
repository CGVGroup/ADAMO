using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class AdamAgentClient
{
    public static AdamAgentClient Instance;
    
    private readonly string baseUrl;
    public string BaseUrl => baseUrl;
    private readonly HttpClient client;

    [SerializeField]
    private List<ObjectTag> visibleObjects = new List<ObjectTag>();
    [SerializeField]
    private UnityGameObjectData[] visibleObjectsData;

    public AdamAgentClient(string baseUrl)
    {
        //baseUrl = host.TrimEnd('/');
        this.baseUrl = baseUrl;
        client = new HttpClient();
        // Remember to wait indefinitely for the response
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    // public void SetVisibleObjects(List<ObjectTag> visibleObjects)
    // {
    //     this.visibleObjects.Clear();
    //     foreach (ObjectTag obj in visibleObjects)
    //     {
    //         this.visibleObjects.Add(obj);
    //     }
    //
    //     visibleObjectsData = ConstructUnityGameObjectData(this.visibleObjects);
    // }

    // public async Task<string> TextInference(string threadId, string message)
    // {
    //     var payload = new TextInferencePayload
    //     {
    //         thread_id = threadId,
    //         message = message
    //     };
    //
    //     var json = await PostJson("/text_inference", payload);
    //     var parsed = JsonUtility.FromJson<InferenceResponse>(json);
    //     return parsed.response;
    // }
    //
    // public async Task<string> VisionInference(string threadId, string message, Texture2D image)
    // {
    //     string base64Image = Convert.ToBase64String(CameraManager.Instance.EncodeToJPGCustom(image));
    //
    //     var payload = new VisionInferencePayload
    //     {
    //         thread_id = threadId,
    //         message = message,
    //         image_base64 = base64Image
    //     };
    //
    //     var json = await PostJson("/vision_inference", payload);
    //     var parsed = JsonUtility.FromJson<InferenceResponse>(json);
    //     return parsed.response;
    // }

    // public static string VisibleObjectsToJson(List<ObjectTag> visibleObjects)
    // {
    //     var visibleObjsData = ConstructUnityGameObjectData(visibleObjects);
    //     return JsonUtility.ToJson(visibleObjsData);
    // }

    public static UnityGameObjectData[] ConstructUnityGameObjectData(List<ObjectTag> visibleObjects)
    {
        var gameObjectDataList = new UnityGameObjectData[visibleObjects.Count];
        string debugString = "";
        for (int i = 0; i < visibleObjects.Count; i++)
        {
            ObjectTag objTag = visibleObjects[i];
            
            gameObjectDataList[i] = new UnityGameObjectData(objTag, BenchmarkManager.Instance.CurrentRun);

            if (BenchmarkManager.Instance.CurrentRun.coordinatesType == CoordinatesType.ABS)
                debugString += objTag.GetStringId() + " " + objTag.CenterPosition + "\n";
            else
                debugString += objTag.GetStringId() + " " + CameraManager.Instance.GetLastCaptureRelativePosition(objTag.CenterPosition) + "\n";
        }
        //Debug.Log(debugString);

        return gameObjectDataList;
    }
    
    public static UnityGameObjectData[] ConstructUnityGameObjectData(List<SpatialPointTag> spatialPoints)
    {
        var gameObjectDataList = new UnityGameObjectData[spatialPoints.Count];
        string debugString = "";
        for (int i = 0; i < spatialPoints.Count; i++)
        {
            SpatialPointTag pointTag = spatialPoints[i];
            
            gameObjectDataList[i] = new UnityGameObjectData(pointTag, BenchmarkManager.Instance.CurrentRun);

            if (BenchmarkManager.Instance.CurrentRun.coordinatesType == CoordinatesType.ABS)
                debugString += pointTag.Id + " " + pointTag.Id + "\n";
            else
                debugString += pointTag.Id + " " + CameraManager.Instance.GetLastCaptureRelativePosition(pointTag.transform.position) + "\n";
        }
        //Debug.Log(debugString);

        return gameObjectDataList;
    }

    public async Task<string> AgentInference(string threadId,string message,Texture2D image, List<SpatialPointTag> spatialPoints,Vector3 agentLocation)
    {
        var spatialPointsData = ConstructUnityGameObjectData(spatialPoints);
        string base64Image = Convert.ToBase64String(CameraManager.Instance.EncodeToJPGCustom(image));
        //SetVisibleObjects(CameraManager.Instance.GetVisibleObjects());
        var visibleObjsData = ConstructUnityGameObjectData(CameraManager.Instance.GetVisibleObjects());
        
        var payload = new AgentInferencePayload
        {
            thread_id = threadId,
            message = message,
            image_base64 = base64Image,
            agent_location = new Vector3Serializable(agentLocation),
            game_objects = visibleObjsData,
            spatial_points = spatialPointsData
        };

        var json = await PostJson("/agent_inference", payload);
        var parsed = JsonUtility.FromJson<InferenceResponse>(json);
        
        return parsed.response;
    }

    public async Task<string[]> GetThreads()
    {
        var response = await client.GetAsync($"{baseUrl}/get_threads");
        string json = await response.Content.ReadAsStringAsync();

        // Unity JsonUtility richiede che l'array sia campo di una classe
        ThreadsResponse parsed = JsonUtility.FromJson<ThreadsResponse>(json);
        return parsed.threads;
    }

    public async Task<string> DeleteThread(string threadId)
    {
        var uri = $"{baseUrl}/delete_thread?thread_id={Uri.EscapeDataString(threadId)}";
        var response = await client.GetAsync(uri);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> Logout()
    {
        var response = await client.GetAsync($"{baseUrl}/logout");
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> PostJson(string path, object data)
    {
        string json = JsonUtility.ToJson(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{baseUrl}{path}", content);
        
        if(response.StatusCode != HttpStatusCode.OK)
            Debug.LogError($"Inference Failed with status code {response.StatusCode}\n\n{response.ReasonPhrase}");
        
        return await response.Content.ReadAsStringAsync();
    }
}
