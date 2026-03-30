using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;

public class MCPNetworkManager : MonoBehaviour
{
    [Header("Network Bridge Settings")]
    [Tooltip("The path to the compiled console application executable.")]
    public string bridgeExecutablePath = "C:/Path/To/Your/UnityNetworkBridge.exe";

    private readonly string m_unityHost = "127.0.0.1";
    
    [Tooltip("The port the bridge will listen on for Unity's connection.")]
    public int unityPort = 8080;
    
    [Tooltip("The port the bridge will listen on for the remote client's connection.")]
    public int remotePort = 8081;


    private MCPServerManager _mcpServerManager;

    private Process _networkBridgeProcess;
    private TcpClient _unityClient;
    //private bool _isConnected = false;

    void Awake()
    {
        // Automatically find the McpServerManager if it's not assigned in the inspector.
        if (_mcpServerManager == null)
        {
            _mcpServerManager = GetComponent<MCPServerManager>();
        }
    }

    /// <summary>
    /// Starts the network bridge process and connects to it.
    /// </summary>
    void Start()
    {
        LaunchNetworkBridge();
        ConnectToBridge();
    }

    /// <summary>
    /// Launches the external console application.
    /// </summary>
    private void LaunchNetworkBridge()
    {
        if (!File.Exists(bridgeExecutablePath))
        {
            UnityEngine.Debug.LogError($"Network Bridge executable not found at: {bridgeExecutablePath}");
            return;
        }

        try
        {
            _networkBridgeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = bridgeExecutablePath,
                    // Pass the inspector-configured ports as command-line arguments to the bridge.
                    Arguments = $"{unityPort} {remotePort}",
                    CreateNoWindow = false, 
                    UseShellExecute = true
                }
            };
            _networkBridgeProcess.Start();
            UnityEngine.Debug.Log($"Network Bridge process started with arguments: {unityPort} {remotePort}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to start Network Bridge process: {e.Message}");
        }
    }

    /// <summary>
    /// Connects to the running network bridge and hands the connection to the MCP Server Manager.
    /// </summary>
    private async void ConnectToBridge()
    {
        try
        {
            _unityClient = new TcpClient();
            // Give the bridge a moment to start up before connecting.
            await Task.Delay(1000); 

            // Use the inspector-configured host and port to connect to the bridge.
            await _unityClient.ConnectAsync(m_unityHost, unityPort);
            
            if(_unityClient.Connected)
            {
                NetworkStream networkStream = _unityClient.GetStream();
                //_isConnected = true;
                UnityEngine.Debug.Log("Successfully connected to the Network Bridge. Handing stream to MCP Server Manager.");

                // Hand off the established stream to the McpServerManager to start the server.
                _mcpServerManager.StartMcpServer(networkStream);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to connect to Network Bridge on {m_unityHost}:{unityPort}: {e.Message}");
        }
    }

    /// <summary>
    /// Cleans up network resources when the application quits.
    /// </summary>
    private void OnApplicationQuit()
    {
        // Close the TCP connection.
        if (_unityClient != null)
        {
             _unityClient.Close();
             UnityEngine.Debug.Log("TCP client closed.");
        }

        // Terminate the external console process.
        if (_networkBridgeProcess != null && !_networkBridgeProcess.HasExited)
        {
            _networkBridgeProcess.Kill();
            UnityEngine.Debug.Log("Network Bridge process terminated.");
        }
    }
}

