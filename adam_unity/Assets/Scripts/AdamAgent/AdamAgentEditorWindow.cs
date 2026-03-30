using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

#if UNITY_EDITOR
public class AdamAgentEditorWindow : EditorWindow
{
    private string serverHost = "http://localhost:9999";
    private string threadId = "0";
    private string inputText = "Ciao Adam!";
    private Texture2D inputImage;
    private string responseText = "";
    private string lastCallDuration = "";

    private AdamAgentClient client;

    // Sezioni GUI
    private bool showTextInference = true;
    private bool showVisionInference = true;
    private bool showThreads = false;
    private bool showLogout = false;

    private Camera selectedCamera;
    private RenderTexture captureRT;

    [MenuItem("Adam Agent/Client Interface")]
    public static void ShowWindow()
    {
        GetWindow<AdamAgentEditorWindow>("Adam Agent Client");
    }

    private void OnGUI()
    {
        GUILayout.Label("🔧 Server Settings", EditorStyles.boldLabel);
        serverHost = EditorGUILayout.TextField("Host", serverHost);
        if (client == null || clientHostChanged)
            client = new AdamAgentClient(serverHost);

        threadId = EditorGUILayout.TextField("Thread ID", threadId);
        GUILayout.Space(10);

        // showTextInference = EditorGUILayout.Foldout(showTextInference, "💬 Text Inference");
        // if (showTextInference)
        // {
        //     inputText = EditorGUILayout.TextField("Message", inputText);
        //     if (GUILayout.Button("Send Text Inference"))
        //         SendTextInference();
        // }

        // GUILayout.Space(10);
        // showVisionInference = EditorGUILayout.Foldout(showVisionInference, "🖼️ Vision Inference");
        // if (showVisionInference)
        // {
        //     inputText = EditorGUILayout.TextField("Message", inputText);
        //
        //     GUILayout.Label("📁 Image from File", EditorStyles.boldLabel);
        //     inputImage = (Texture2D)EditorGUILayout.ObjectField("Image", inputImage, typeof(Texture2D), false);
        //
        //     GUILayout.Label("📷 Capture from Camera", EditorStyles.boldLabel);
        //     selectedCamera = (Camera)EditorGUILayout.ObjectField("Camera", selectedCamera, typeof(Camera), true);
        //     if (GUILayout.Button("Capture From Camera"))
        //         inputImage = CaptureFromCamera();
        //
        //     if (inputImage != null && GUILayout.Button("Send Vision Inference"))
        //         SendVisionInference();
        // }

        GUILayout.Space(10);
        showThreads = EditorGUILayout.Foldout(showThreads, "📂 Threads");
        if (showThreads)
        {
            if (GUILayout.Button("Get Threads"))
                GetThreads();
            if (GUILayout.Button("Delete Current Thread"))
                DeleteThread();
        }

        GUILayout.Space(10);
        showLogout = EditorGUILayout.Foldout(showLogout, "🚪 Logout");
        if (showLogout && GUILayout.Button("Logout"))
            Logout();

        GUILayout.Space(10);
        GUILayout.Label("📨 Response:", EditorStyles.boldLabel);

        GUIStyle style = EditorStyles.textArea;
        float height = style.CalcHeight(new GUIContent(responseText), position.width - 20);
        EditorGUILayout.TextArea(responseText, style, GUILayout.Height(height));

        GUILayout.Label(lastCallDuration, EditorStyles.miniLabel);
    }

    private bool clientHostChanged => client != null && serverHost != clientHostField;
    private string clientHostField => client?.ToString();

    // private async void SendTextInference()
    // {
    //     responseText = "Sending...";
    //     Repaint();
    //
    //     Stopwatch sw = Stopwatch.StartNew();
    //     responseText = await client.TextInference(threadId, inputText);
    //     sw.Stop();
    //
    //     lastCallDuration = $"⏱ Duration: {sw.ElapsedMilliseconds} ms";
    //     Repaint();
    // }
    //
    // private async void SendVisionInference()
    // {
    //     responseText = "Sending...";
    //     Repaint();
    //
    //     Stopwatch sw = Stopwatch.StartNew();
    //     responseText = await client.VisionInference(threadId, inputText, inputImage);
    //     sw.Stop();
    //
    //     lastCallDuration = $"⏱ Duration: {sw.ElapsedMilliseconds} ms";
    //     Repaint();
    // }

    private async void GetThreads()
    {
        responseText = "Loading threads...";
        Repaint();

        Stopwatch sw = Stopwatch.StartNew();
        var threads = await client.GetThreads();
        sw.Stop();

        responseText = "Threads:\n" + string.Join("\n", threads);
        lastCallDuration = $"⏱ Duration: {sw.ElapsedMilliseconds} ms";
        Repaint();
    }

    private async void DeleteThread()
    {
        responseText = "Deleting...";
        Repaint();

        Stopwatch sw = Stopwatch.StartNew();
        responseText = await client.DeleteThread(threadId);
        sw.Stop();

        lastCallDuration = $"⏱ Duration: {sw.ElapsedMilliseconds} ms";
        Repaint();
    }

    private async void Logout()
    {
        responseText = "Logging out...";
        Repaint();

        Stopwatch sw = Stopwatch.StartNew();
        responseText = await client.Logout();
        sw.Stop();

        lastCallDuration = $"⏱ Duration: {sw.ElapsedMilliseconds} ms";
        Repaint();
    }

    private Texture2D CaptureFromCamera()
    {
        if (selectedCamera == null)
        {
            UnityEngine.Debug.LogWarning("Nessuna camera selezionata.");
            return null;
        }

        int width = 512;
        int height = 512;
        captureRT = new RenderTexture(width, height, 24);
        selectedCamera.targetTexture = captureRT;
        selectedCamera.Render();

        RenderTexture.active = captureRT;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        selectedCamera.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(captureRT);

        return tex;
    }
}
#endif
