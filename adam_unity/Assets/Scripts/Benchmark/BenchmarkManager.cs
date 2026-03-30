using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Swan.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[Serializable]
public enum RepetitionStatus{
    NotStarted,
    Started,
    Ended,
    Timeout,
    Skipped,
}

[Serializable]
public class RunDataLogs
{
    private RepetitionStatus[] statuses;
    private float[] solutionChecks;
    private int[] completedTools;
    private int[] stoppedTools;
    private int[] failedTools;
    private List<string> threadId = new List<string>();

    public RunDataLogs(int repetitionsNumber)
    {
        statuses = new RepetitionStatus[repetitionsNumber];
        statuses = statuses.Select(s => s=RepetitionStatus.NotStarted).ToArray();
        
        solutionChecks = new float[repetitionsNumber];
        
        completedTools = new int[repetitionsNumber];
        stoppedTools = new int[repetitionsNumber];
        failedTools = new int[repetitionsNumber];
    }

    public void SetStatus(int index, RepetitionStatus status)
    {
        Assert.IsNotNull(statuses);
        
        this.statuses[index] = status;
    }

    public void SetCheck(int index, float value)
    {
        Assert.IsNotNull(solutionChecks);
        
        this.solutionChecks[index] = value;
    }

    public void IncrementCompletedToolCount(int index) { this.completedTools[index]++; }
    public void IncrementStoppedToolCount(int index) { this.stoppedTools[index]++; }
    public void IncrementFailedToolCount(int index) { this.failedTools[index]++; }

    public void SetLogThreadId(int index)
    {
        this.threadId.Add(BenchmarkManager.Instance.CurrentRun.ThreadId);
        Assert.IsTrue(index == threadId.IndexOf(BenchmarkManager.Instance.CurrentRun.ThreadId));
    }
}

[Serializable]
public class RunData
{
    public SceneId scene;
    public TaskId taskId;
    public string taskPrompt;
    public SolutionCheckerType solutionChecker;
    public GraphicalResolution graphicalResolution;
    public GraphicalLighting graphicalLighting;
    public Model model;
    public ObjectIdentifier objectIdentifier;
    public CoordinatesType coordinatesType;
    public int repetitions;
    
    public SolutionCheckerBase solutionCheckerBase;
    public RunDataLogs runDataLogs;

    [SerializeField] private System.DateTime startDateTime;
    
    public int repIndex = 0;

    public void SetRepetitionStatus(RepetitionStatus status)
    {
        runDataLogs.SetStatus(repIndex, status);
    }
    
    public void SetSolutionCheck(float solutionCheck)
    {
        runDataLogs.SetCheck(repIndex, solutionCheck);
    }

    public void IncrementCompletedToolCount() { runDataLogs.IncrementCompletedToolCount(repIndex); }
    public void IncrementStoppedToolCount() { runDataLogs.IncrementStoppedToolCount(repIndex); }
    public void IncrementFailedToolCount() { runDataLogs.IncrementFailedToolCount(repIndex); }
    public void SetLogThreadId() { runDataLogs.SetLogThreadId(repIndex); }
    
    public string RunId => $"{scene}-{taskId}-{solutionChecker}-{graphicalLighting}-{graphicalResolution}-{model}-{objectIdentifier}-{coordinatesType}";
    public string StartDateTime => startDateTime.ToString("[MM-dd]-[HH-mm-ss]");
    public string ThreadId => $"{RunId}_{StartDateTime}_rep{repIndex + 1}";

    public void SetStartDateTime()
    {
        startDateTime = System.DateTime.Now;
    }
}

[RequireComponent(typeof(PythonServerWrapper))]
public class BenchmarkManager : MonoBehaviour
{
    [Header("Configs")]
    public bool debugMode = false;
    public bool useCustomRun = false;
    [SerializeField] public bool runPythonServer = true;
    public string csvRelativePath = "BenchmarkData/runs.csv"; // Example path
    public string experimentName = "TO_NAME";
    [SerializeField] public float timeMultiplier = 5f;
    [SerializeField] private float timeoutSeconds = 240f;
    public float TimeoutSeconds => timeoutSeconds;
    
