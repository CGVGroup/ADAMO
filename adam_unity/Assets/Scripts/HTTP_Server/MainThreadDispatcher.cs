using System;
using System.Collections.Concurrent; // Needed for the thread-safe queue
using System.Threading.Tasks; 
using ActionSystem; 
using UnityEngine;

/// <summary>
/// A thread-safe dispatcher that allows you to execute code on the main Unity thread
/// from any other thread. It uses a singleton pattern for easy access.
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    // The singleton instance.
    private static MainThreadDispatcher _instance;

    // The thread-safe queue of actions to be executed on the main thread.
    // ConcurrentQueue is designed for one thread to add items while another removes them.
    private readonly static ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

    // A lock for the singleton instance creation to make it thread-safe.
    private static readonly object _lock = new object();

    /// <summary>
    /// Gets the singleton instance of the dispatcher.
    /// It will be created if it doesn't exist in the scene.
    /// </summary>
    public static MainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    // Check again inside the lock to be sure
                    if (_instance == null)
                    {
                        // Try to find it in the scene
                        _instance = FindObjectsByType<MainThreadDispatcher>(FindObjectsSortMode.None)[0];
                        if (_instance == null)
                        {
                            // If not found, create a new GameObject and add the component
                            var singletonObject = new GameObject(nameof(MainThreadDispatcher));
                            _instance = singletonObject.AddComponent<MainThreadDispatcher>();
                        }
                    }
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        // Enforce the singleton pattern. If an instance already exists and it's not this one, destroy this one.
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            //Don't destroy this object when loading a new scene
            //DontDestroyOnLoad(this.gameObject); 
        }
    }

    private void Update()
    {
        // Process all actions in the queue on the main thread.
        // We use TryDequeue to safely get an action from the queue.
        
        while (_actions.TryDequeue(out var action))
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw e;
            }
        }
    }

    /// <summary>
    /// Enqueues an action to be executed on the main thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public static void Enqueue(Action action)
    {
        // Add the action to our thread-safe queue.
        _actions.Enqueue(action);
    }
    
    /// <summary>
    /// Enqueues a function to be executed on the main thread and returns a Task
    /// that will be completed with the function's result.
    /// </summary>
    /// <param name="function">The function to execute.</param>
    /// <returns>A Task that represents the asynchronous operation and contains the result.</returns>
    public static Task<T> Enqueue<T>(Func<T> function)
    {
        // TaskCompletionSource is the "controller" for a Task.
        // It's how we manually complete a Task when our work is done.
        var tcs = new TaskCompletionSource<T>();

        // We enqueue a simple action.
        // The job of this action is to run the user's function and set the result on our Task.
        Enqueue(() =>
        {
            try
            {
                // Execute the function and set the result.
                T result = function();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                // If the function throws an exception, pass it to the Task.
                tcs.SetException(ex);
                Debug.LogError(ex);
            }
        });

        // Return the Task immediately. The background thread will await this.
        return tcs.Task;
    }
}