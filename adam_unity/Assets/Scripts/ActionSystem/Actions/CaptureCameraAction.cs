using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

namespace ActionSystem
{
    [Serializable]
    public class CaptureCameraAction : AgentAction
    {
        [SerializeField] private CameraManager cameraManager;
        
        [SerializeField] private string texBase64;
        public string TexBase64 => texBase64;

        [SerializeField] private List<UnityGameObjectData> visibleObjsData;
        public List<UnityGameObjectData> VisibleObjsData => visibleObjsData;
        
        [SerializeField] private List<UnityGameObjectData> spatialPointsData;
        public List<UnityGameObjectData> SpatialPointsData => spatialPointsData;
    
        public CaptureCameraAction(CameraManager cameraManager)
        {
            Assert.IsNotNull(cameraManager);

            this.cameraManager = cameraManager;
        }

        protected internal override void Setup()
        {
            SetState(ActionState.Updating);
        }

        protected internal override void OnStart()
        {
            cameraManager.ClearScreenGrid();
            List<SpatialPointTag> spatialPoints;
            cameraManager.ProjectScreenGrid(out spatialPoints);

            spatialPointsData = spatialPoints
                .Select(p => new UnityGameObjectData(p, BenchmarkManager.Instance.CurrentRun)).ToList();
            
            cameraManager.CaptureBase64(out texBase64, out visibleObjsData);
            SetState(ActionState.Completed);
        }

        protected internal override void OnUpdate()
        {
        }

        protected internal override void OnComplete()
        {
        }

        protected internal override void OnStop()
        {
        }

        protected internal override void OnFail()
        {
            throw new NotImplementedException();
        }
    } 
}
