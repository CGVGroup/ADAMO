using UnityEngine;
using System;
using System.Net;
using System.Threading;
using System.Text;

/// <summary>
/// A simple HTTP server that runs in a separate thread within Unity.
/// Responds to requests with a basic "Hello from Unity!" message.
/// </summary>
public class HttpServer : MonoBehaviour
{
    [SerializeField] private string serverUrl = "http://localhost:8080/";
    private HttpListener listener;
    private Thread listenerThread;

    /// <summary>
    /// Called when the script instance is being loaded.
    /// </summary>
    void Start()
    {
        // Check if HttpListener is supported on the current platform.
        if (!HttpListener.IsSupported)
        {
            Debug.LogError("HttpListener is not supported on this platform.");
            return;
        }

        // Initialize and start the HTTP listener in a new thread.
        // Running it in a separate thread prevents it from blocking the main Unity game loop.
        listener = new HttpListener();
        listener.Prefixes.Add(serverUrl);
        listenerThread = new Thread(StartListener);
        listenerThread.IsBackground = true; // Allows the thread to be terminated when the application exits.
        listenerThread.Start();
        Debug.Log($"HTTP Server started. Listening on {serverUrl}");
    }

    /// <summary>
    /// The main loop for the listener thread.
    /// Waits for incoming requests and handles them.
    /// </summary>
    private void StartListener()
    {
        try
        {
            listener.Start();
            // The while loop will block on GetContext() until a request is received.
            while (listener.IsListening)
            {
                // Wait for a client to connect
                HttpListenerContext context = listener.GetContext();
                ProcessRequest(context);
            }
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur in the listener thread.
            Debug.LogError($"Listener Thread Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes an individual HTTP request.
    /// </summary>
    /// <param name="context">The context object representing the client request.</param>
    private void ProcessRequest(HttpListenerContext context)
    {
        // Get the request and response objects.
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        // Create a response message.
        string responseString = $"<html><body><h1>Hello from Unity!</h1><p>Current Time: {DateTime.Now}</p><p>Requested URL: {request.Url}</p></body></html>";
        byte[] buffer = Encoding.UTF8.GetBytes(responseString);

        // Set the response headers.
        response.ContentLength64 = buffer.Length;
        response.ContentType = "text/html";

        // Write the response body and close the connection.
        try
        {
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error writing response: {ex.Message}");
        }
        finally
        {
            // Always close the output stream.
            response.OutputStream.Close();
        }
    }

    /// <summary>
    /// Called when the application quits or the editor stops playing.
    /// Cleans up the listener and thread.
    /// </summary>
    void OnDestroy()
    {
        StopServer();
    }

    /// <summary>
    /// Stops the HttpListener and aborts the listener thread.
    /// </summary>
    private void StopServer()
    {
        if (listener != null && listener.IsListening)
        {
            Debug.Log("Stopping HTTP Server...");
            // Stop the listener from accepting new requests.
            listener.Stop();
            listener.Close();
        }

        if (listenerThread != null && listenerThread.IsAlive)
        {
            // Terminate the thread.
            listenerThread.Abort();
        }
        Debug.Log("HTTP Server stopped.");
    }
}
