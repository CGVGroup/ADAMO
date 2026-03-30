using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.Assertions;

public class TransformParentContraint : MonoBehaviour
{
    [SerializeField] Transform targetParent;
    [SerializeField] Vector3 positionOffset;
    
    [SerializeField] LookAtIK lookAtIK;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Assert.IsNotNull(targetParent);
        
        positionOffset = targetParent.InverseTransformPoint(this.transform.position);

        lookAtIK.solver.OnPostUpdate += OnPostUpdate;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // DebugPosition();
        //
        // this.transform.position = targetParent.position + positionOffset;
        // this.transform.rotation = targetParent.rotation;
    }

    private void OnPostUpdate()
    {
        this.transform.position = targetParent.position + positionOffset;
        this.transform.rotation = targetParent.rotation;
        // Debug.Log(this.transform.position);
    }

    private void OnDestroy()
    {
        lookAtIK.solver.OnPostUpdate -= OnPostUpdate;
    }
}
