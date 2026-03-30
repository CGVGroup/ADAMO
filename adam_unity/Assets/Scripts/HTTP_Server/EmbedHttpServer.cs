using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// EmbedIO
using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing; // [Route]

// For interaction effectors
using HumanoidInteraction;
// To check ActionState
using ActionSystem;

[RequireComponent(typeof(MainThreadDispatcher))]
public class EmbedHttpServer : MonoBehaviour
{
    [SerializeField] int port = 60000; // Default port for UnityServer
    [SerializeField] string bindAddress = "0.0.0.0"; // 127.0.0.1 = solo locale

    [SerializeField] private AdamAgent adamAgent;
    [SerializeField] private Transform walkLocation;
    [SerializeField] private Transform dropTarget;
    
    private IWebServer m_server;
    private CancellationTokenSource m_cts;

    void Awake()
    {
        port = BenchmarkManager.Instance.ToolPort;
        
        var url = $"http://{bindAddress}:{port}/";

        m_server = new WebServer(opt => 
                opt.WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO)) // usa listener interno (portabile)
                    .WithCors("*", "*", "*") // CORS permissivo (sviluppo)
                    .WithWebApi("/api", m => m.WithController(() => new AdamAgentController(adamAgent, dropTarget)));
        
        m_cts = new CancellationTokenSource();

        m_server.OnUnhandledException = (context, exception) =>
        {
            Debug.LogError(exception.Message + "\n" + exception.StackTrace);
            context.Response.StatusCode = 500; // Internal Server Error
            return context.SendDataAsync(exception.Message + "\n" + exception.StackTrace);
        };
        m_server.OnHttpException = (context, exception) =>
        {
            Debug.LogError(exception.Message + "\n" + exception.StackTrace);
            context.Response.StatusCode = 500;
            return context.SendDataAsync(exception.Message + "\n" + exception.StackTrace);
        };
        
        // Avvia il server in background
        _ = m_server.RunAsync(m_cts.Token);

        //Debug.Log($"[EmbedIO] listening on {url}");
    }

    void Start()
    {
        StartCoroutine(TimeoutTimer());
    }
    
    IEnumerator TimeoutTimer()
    {
        int subdivisions = 10;
        float subSecondsToWait = BenchmarkManager.Instance.TimeoutSeconds / subdivisions;
        
        for (int i = 0; i < subdivisions; i++){
            yield return new WaitForSecondsRealtime(subSecondsToWait);
            Debug.Log($"TimeoutTimer: {subSecondsToWait * (subdivisions - (i+1))}s remaining.");
        }
        
        // This code will execute after the timer has finished
        Debug.Log($"TimeoutTimer finished! Shutting down current repetition: {BenchmarkManager.Instance.CurrentRun.repIndex}");
        BenchmarkManager.Instance.ForceRepetitionTimeout();
    }

    void OnDestroy()
    {
        StopCoroutine(TimeoutTimer());
        try { m_cts?.Cancel(); } catch { }
        try { (m_server as IDisposable)?.Dispose(); } catch { }
        m_cts = null;
        m_server = null;
    }
}


public sealed class AdamAgentController : WebApiController
{
    private AdamAgent m_agent;
    private Transform m_dropTarget;

    public AdamAgentController(AdamAgent agent, Transform dropTarget)
    {
        m_agent = agent;
        m_dropTarget = dropTarget;
    }
    
