using System.IO;
using UnityEngine;

public class TestCameraCapture : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private Texture2D tex;
    [SerializeField] private string imageRelativePath;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            SaveCapture();
        if(Input.GetKey(KeyCode.A))
            this.transform.position -= this.transform.right * Time.deltaTime;
        if(Input.GetKey(KeyCode.D))
            this.transform.position += this.transform.right * Time.deltaTime;
        if(Input.GetKey(KeyCode.W))
            this.transform.position += this.transform.up * Time.deltaTime;
        if(Input.GetKey(KeyCode.S))
            this.transform.position -= this.transform.up * Time.deltaTime;
        if (Input.GetKey(KeyCode.E))
            this.transform.position -= this.transform.forward * Time.deltaTime;
        if(Input.GetKey(KeyCode.Q))
            this.transform.position += this.transform.forward * Time.deltaTime;
    }
    
    public void SaveCapture()
    {
        int width = 1920;
        int height = 1080;
        RenderTexture rt = new RenderTexture(width, height, 24);
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        cam.targetTexture = rt;
        RenderTexture.active = rt;

        cam.Render();
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        
        cam.targetTexture = null;
        RenderTexture.active = null;

        SaveImageToFileSystem();
    }
    
    public void SaveImageToFileSystem()
    {
        // Converte in JPG
        byte[] bytes = CameraManager.Instance.EncodeToJPGCustom(tex);
        
        string imageRelPath = $"CameraCaptures/PLACEHOLDER_{System.DateTime.Now.ToString("yyyy-M-d_hh-mm-ss")}.jpg";
        Debug.Log(imageRelPath);
        
        // Path di salvataggio (es. nella cartella persistente dell'app)
        string path = Path.Combine(Application.dataPath, imageRelPath);
        
        Debug.Log(path);
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        File.WriteAllBytes(path, bytes);
        
        Debug.Log($"Screenshot salvato in: {path}");
    }
}
