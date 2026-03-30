using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations.Rigging;
using MxM;
using UnityEngine.Animations;

[RequireComponent(typeof(MxMAnimator))]
[RequireComponent(typeof(RigBuilder))]
public class MxMRigGraphPostIK : MonoBehaviour
{
    private MxMAnimator mxmAnimator;
    private RigBuilder rigBuilder;
    private PlayableGraph graph;
    private Playable rigPlayable;

    private void Awake()
    {
        mxmAnimator = GetComponent<MxMAnimator>();
        rigBuilder = GetComponent<RigBuilder>();
        mxmAnimator.OnIdleEnd.AddListener(OnMxMIdleEnd);
    }

    private void OnDestroy()
    {
        mxmAnimator.OnIdleEnd.RemoveListener(OnMxMIdleEnd);
        if (rigPlayable.IsValid())
            rigPlayable.Destroy();
    }

    private void OnMxMIdleEnd()
    {
        StartCoroutine(HookRigPlayableNextFrame());
    }

    private System.Collections.IEnumerator HookRigPlayableNextFrame()
    {
        yield return null; // wait one frame for MxM to build graph

        graph = mxmAnimator.MxMPlayableGraph;
        if (!graph.IsValid())
            yield break;

        // Create the custom playable
        var playableBehaviour = new RigPlayableBehaviour(); // Use default constructor
        playableBehaviour.rigBuilder = rigBuilder;          // Assign your RigBuilder

        rigPlayable = ScriptPlayable<RigPlayableBehaviour>.Create(graph, playableBehaviour);

        // Connect it to the Animator output
        var output = AnimationPlayableOutput.Create(graph, "RigPostIK", mxmAnimator.UnityAnimator);
        output.SetSourcePlayable(rigPlayable);

        graph.Play();
    }
}

public class RigPlayableBehaviour : IPlayableBehaviour
{
    public RigBuilder rigBuilder;

    // Parameterless constructor required by ScriptPlayable
    public RigPlayableBehaviour() { }

    public void PrepareFrame(Playable playable, FrameData info)
    {
        //throw new System.NotImplementedException();
    }

    public void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (rigBuilder != null)
        {
            // Evaluate rig layers after MxM updates animation
            rigBuilder.Build();
        }
    }

    public void OnGraphStart(Playable playable) { }
    public void OnGraphStop(Playable playable) { }
    public void OnPlayableCreate(Playable playable) { }
    public void OnPlayableDestroy(Playable playable) { }
    public void OnBehaviourPlay(Playable playable, FrameData info) { }
    public void OnBehaviourPause(Playable playable, FrameData info) { }
}