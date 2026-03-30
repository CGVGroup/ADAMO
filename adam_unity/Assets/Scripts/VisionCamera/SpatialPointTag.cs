using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

[Serializable]
[ExecuteInEditMode] // Ensures OnValidate is called reliably, especially on scene load and object creation.
public class SpatialPointTag : MonoBehaviour
{
    [SerializeField] private int id = -1;
    public int Id => this.id;
    public void SetId(int id) { this.id = id; }
    
#if UNITY_EDITOR
    [Header("Gizmo Visualization")]
    [SerializeField] private float gizmoRadius = 0.05f;
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private Color labelColor = Color.white;

    private void OnDrawGizmos()
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = labelColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Handles.BeginGUI();

        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(this.transform.position, gizmoRadius);
        
        Vector2 guiPoint = HandleUtility.WorldToGUIPoint(this.transform.position);
        Rect labelRect = new Rect(guiPoint.x - 20f, guiPoint.y - 10f, 40f, 20f);
        GUI.Label(labelRect, "P" + this.id.ToString(), style);
        
        Handles.EndGUI();
    }
#endif
    
}