using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.Identifiers;
using Debug = UnityEngine.Debug;

public class PythonServerWrapper : MonoBehaviour
{
    public string pyExeRelativePath = @".venv\Scripts\python.exe";
    public string scriptRelativePath = @"adam_python\adam_agent_server.py";
    public string args;
    private string argsBase;

    private Process pythonProcess;    // ATT: qui � il processo host (cmd.exe)
    public event Action OnServerLaunched;


    private void Awake()
    {
        argsBase = args;
    }

    public void RunPythonServer(RunData run, bool useCustomeRun = false)
    {
        
#if UNITY_EDITOR //Dati caricati da csv o dalla custom run
        args = argsBase + $"--model-id {run.model} --object-identifier-id {run.objectIdentifier} --coordinates-type-id {run.coordinatesType} ";
#endif
        
#if UNITY_STANDALONE
        args = $"--agent-port {BenchmarkManager.Instance.AgentPort} --tool-port {BenchmarkManager.Instance.ToolPort} --model-id {run.model} --object-identifier-id {run.objectIdentifier} --coordinates-type-id {run.coordinatesType} ";
#endif
        
        Debug.Log($"Starting Python Server\n{args}");
        RunPythonScriptWithArgs(scriptRelativePath, args);
    }

    public void RunPythonScriptWithArgs(string relativeScriptPath, params string[] args)
    {
        string arguments = string.Join(" ", args);

        string pyExePath, scriptPath;
        
#if UNITY_EDITOR
        // Due GetParent: <proj>/Assets -> <proj> -> <adam_project>
        DirectoryInfo mainProjectDir = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName);
        
        pyExePath = Path.Combine(mainProjectDir.FullName, pyExeRelativePath);
        scriptPath = Path.Combine(mainProjectDir.FullName, relativeScriptPath);
#else
        pyExePath = Path.Combine(Application.dataPath, ArgHelper.PythonExePath);
        scriptPath = Path.Combine(Application.dataPath, ArgHelper.PythonServerPath);
#endif

        // Log per debug path
        //Debug.Log($"[Python] mainProjectDir = {mainProjectDir.FullName}");
        //Debug.Log($"[Python] pyExePath      = {pyExePath}  (exists: {File.Exists(pyExePath)})");
        //Debug.Log($"[Python] scriptPath     = {scriptPath} (exists: {File.Exists(scriptPath)})");

        if (!File.Exists(pyExePath))
        {
            Debug.LogError($"python.exe non trovato: {pyExePath}");
            return;
        }
        if (!File.Exists(scriptPath))
        {
            Debug.LogError($"Script Python non trovato: {scriptPath}");
            return;
        }

        string fullCommand = $"\"{pyExePath}\" \"{scriptPath}\" {arguments}";

        var processInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/k \"{fullCommand}\"", // shell visibile finchè sei in Play
            WorkingDirectory = Path.GetDirectoryName(scriptPath),
            UseShellExecute = true               // lasciamo la shell gestita dall'OS (va bene se vuoi la finestra)
        };

        //Debug.Log($"[Python] Running shell command: {processInfo.FileName} {processInfo.Arguments}");
        //Debug.Log($"[Python] WorkingDirectory: {processInfo.WorkingDirectory}");

        int trycount = 0;
        do
        {
            Debug.Log($"Starting Python Server - Try #{trycount++}");

            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpListeners = ipProperties.GetActiveTcpListeners();

            bool portInUse = tcpListeners.Any(ipEndpoint => ipEndpoint.Port == BenchmarkManager.Instance.AgentPort);

            if (portInUse)
            {
                Debug.Log($"Port #{BenchmarkManager.Instance.AgentPort} was already in use. Skipping repetition");
                BenchmarkManager.Instance.ForceRepetitionSkip();
                return;
            }

            try
            {
                pythonProcess = Process.Start(processInfo);
            }
            catch(Exception ex)
            {
                Debug.LogError($"[Python] Failed to start process: {ex.Message}");
            }
        } while (pythonProcess == null || pythonProcess.HasExited);
        
        BenchmarkManager.Instance.SetAgentHostPID(pythonProcess.Id);
        Debug.Log($"[Python] Successfully launched. Host PID (cmd): {pythonProcess.Id}");

        OnServerLaunched?.Invoke(); //Actually nver used
    }

    public void KillPythonServer()
    {
        //Debug.Log("[Python] Attempting to shut down�");

        if (pythonProcess == null)
        {
            //Debug.Log("[Python] (No host process to kill)");
            return;
        }

        try
        {
            if (!pythonProcess.HasExited)
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                // Chiudi l'intero albero (cmd + python): niente Kill(bool), niente problemi di supporto
                var tk = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {pythonProcess.Id} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(tk))
                {
                    p?.WaitForExit(3000);
                }
#else
                // Su altri OS, prova la kill diretta del processo host
                pythonProcess.Kill();
                pythonProcess.WaitForExit(3000);
#endif
            }
            //Debug.Log("[Python] Host process terminated.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Python] Error during shutdown: {e.Message}");
        }
        finally
        {
            try { pythonProcess?.Dispose(); } catch { /* ignore */ }
            pythonProcess = null;
        }
    }

    private void OnDestroy() => KillPythonServer();
    private void OnApplicationQuit() => KillPythonServer();
}