    // LOOK action
    [Route(HttpVerbs.Post, "/look")]
    public async Task<object> LookAtAndCapture()
    {
        var payload = await HttpContext.GetRequestDataAsync<Dictionary<string, float>>();
        string debugString = "";
        foreach (var kvp in payload)
        {
            debugString += $"{kvp.Key}: {kvp.Value}";
        }
        // Debug.Log($"[AdamAgent] {debugString}");

        LookAndCaptureCompositeAction compositeAction = await MainThreadDispatcher.Enqueue<LookAndCaptureCompositeAction>(() =>
        {
            Vector3 lookPointLocal =
                new Vector3(
                    payload["x"],
                    payload["y"],
                    payload["z"]);
            
            Vector3 lookToPoint;

            if (BenchmarkManager.Instance.CurrentRun.coordinatesType == CoordinatesType.REL)
                lookToPoint = CameraManager.Instance.GetLastCaptureGlobalPosition(lookPointLocal);
            else
                lookToPoint = lookPointLocal;

            LookAndCaptureCompositeAction compositeAction = new LookAndCaptureCompositeAction(m_agent, lookToPoint);
            m_agent.AddAction(compositeAction);

            return compositeAction;
        });
        
        await WaitForAction(compositeAction);

        Dictionary<string,object> response = new Dictionary<string, object>();
       
        switch (compositeAction.State)
        {
            case ActionState.Completed:

                var goList = new List<object>();
                foreach (var capturedObj in compositeAction.captureCameraAction.VisibleObjsData)
                    goList.Add(ParseIntoAnonymousObject(capturedObj));
                
                var pointList = new List<object>();
                foreach (var p in compositeAction.captureCameraAction.SpatialPointsData)
                    pointList.Add(ParseIntoAnonymousObject(p));
                
                response = new Dictionary<string, object>()
                {
                    {"action_state", compositeAction.captureCameraAction.State.ToString()},
                    {"action_log", compositeAction.captureCameraAction.Log},
                
                    {"image_base64", compositeAction.captureCameraAction.TexBase64},
                    {"game_objects", goList.ToArray()},
                    {"spatial_points", pointList.ToArray()},
                };
                break;
            
            case ActionState.Failed:
                Debug.LogError("Action failed, but it shouldn't happen!");
                break;
            
            default:
                throw new Exception("Unhandled action state: " + compositeAction.State);
        }
        
        return response;
    }
    
    
    // WALK action
    [Route(HttpVerbs.Post, "/walk")]
    public async Task<object> Walk()
    {
        var payload = await HttpContext.GetRequestDataAsync<Dictionary<string, float>>();

        WalkAction walkAction = await MainThreadDispatcher.Enqueue<WalkAction>(() =>
        {
            // This code will be executed safely on the main thread in the Update loop
            Vector3 reqPoint =
                new Vector3(
                    payload["x"], 
                    payload["y"], 
                    payload["z"]);

            Vector3 reqDestination;

            if (BenchmarkManager.Instance.CurrentRun.coordinatesType == CoordinatesType.REL)
                reqDestination = CameraManager.Instance.GetLastCaptureGlobalPosition(reqPoint);
            else
                reqDestination = reqPoint;

            WalkAction moveTurnAction = new WalkAction(m_agent, reqDestination);
            m_agent.AddAction(moveTurnAction);

            return moveTurnAction;
        });
        
        await WaitForAction(walkAction);

        Dictionary<string,object> response = new Dictionary<string, object>();
       
        switch (walkAction.State)
        {
            case ActionState.Completed:
                response["action_state"] = walkAction.State.ToString();
                response["completion_code"] = walkAction.CompletionCode?.ToString();
                response["action_log"] = walkAction.Log;
                break;
            case ActionState.Failed:
                response["action_state"] = walkAction.State.ToString();
                response["fail_code"] = walkAction.FailCode?.ToString();
                response["action_log"] = walkAction.Log;
                break;
            default:
                throw new Exception("Unhandled action state: " + walkAction.State);
        }

        response = await AddAgentState(response);
        
        return response;
    }
    
    // PICK Action
    [Route(HttpVerbs.Post, "/pick")]
    public async Task<object> Pick()
    {
        var payload = await HttpContext.GetRequestDataAsync<Dictionary<string, string>>();

        ReachPickCompositeAction compAction = await MainThreadDispatcher.Enqueue<ReachPickCompositeAction>(() =>
        {
            // I try to get the requested object from stringId
            
            ObjectTag objTag = ObjectTag.GetObjectTagFromStringID(payload["stringId"]);
            
            Pickable pickable;
            if (objTag == null)
            {
                Debug.LogWarning($"No ObjectTag found for StringId={payload["stringId"]}");
                pickable = null; // I put pickable=null to still create a PickAction an process its failure inside the PickAction itself
            }
            else
                pickable = objTag.GetComponent<Pickable>();
            
            ReachPickCompositeAction pickAction = (ReachPickCompositeAction) m_agent.Pick(pickable, EffectorType.RightHand);

            return pickAction;
        });
        
        await WaitForAction(compAction);
        
        Vector3 effectorStoppedPosition;
        Vector3 objectPosition;
        //I need to get global position using mainThread (has to call UnityEngine internal functions for transformation)
        if (BenchmarkManager.Instance.CurrentRun.coordinatesType == CoordinatesType.REL) {
            effectorStoppedPosition = await MainThreadDispatcher.Enqueue<Vector3>(() =>
                CameraManager.Instance.GetLastCaptureRelativePosition(compAction.pickAction.StoppedPosition));
            objectPosition = await MainThreadDispatcher.Enqueue<Vector3>(() =>
            {
                if (compAction.pickAction.ObjectTransform == null) return Vector3.zero;
                return CameraManager.Instance.GetLastCaptureRelativePosition(compAction.pickAction.ObjectTransform.position);
            });
        }
        else
        {
            effectorStoppedPosition = await MainThreadDispatcher.Enqueue<Vector3>(() => compAction.pickAction.StoppedPosition);
            objectPosition = await MainThreadDispatcher.Enqueue<Vector3>(() =>
            {
                if (compAction.pickAction.ObjectTransform == null) return Vector3.zero;
                return compAction.pickAction.ObjectTransform.position;
            });
        }
        
        Dictionary<string,object> response = new Dictionary<string, object>(){
            {"action_state", compAction.State.ToString()},
            {"fail_code", compAction.FailCode?.ToString()},
            {"completion_code", compAction.CompletionCode?.ToString()},
            {"action_log", compAction.Log},
            {"obj_id", payload["stringId"]},
            {"obj_pos", objectPosition},
            {"extension_limit_point", effectorStoppedPosition},
        };

        response = await AddAgentState(response);
        
        return response;
    }
    