    public static string BenchmarkFolderPath => Path.Combine(Application.dataPath, "BenchmarkData/");
    
    [Header("Runs Data")]
    [SerializeField] private RunData currentRun;
    public RunData CurrentRun => currentRun;

    public string CurrentRunFolderName =>
        Path.Combine(BenchmarkFolderPath, experimentName,  $"{currentRun.RunId}_{currentRun.StartDateTime}");
    
    // List to store all loaded configurations from the CSV
    [SerializeField] private List<RunData> queuedRuns = new List<RunData>();
    [SerializeField] private List<RunData> pastRuns = new List<RunData>();
    
    private PythonServerWrapper m_pythonServerWrapper;
    
    [Header("References")]
    public static BenchmarkManager Instance;
    public AdamAgentRuntime agentRuntime;
    public SolutionCheckerManager checkerManager;

    private int m_sceneBuildIndex;

    [Header("DEBUG")] 
    [SerializeField] private int agentHostPID;
    public int AgentHostPID => agentHostPID;

    public void SetAgentHostPID(int pid)
    {
        agentHostPID = pid;
    }
        
    [SerializeField] private int portOffset = 0;
    public int PortOffset => portOffset;
    
    public int ToolPort => ArgHelper.UnityServerPort + portOffset;
    public int AgentPort => ArgHelper.PythonServerPort + portOffset;

