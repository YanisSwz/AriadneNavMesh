using System;
using System.Collections.Generic;
using UnityEngine;

public class GeometryGetter : MonoBehaviour
{
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    public List<Vector3> Vertices {  get { return vertices; } }
    public List<int> Indices { get { return triangles; } }

    void Start()
    {
        
    }

    private void OnDrawGizmos()
    {
        for (int i = 0; i < triangles.Count - 3; i += 3)
        {
            Gizmos.DrawLine(vertices[triangles[i]], vertices[triangles[i + 1]]);
            Gizmos.DrawLine(vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
            Gizmos.DrawLine(vertices[triangles[i + 2]], vertices[triangles[i]]);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ClearAllVertices()
    {
        vertices.Clear();
        triangles.Clear();

        Debug.Log("Cleared geometry");
    }

    public void GetAllVertices() 
    {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        vertices.Clear();
        triangles.Clear();

        MeshFilter[] meshes = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        
        foreach (MeshFilter filter in meshes) 
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh != null)
            {
                int[] meshTriangles = mesh.triangles;
                Vector3[] meshVertices = mesh.vertices;
                int currentVertices = vertices.Count;
                for (int i = 0; i < meshVertices.Length; i++) 
                {
                    vertices.Add(filter.transform.TransformPoint(meshVertices[i]));
                }
                
                for (int i = 0; i < meshTriangles.Length; i++)
                {
                    triangles.Add(meshTriangles[i] + currentVertices);
                }
            }
        }
        sw.Stop();
        Debug.Log(
            "\nExecuted in " + sw.Elapsed.TotalMilliseconds + "ms"
            + "\n" + vertices.Count + " vertices in scene"
            + "\n" + triangles.Count / 3 + " triangles in scene"
            );
    }
}
