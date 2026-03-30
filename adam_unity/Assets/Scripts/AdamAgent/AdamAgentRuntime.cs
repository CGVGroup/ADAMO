using UnityEngine;
using System.Threading.Tasks;
using System.Collections;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using Debug = UnityEngine.Debug;

public class AdamAgentRuntime : MonoBehaviour
{
    static AdamAgentRuntime mInstance = null;
    
    public string pythonServerAddress = "127.0.0.1";
    //public int pythonServerPort = 50000;
    public string threadId = "0";
    public string inputText = "Ciao Adam!";

    [SerializeField] 
    private AdamAgentClient client;
    //private bool isBusy = false;
    
    private Texture2D m_capturedImage;
    
    public static AdamAgentRuntime Instance => mInstance;

    public event Action OnResponseReceived;

    public void Awake()
    {
        if(mInstance == null)
            mInstance = this;
        else
        {
            Debug.LogError("More than one instance of AdamAgentRuntime are present in current scene!");
            Destroy(this);
        }
        
        client = new AdamAgentClient($"http://{pythonServerAddress}:{BenchmarkManager.Instance.AgentPort}");
    }

    public Task StartAgent()
    {
        //Debug.Log($"Sending Prompt to initiate Task... to {client.BaseUrl} \n Prompt : \"{BenchmarkManager.Instance.CurrentRun.taskPrompt}\" ");
        Debug.Log($"Sending Prompt to initiate Task: \"{BenchmarkManager.Instance.CurrentRun.taskPrompt}\" ");
        return SendAgentInference(BenchmarkManager.Instance.CurrentRun.ThreadId, BenchmarkManager.Instance.CurrentRun.taskPrompt);
    }

    IEnumerator Run(Func<Task> action)
    {
        //isBusy = true;
        yield return action().AsCoroutine();
        //isBusy = false;
    }

    public async Task SendAgentInference(
        string threadIdOverride = null,
        string inputTextOverride = null,
        Vector3? agentPositionOverride = null
    )
    {
        Stopwatch sw = Stopwatch.StartNew();
        //Debug.Log("Preparing agent inference...");
        
        // perform RayCast and Capture
        List<SpatialPointTag> spatialPoints;
        CameraManager.Instance.ProjectScreenGrid(out spatialPoints);
        m_capturedImage = CameraManager.Instance.Capture();

        // Se sono stati passati parametri, usali. Altrimenti usa quelli di default della classe.
        string effectiveThreadId = !string.IsNullOrEmpty(threadIdOverride) ? threadIdOverride : threadId;
        string effectiveInputText = !string.IsNullOrEmpty(inputTextOverride) ? inputTextOverride : inputText;
        Vector3 effectiveAgentPosition = agentPositionOverride ?? transform.position;

        var result = await client.AgentInference(
            effectiveThreadId,
            effectiveInputText,
            m_capturedImage,
            spatialPoints,
            effectiveAgentPosition
        );

        OnResponseReceived?.Invoke();
        
        sw.Stop();
        Debug.Log($"Agent inference completed\nResult: \"{result}\"\nTime = {sw.ElapsedMilliseconds} ms");
    }

    // Texture2D CaptureFromCamera()
    // {
    //     //save visible objects when the screen was captured
    //     List<ObjectTag> capturedObjects = CameraManager.Instance.GetVisibleObjects();
    //     client.SetVisibleObjects(capturedObjects);
    //     AdamAgentClient.ConstructUnityGameObjectData(capturedObjects);
    //     return CameraManager.Instance.Capture();
    // }
}

public static class TaskExtensions
{
    public static IEnumerator AsCoroutine(this Task task)
    {
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
            Debug.LogException(task.Exception);
    }
}
