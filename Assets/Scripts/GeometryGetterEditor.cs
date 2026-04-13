using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GeometryGetter))]
public class GeometryGetterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GeometryGetter geometryGetter = (GeometryGetter)target;

        if (GUILayout.Button("Clear Geometry"))
        {
            geometryGetter.ClearAllVertices();
        }
        else if (GUILayout.Button("Get Geometry"))
        {
            geometryGetter.GetAllVertices();
        }
    }
}