    // DROP Action
    [Route(HttpVerbs.Post, "/drop")]
    public async Task<object> Drop()
    {
        var payload = await HttpContext.GetRequestDataAsync<Dictionary<string, object>>();

        ReachDropCompositeAction compAction = await MainThreadDispatcher.Enqueue<ReachDropCompositeAction>(() =>
        {
            Vector3 dropPosition = new Vector3(
                Convert.ToSingle(payload["x"]),
                Convert.ToSingle(payload["y"]),
                Convert.ToSingle(payload["z"])
                );
            
            if(BenchmarkManager.Instance.CurrentRun.coordinatesType == CoordinatesType.REL)
                //Modifico il riferimento alla camera del VH
                m_dropTarget.transform.position = CameraManager.Instance.GetLastCaptureRelativePosition(dropPosition);
            else
                m_dropTarget.transform.position = dropPosition;

            // I try to get the requested object from stringId
            Pickable pickable;
            
            ObjectTag objectTag = ObjectTag.GetObjectTagFromStringID((string) payload["stringId"]);
            if (objectTag == null)
            {
                Debug.LogWarning($"No ObjectTag found for StringId={payload["stringId"]}");
                pickable = null;
            }
            else
                pickable = objectTag.GetComponent<Pickable>();
            
            ReachDropCompositeAction dropAction = (ReachDropCompositeAction) m_agent.Drop(pickable,m_dropTarget,EffectorType.RightHand);
            return dropAction;
        });
        
        await WaitForAction(compAction);

        Vector3 effectorStoppedPosition;
        Vector3 dropPosition;
        Vector3 currentLocation;
        //I need to get global position using mainThread (has to call UnityEngine internal functions for transformation)
        if (BenchmarkManager.Instance.CurrentRun.coordinatesType == CoordinatesType.REL)
        {
            effectorStoppedPosition = await MainThreadDispatcher.Enqueue<Vector3>(() =>
                CameraManager.Instance.GetLastCaptureRelativePosition(compAction.dropAction.StoppedPosition));
            dropPosition = await MainThreadDispatcher.Enqueue<Vector3>(() => 
                CameraManager.Instance.GetLastCaptureRelativePosition(compAction.dropAction.DropTransform.position));
        }
        else
        {
            effectorStoppedPosition = await MainThreadDispatcher.Enqueue<Vector3>(() => compAction.dropAction.StoppedPosition);
            dropPosition = await MainThreadDispatcher.Enqueue<Vector3>(() => compAction.dropAction.DropTransform.position);
        }

        Dictionary<string, object> response = new Dictionary<string, object>()
        {
            {"action_state", compAction.State.ToString()},
            {"fail_code", compAction.FailCode?.ToString()},
            {"completion_code", compAction.CompletionCode?.ToString()},
            {"action_log", compAction.Log},
            {"obj_id", payload["stringId"]},
            {"drop_pos", dropPosition},
            {"extension_limit_point", effectorStoppedPosition},
        };
        
        response = await AddAgentState(response);
        
        return response;
    }

    private async Task WaitForAction(AgentAction action)
    {
        while (true) //TODO: Aggiungere timeout!!!
        {
            ActionState state = action.State;
        
            if (state == ActionState.Completed || state == ActionState.Failed || state == ActionState.Stopped)
                break;
            
            await Task.Yield();
        }
        await Task.Yield();
    }
    
    // private async Task WaitForActions(List<AgentAction> actionList)
    // {
    //     bool finished = false;
    //     
    //     while (! finished) //TODO: Aggiungere timeout!!!
    //     {
    //         foreach (AgentAction action in actionList)
    //         {
    //             finished = true;
    //             ActionState state = action.State;
    //
    //             if (! (state == ActionState.Completed ||
    //                 state == ActionState.Failed ||
    //                 state == ActionState.Stopped))
    //             {
    //                 finished = false;
    //             }
    //         }
    //
    //         await Task.Delay(10); //TODO: Tempo totalmente arbitrario!!!
    //     }
    // }

    private object ParseIntoAnonymousObject(UnityGameObjectData goData)
    {
        return new
        {
            id = goData.id.ToString(),
            type = goData.type.ToString(),
            position = new
            {
                x = goData.position.x,
                y = goData.position.y,
                z = goData.position.z,
            },
            volume = goData.volume,
        };
    }

    private async Task<Dictionary<string,object>> AddAgentState(Dictionary<string,object> dict)
    {
        Vector3 currentLocation;
        if (BenchmarkManager.Instance.CurrentRun.coordinatesType == CoordinatesType.REL)
        { 
            currentLocation = await MainThreadDispatcher.Enqueue<Vector3>(() =>
                CameraManager.Instance.GetLastCaptureRelativePosition(m_agent.LocomotionSystem.transform.position));
        }
        else
        {
            currentLocation = await MainThreadDispatcher.Enqueue<Vector3>(()=> m_agent.LocomotionSystem.transform.position);
        }
        
        dict["agent_location"] = currentLocation;
        
        return dict;
    }
}