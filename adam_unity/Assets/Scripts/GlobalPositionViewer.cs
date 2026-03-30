using UnityEngine;

public class GlobalPositionViewer : MonoBehaviour
{
    [SerializeField] private Vector3 globalPosition;


    // Update is called once per frame
    void Update()
    {
        globalPosition = this.transform.position;
    }
}
