using UnityEngine;
using System.IO;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System;

[RequireComponent(typeof(MCPNetworkManager))]
public class MCPServerManager : MonoBehaviour
{
    private IHost _mcpHost;

    /// <summary>
    /// Configures and starts the MCP Host using a provided network stream.
    /// This method is called by the NetworkManager once a connection is established.
    /// </summary>
    /// <param name="stream">The network stream to be used for MCP communication.</param>
    public void StartMcpServer(Stream stream)
    {
        if (stream == null)
        {
            UnityEngine.Debug.LogError("Cannot start MCP Server: The provided stream is null.");
            return;
        }

        var builder = Host.CreateApplicationBuilder();
        
        builder.Logging.ClearProviders(); // Optional: remove default loggers
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            // Log everything for debugging purposes.
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Configure the MCP server services, providing the stream transport
        // and discovering tools in the current assembly.
        builder.Services
            .AddMcpServer()
            // A NetworkStream is bidirectional, so we can use it for both stdin and stdout.
            .WithStreamServerTransport(stream, stream) 
            .WithToolsFromAssembly();
        
        _mcpHost = builder.Build();
        
        // Run the MCP server host in the background.
        _mcpHost.RunAsync();
        
        UnityEngine.Debug.Log("MCP Server is running.");
    }

    /// <summary>
    /// Cleans up MCP host resources when the application quits.
    /// </summary>
    private async void OnApplicationQuit()
    {
        // Gracefully stop the MCP Host.
        if (_mcpHost != null)
        {
            UnityEngine.Debug.Log("Stopping MCP Host...");
            await _mcpHost.StopAsync();
            _mcpHost.Dispose();
            UnityEngine.Debug.Log("MCP Host stopped.");
        }
    }
}

/// <summary>
/// Defines a tool type that can be discovered by the MCP server.
/// This can be moved to its own file, but is kept here for simplicity.
/// </summary>
[McpServerToolType]
public static class TestTool
{
    /// <summary>
    /// A sample tool that can be called by an MCP client.
    /// </summary>
    [McpServerTool, Description("Tool for testing, it returns fake current weather")]
    public static string GetWeather(string location) => $"The weather in {location} is AWESOME";
}
