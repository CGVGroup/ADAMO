using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

public class BenchmarkDebugUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI threadIdUI;
    [SerializeField] private TextMeshProUGUI agentHostPidUI;

    void Awake()
    {
        Assert.IsNotNull(threadIdUI);
        Assert.IsNotNull(agentHostPidUI);
    }
    
    void Update()
    {
        threadIdUI.text = BenchmarkManager.Instance.CurrentRun.ThreadId;
        agentHostPidUI.text = $"AgentHost_PID={BenchmarkManager.Instance.AgentHostPID} | " +
                              $"--agent-port={BenchmarkManager.Instance.AgentPort} " +
                              $"--tool-port={BenchmarkManager.Instance.ToolPort} ";
    }
}