    public void UpdatePortOffset()
    {
        this.portOffset = this.PortOffset + ArgHelper.Parallelism;
    }

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: keep manager across scenes
        }
        else
        {
            Debug.LogError("BenchmarkManager instance already exists! Destroying duplicate.");
            Destroy(gameObject);
        }

        Debug.Log(ArgHelper.LogArgs());
        
        // Get TimeScale from arguments or not (Build / Editor)
        if(ArgHelper.TimeScale != -1)
            Time.timeScale = ArgHelper.TimeScale;
        else
            Time.timeScale = timeMultiplier;
        
        // Get ExperimentName from arguments or not (Build / Editor)
        if (ArgHelper.ExperimentName != null)
            experimentName = ArgHelper.ExperimentName;
        // else
        //     experimentName = [VALUE_AS_SET_IN_INSPECTOR]
    }

    private void LoadRuns()
    {
#if UNITY_EDITOR
        if (!useCustomRun)
        {
            currentRun = null;
            ReadRunsFromCsv(Path.Combine(Application.dataPath, csvRelativePath));
            LoadNextRun();
        }
        else
        {
            currentRun.runDataLogs = new RunDataLogs(currentRun.repetitions);
            Debug.Log("Using custom run configuration set in the Inspector.");
        }
#else
        ReadRunsFromCsv(Path.Combine(Application.dataPath, ArgHelper.CsvPath));
        LoadNextRun();
#endif
    }

    private void SetupPythonServer()
    {
        if (this.runPythonServer == true){
            m_pythonServerWrapper.KillPythonServer();
            //pythonServerWrapper.OnServerLaunched += () => FindObjectsByType<AdamAgentRuntimeUI>(FindObjectsSortMode.None)[0].StartAgent();
            m_pythonServerWrapper.RunPythonServer(currentRun,useCustomRun);
        }
    }

    private void Start()
    {
        m_pythonServerWrapper = GetComponent<PythonServerWrapper>();
        
        LoadRuns();
      
        if (useCustomRun)
        {
            if (currentRun == null)
            {
                Debug.LogError("useCustomRun è TRUE ma CurrentRun non è impostato nell’Inspector.");
                return; // evita di partire senza run
            }
            if (currentRun.runDataLogs == null)
                currentRun.runDataLogs = new RunDataLogs(currentRun.repetitions);
            currentRun.SetStartDateTime();
            currentRun.repIndex = 0; // assicurati che parta da 0
        }

        StartCoroutine(SetupAndStartRunRepetition());
    }
    
    private IEnumerator SetupAndStartRunRepetition()
    {
        switch (BenchmarkManager.Instance.CurrentRun.scene)
        {
            case SceneId.S1:
                m_sceneBuildIndex = 1;
                break;
            case SceneId.S2:
                m_sceneBuildIndex = 2;
                //Debug.LogError("Scene S2 not implemented yet!");
                //throw new Exception("Scene S2 not implemented yet!");
                break;
            default:
                Debug.LogError($"Scene {BenchmarkManager.Instance.currentRun.scene} not implemented yet!");
                throw new Exception($"Scene {BenchmarkManager.Instance.currentRun.scene} not implemented yet!");
                //break;
        }
        
        foreach (SolutionCheckerBase checker in GetComponentsInChildren<SolutionCheckerBase>())
        {
            Destroy(checker);
        }
        
        if (SceneManager.loadedSceneCount == 2)
        {
            AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(m_sceneBuildIndex, UnloadSceneOptions.None);
            asyncUnload.completed += (AsyncOperation asyncOp) =>
            {
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(m_sceneBuildIndex, LoadSceneMode.Additive);
                asyncLoad.completed += LoadSceneCompleteCallback;
            };
        }
        else
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(m_sceneBuildIndex, LoadSceneMode.Additive);
            asyncLoad.completed += LoadSceneCompleteCallback;
        }

        yield return new WaitForEndOfFrame();
    }

    private void LoadSceneCompleteCallback(AsyncOperation asyncOp)
    {
        SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(m_sceneBuildIndex));
        // Load lights correctly
        // LightProbes.Tetrahedralize();
        
        // I get references everytime new RunRepetition scene instance is loaded
        agentRuntime = FindObjectsByType<AdamAgentRuntime>(FindObjectsSortMode.None)[0];
        checkerManager = FindObjectsByType<SolutionCheckerManager>(FindObjectsSortMode.None)[0];
        
        agentRuntime.OnResponseReceived += OnSolutionCheck;

        checkerManager.SetupSolutionChecker();
        
        SetupPythonServer();
        
        currentRun.SetRepetitionStatus(RepetitionStatus.Started);
        StopCoroutine(StartAgent()); //I stop this coroutine if it wass running from a rep before (trying to perform health check)
        StartCoroutine(StartAgent());
    }

    public void ForceRepetitionSkip()
    {
        currentRun.SetRepetitionStatus(RepetitionStatus.Skipped);
        
        Debug.Log($"{RepetitionStatus.Skipped} - Solution completion: N/A");
        currentRun.SetSolutionCheck(0f);
        
        StepToNextRep();
    }

    public void ForceRepetitionTimeout()
    {
        currentRun.SetRepetitionStatus(RepetitionStatus.Timeout);
        
        float completion = checkerManager.SolutionCheker.CheckCompletion();
        Debug.Log($"{RepetitionStatus.Timeout} - Solution completion: {completion}");
        currentRun.SetSolutionCheck(completion);
        
        StepToNextRep();
    }

    private void OnSolutionCheck()
    {
        currentRun.SetRepetitionStatus(RepetitionStatus.Ended);
        
        float completion = checkerManager.SolutionCheker.CheckCompletion();
        Debug.Log($"{RepetitionStatus.Ended} - Solution completion: {completion}");
        currentRun.SetSolutionCheck(completion);
        
        StepToNextRep();
    }

    private void StepToNextRep()
    {
        // Save RunData and ActionsLog to FileSystem
        AdamAgent adamAgent = FindObjectsByType<AdamAgent>(FindObjectsSortMode.None)[0];
        adamAgent.SetCurrentRunDataActions();
        currentRun.SetLogThreadId();
        RunDataSaver.SaveRunData(currentRun, CurrentRunFolderName);
        ActionLogger.LogActionsToFile(adamAgent.PastActions, Path.Combine(CurrentRunFolderName, "ActionLogs/"), $"rep{currentRun.repIndex + 1}.log");

#if UNITY_EDITOR
        if (debugMode)
        {
            string title = $"Repetition Finished";
            string message =
                $"{currentRun.RunId} Rep {currentRun.repIndex + 1}/{currentRun.repetitions} has finished its execution." +
                $"\n\nClick OK to continue.";
            EditorUtility.DisplayDialog(title, message, "OK");
        }
#endif
        
        // Handle next Repetitions/Runs load
        if(currentRun.repIndex < (currentRun.repetitions - 1))
            LoadNextRep();
        else
            LoadNextRun();
        
        // Update ports offset
        UpdatePortOffset();
        // Start new Repetition
        StartCoroutine(SetupAndStartRunRepetition());
    }

    public void LoadNextRep()
    {
        currentRun.repIndex++;
    }
    
    private void LoadNextRun()
    {
        // Retain runs History
        if (currentRun != null)
        {
            pastRuns.Add(currentRun);
        }

        if (queuedRuns.Count == 0)
        {
            currentRun = null;
            Debug.Log("Runs finished executing!");
            
            #if UNITY_EDITOR
                EditorUtility.DisplayDialog("Finished Execution", "All runs finished their execution!\n\nSee results at Assets\\BenchmarkData", "OK");
                EditorApplication.isPaused = true;
            #else
                Application.Quit();
            #endif
            
        }

        // Update current run
        currentRun = queuedRuns[0];
        // Remove run that has just been loaded from queue
        queuedRuns.RemoveAt(0);
        
        if(currentRun.repIndex != 0)
            Debug.LogError("This should not happen : current Run must have RepIndex=0 when it is loaded!");
        
        currentRun.SetStartDateTime();
    }

    IEnumerator StartAgent()
    {
        Debug.Log("[Before StartAgent()] Waiting for python server to start.");

        string endPoint = $"http://localhost:{AgentPort}/health";
        
        UnityWebRequestAsyncOperation req = UnityWebRequest.Get(endPoint).SendWebRequest();
        
        while (true)
        {
            if (req.isDone)
            {
                if(req.webRequest.result == UnityWebRequest.Result.ConnectionError || req.webRequest.result == UnityWebRequest.Result.ProtocolError)
                    Debug.Log($"[Before StartAgent()] Error: {req.webRequest.error}. I will retry to send Get Request...");
                
                if (req.webRequest.responseCode == 200)
                    break;
                else
                    req = UnityWebRequest.Get(endPoint).SendWebRequest();
            }
            else
                yield return new WaitForSecondsRealtime(0.2f);
        }
        Debug.Log("[Before StartAgent()] Python server is running.");
        Debug.Log($"Starting Repetition {currentRun.repIndex + 1} / {currentRun.repetitions}");
        agentRuntime.StartAgent();
    }

    private void ReadRunsFromCsv(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"CSV file not found at path: {path}");
            return;
        }

        try
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length <= 1)
            {
                Debug.LogWarning("CSV file is empty or contains only a header.");
                return;
            }

            // Read header and create a map of column names to index
            var header = lines[0].Split(';').Select(h => h.Trim()).ToList();
            var columnMap = header.ToDictionary(h => h, h => header.IndexOf(h));

            // Process each data row
            for (int i = 1; i < lines.Length; i++)
            {
                
                
                var values = lines[i].Split(';');
                if (values.Length != header.Count)
                {
                    Debug.LogWarning($"Skipping malformed row #{i + 1}: incorrect number of columns.");
                    continue;
                }

                RunData run = new RunData
                {
                    // Use Enum.Parse to convert strings to enum values. True for case-insensitivity.
                    scene = (SceneId)Enum.Parse(typeof(SceneId), values[columnMap["scene"]].Trim(), true),
                    taskId = (TaskId)Enum.Parse(typeof(TaskId), values[columnMap["taskId"]].Trim(), true),
                    taskPrompt = values[columnMap["taskPrompt"]].Trim(),
                    solutionChecker = (SolutionCheckerType)Enum.Parse(typeof(SolutionCheckerType), values[columnMap["solutionChecker"]].Trim(), true),
                    graphicalResolution = (GraphicalResolution)Enum.Parse(typeof(GraphicalResolution), values[columnMap["graphicalResolution"]].Trim(), true),
                    graphicalLighting = (GraphicalLighting)Enum.Parse(typeof(GraphicalLighting), values[columnMap["graphicalLighting"]].Trim(), true),
                    model = (Model)Enum.Parse(typeof(Model), values[columnMap["model"]].Trim(), true),
                    objectIdentifier = (ObjectIdentifier)Enum.Parse(typeof(ObjectIdentifier), values[columnMap["objectIdentifier"]].Trim(), true),
                    coordinatesType = (CoordinatesType)Enum.Parse(typeof(CoordinatesType), values[columnMap["coordinatesType"]].Trim(), true),
                    repetitions = int.Parse(values[columnMap["repetitions"]].Trim()),
                    
                    runDataLogs = new RunDataLogs(int.Parse(values[columnMap["repetitions"]].Trim())),
                };
                
                queuedRuns.Add(run);
            }
            
            Debug.Log($"Successfully loaded {queuedRuns.Count} benchmark runs from {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load or parse CSV file at {path}. Error: {e.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        if (this.runPythonServer==true) m_pythonServerWrapper.KillPythonServer();
    }
}

