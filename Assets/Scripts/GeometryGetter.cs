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
    public Triangle(Vector3 _point1, Vector3 _point2, Vector3 _point3, Vector3 _normal, AreaID _areaID = AreaID.NULL) 
    {
        point1 = _point1;
        point2 = _point2;
        point3 = _point3;
        normal = _normal;
        areaID = _areaID;
    }

    public Vector3 point1;
    public Vector3 point2;
    public Vector3 point3;
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
    public int VerticesCount {  get { return verticesCount; } }

    private void OnDrawGizmos()
    {
        if (drawDebug)
        {
            foreach (Triangle tri in triangles)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(tri.point1, tri.point2);
                Gizmos.DrawLine(tri.point2, tri.point3);
                Gizmos.DrawLine(tri.point3, tri.point1);

                Gizmos.color = tri.areaID == AreaID.WALKABLE ? Color.green : Color.red;
                Vector3 center = (tri.point1 + tri.point2 + tri.point3) / 3f;
                Gizmos.DrawLine(center, center + tri.normal);
            }
        }
    }

    public void ClearGeometry()
    {
        triangles.Clear();
        Debug.Log("Cleared geometry");
    }

    // TODO: dirty flag system and cache meshes
    public void GetGeometry() 
    {
        //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        //sw.Start();
        triangles.Clear();
        verticesCount = 0;

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
                Matrix4x4 localToWorld = filter.transform.localToWorldMatrix;
                Quaternion localToWorldRotation = filter.transform.rotation;
                for (int i = 0; i < meshTriangles.Length; i+=3)
                {
                    Triangle tri = new Triangle
                        (
                            localToWorld.MultiplyPoint3x4(meshVertices[meshTriangles[i]]),
                            localToWorld.MultiplyPoint3x4(meshVertices[meshTriangles[i + 1]]),
                            localToWorld.MultiplyPoint3x4(meshVertices[meshTriangles[i + 2]]),
                            
                            localToWorldRotation * meshNormals[meshTriangles[i]]
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
