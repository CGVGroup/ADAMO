using System;
using System.Collections.Generic;
using UnityEngine;

// [Serializable]
// public class TextInferencePayload
// {
//     public string thread_id;
//     public string message;
// }
//
// [Serializable]
// public class VisionInferencePayload
// {
//     public string thread_id;
//     public string message;
//     public string image_base64;
// }

[Serializable]
public class InferenceResponse
{
    public string thread_id;
    public string response;
}

[Serializable]
public class ThreadsResponse
{
    public string[] threads;
}

[Serializable]
public class BaseResponse
{
    public int code;
    public string message;
}

[Serializable]
public class Vector3Serializable
{
    public float x;
    public float y;
    public float z;

    public Vector3Serializable(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }
}

[Serializable]
public class UnityGameObjectData
{
    public int id; // instanceID
    public string type;
    public Vector3Serializable position;
    public float volume;

    public UnityGameObjectData(ObjectTag objTag, RunData run)
    {
        this.id = objTag.PersistentId;
        this.type = objTag.type.ToString();
        this.volume = objTag.Volume;
        
        if (run.coordinatesType == CoordinatesType.ABS)
            this.position = new Vector3Serializable(objTag.CenterPosition);
        else
            this.position =
                new Vector3Serializable(CameraManager.Instance.GetLastCaptureRelativePosition(objTag.CenterPosition));
    }

    public UnityGameObjectData(SpatialPointTag pointTag, RunData run)
    {
        this.id = pointTag.Id;
        this.type = "P";
        
        if (run.coordinatesType == CoordinatesType.ABS)
            this.position = new Vector3Serializable(pointTag.transform.position);
        else
            this.position =
                new Vector3Serializable(CameraManager.Instance.GetLastCaptureRelativePosition(pointTag.transform.position));
    }
}

[Serializable]
public class UnityAgentData
{
    public Vector3Serializable position;

    public UnityAgentData(Vector3 position)
    {
        this.position = new Vector3Serializable(position);
    }
}

[Serializable]
public class AgentInferencePayload
{
    public string thread_id;
    public string message;
    public string image_base64;
    public Vector3Serializable agent_location;
    public UnityGameObjectData[] game_objects;
    public UnityGameObjectData[] spatial_points;
}