public static class RunDataSaver
{
    public static void SaveRunData(RunData runData, string folderName)
    {
        string folderPath = Path.Combine(BenchmarkManager.BenchmarkFolderPath, folderName);
        
        Debug.Log($"Saving run data to {folderPath}");
        
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // --- Save runData.csv ---
        string runDataPath = Path.Combine(folderPath, "runData.csv");
        
        string header = "scene;taskId;taskPrompt;solutionChecker;" +
                            "graphicalResolution;graphicalLighting;model;objectIdentifier;" +
                            "coordinatesType;repetitions";

        // Data line
        string runDataLine = string.Join(";",
            runData.scene,
            runData.taskId,
            runData.taskPrompt, //EscapeCsv(runData.taskPrompt),
            runData.solutionChecker,
            runData.graphicalResolution,
            runData.graphicalLighting,
            runData.model,
            runData.objectIdentifier,
            runData.coordinatesType,
            runData.repetitions
        );
        File.WriteAllText(runDataPath, header + "\n" + runDataLine + "\n");

        // --- Save results.csv ---
        string resultsPath = Path.Combine(folderPath, "results.csv");

        // Extract logs
        var statuses = GetRepetitionStatuses(runData.runDataLogs);
        var checks = GetSolutionChecks(runData.runDataLogs);
        var completed = GetCompletedTools(runData.runDataLogs);
        var stopped = GetStoppedTools(runData.runDataLogs);
        var failed = GetFailedTools(runData.runDataLogs);
        var threadId = GetThreadIds(runData.runDataLogs);
        
        string resHeader = "repIndex;status;solutionCheck;completedTools;stoppedTools;failedTools;threadId";
        File.WriteAllText(resultsPath, resHeader + "\n");
        
        // Each repetition = one line
        int completedReps = BenchmarkManager.Instance.CurrentRun.repIndex + 1; // all arrays should have same length
        
        for (int i = 0; i < completedReps; i++)
        {
            string line = string.Join(";",
                i, // repetition index
                statuses[i].ToString(),
                checks[i].ToString("0.000", CultureInfo.InvariantCulture),
                completed[i],
                stopped[i],
                failed[i],
                threadId[i]
            );
            File.AppendAllText(resultsPath, line + "\n");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(";") || value.Contains("\"") || value.Contains("\n"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    private static RepetitionStatus[] GetRepetitionStatuses(RunDataLogs logs)
    {
        var field = typeof(RunDataLogs)
            .GetField("statuses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (RepetitionStatus[])field.GetValue(logs);
    }
    
    private static float[] GetSolutionChecks(RunDataLogs logs)
    {
        var field = typeof(RunDataLogs)
            .GetField("solutionChecks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (float[])field.GetValue(logs);
    }

    private static int[] GetCompletedTools(RunDataLogs logs)
    {
        var field = typeof(RunDataLogs)
            .GetField("completedTools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (int[])field.GetValue(logs);
    }

    private static int[] GetStoppedTools(RunDataLogs logs)
    {
        var field = typeof(RunDataLogs)
            .GetField("stoppedTools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (int[])field.GetValue(logs);
    }

    private static int[] GetFailedTools(RunDataLogs logs)
    {
        var field = typeof(RunDataLogs)
            .GetField("failedTools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (int[])field.GetValue(logs);
    }
    
    private static List<string> GetThreadIds(RunDataLogs logs)
    {
        var field = typeof(RunDataLogs)
            .GetField("threadId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (List<string>)field.GetValue(logs);
    }
}