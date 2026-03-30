using System;
using System.Collections;
using UnityEngine;

[Serializable]
public abstract class SolutionCheckerBase : MonoBehaviour
{
    [SerializeField] protected float completion = -1f;
    
    public abstract IEnumerator SetupCheckerData<T>(T checkerParams) where T : SolutionCheckerParams;

    public abstract float CheckCompletion();
}
