using System;
using System.Collections.Generic;
using UnityEngine;

public enum AreaID 
{
    NULL = 0,
    WALKABLE = int.MaxValue,
}

public struct Triangle 
{
    public Triangle(Vector3[] _vertices, Vector3 _normal, AreaID _areaID = AreaID.NULL) 
    {
        vertices = _vertices;
        normal = _normal;
        areaID = _areaID;
    }

    public Vector3[] vertices;
    public Vector3 normal;
    public AreaID areaID;
}

public class GeometryGetter : MonoBehaviour
{
    [SerializeField] private bool drawDebug = true;
    [Range(0f, 90f)]
    public float walkableSlopeAngle = 60f;

    private List<Triangle> triangles = new List<Triangle>();
    public List<Triangle> Triangles { get { return triangles; } }
    private int verticesCount = 0;

    private void OnDrawGizmos()
    {
        if (drawDebug)
        {
            foreach (Triangle tri in triangles)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(tri.vertices[0], tri.vertices[1]);
                Gizmos.DrawLine(tri.vertices[1], tri.vertices[2]);
                Gizmos.DrawLine(tri.vertices[2], tri.vertices[0]);

                Gizmos.color = tri.areaID == AreaID.WALKABLE ? Color.green : Color.red;
                Vector3 center = (tri.vertices[0] + tri.vertices[1] + tri.vertices[2]) / 3f;
                Gizmos.DrawLine(center, center + tri.normal);
            }
        }
    }

    public void ClearGeometry()
    {
        triangles.Clear();
        Debug.Log("Cleared geometry");
    }

    public void GetGeometry() 
    {
        //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        //sw.Start();
        triangles.Clear();

        MeshFilter[] meshes = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        float walkableThreshold = Mathf.Cos(walkableSlopeAngle * Mathf.Deg2Rad);

        foreach (MeshFilter filter in meshes) 
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh != null)
            {
                Vector3[] meshVertices = mesh.vertices;
                int[] meshTriangles = mesh.triangles;
                Vector3[] meshNormals = mesh.normals;

                verticesCount += meshVertices.Length;

                for (int i = 0; i < meshTriangles.Length; i+=3)
                {
                    Triangle tri = new Triangle
                        (
                            new Vector3[]
                            {
                                filter.transform.TransformPoint(meshVertices[meshTriangles[i]]),
                                filter.transform.TransformPoint(meshVertices[meshTriangles[i + 1]]),
                                filter.transform.TransformPoint(meshVertices[meshTriangles[i + 2]])
                            },
                            filter.transform.rotation * meshNormals[meshTriangles[i]]
                        );

                    tri.areaID = tri.normal.y > walkableThreshold ? AreaID.WALKABLE : AreaID.NULL;

                    triangles.Add(tri);
                }
            }
        }
        //sw.Stop();
        //Debug.Log(
        //    "\nExecuted in " + sw.Elapsed.TotalMilliseconds + "ms"
        //    + "\n" + verticesCount + " vertices in scene"
        //    + "\n" + triangles.Count + " triangles in scene"
        //    );
    }
}
