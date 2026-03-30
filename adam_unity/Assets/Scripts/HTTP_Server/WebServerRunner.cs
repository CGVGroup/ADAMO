using UnityEngine;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

public class WebServerRunner : MonoBehaviour
{
    [Tooltip("The path to the Python script, relative to the Unity project's root directory. E.g., 'MyServer/main.py'")]
    [SerializeField] private string serverScriptPath;

    private Process webServerProcess;

    void Awake()
    {
        StartWebServer();
    }

    void OnApplicationQuit()
    {
        StopWebServer();
    }

    private void StartWebServer()
    {
        // 1. Construct the full, absolute path from the relative path.
        // First, combine the project root with the relative path.
        string projectRootPath = Directory.GetParent(Application.dataPath).FullName;
        string combinedPath = Path.Combine(projectRootPath, serverScriptPath);

        // Then, resolve it to a full, clean absolute path to handle ".." characters.
        string absoluteScriptPath = Path.GetFullPath(combinedPath);

        // 2. Validate the final path.
        if (string.IsNullOrEmpty(serverScriptPath) || !File.Exists(absoluteScriptPath))
        {
            Debug.LogError($"Server script path is invalid or file not found! Please check the relative path in the Inspector.\nResolved Path: '{absoluteScriptPath}'");
            return;
        }

        if (webServerProcess != null && !webServerProcess.HasExited)
        {
            Debug.LogWarning("Web server process is already running.");
            return;
        }

        try
        {
            // The working directory is the directory containing the script.
            string workingDirectory = Path.GetDirectoryName(absoluteScriptPath);

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "python",
                // The argument is the full path to the script.
                Arguments = absoluteScriptPath,
                WorkingDirectory = workingDirectory,

                // Your settings to show the console window:
                UseShellExecute = true,
                CreateNoWindow = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            webServerProcess = new Process { StartInfo = startInfo };
            webServerProcess.Start();
            
            Debug.Log($"✅ Started web server '{Path.GetFileName(absoluteScriptPath)}' (PID: {webServerProcess.Id}). A console window should appear.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to start web server process: {e.Message}");
        }
    }

    private void StopWebServer()
    {
        if (webServerProcess == null || webServerProcess.HasExited)
        {
            return;
        }

        try
        {
            Debug.Log($"🛑 Stopping web server process (PID: {webServerProcess.Id})...");
            // Using Kill(true) is slightly more robust as it attempts to kill child processes.
            webServerProcess.Kill(); 
            webServerProcess = null;
        }
        catch (System.Exception e)
        {
            // It's possible the process was already killed, which can throw an error.
            Debug.LogWarning($"Could not stop web server process (it may have already been closed): {e.Message}");
        }
    }
}
