using System.Collections.Generic;
using UnityEngine;

public enum AreaID
{
    NULL = 0,
    WALKABLE = int.MaxValue,
}

public struct Triangle
{
    public Triangle(Vector3 point1, Vector3 point2, Vector3 point3, Vector3 normal, AreaID areaID = AreaID.NULL)
    {
        this.point1 = point1;
        this.point2 = point2;
        this.point3 = point3;
        this.normal = normal;
        this.areaID = areaID;
    }

    public Vector3 point1;
    public Vector3 point2;
    public Vector3 point3;
    public Vector3 normal;
    public AreaID areaID;
}

[ExecuteInEditMode]
public class AriadneNavMesh : MonoBehaviour
{
    #region Properties
    [Header("--- Volume ---")]
    [SerializeField] private Vector3 size = Vector3.one;
    [SerializeField] private Vector3 center = Vector3.zero;

    [Header("--- Voxelization ---")]
    [Range(0.1f, 1f)]
    [SerializeField] private float cellSize = 0.5f;
    [Range(0.025f, 1f)]
    [SerializeField] private float cellHeight = 0.5f;

    [Header("--- Walkability ---")]
    [Range(0f, 90f)]
    [SerializeField] private float walkableSlopeAngle = 45f;
    [SerializeField] private float walkableClimb = 0.75f;
    [SerializeField] private float walkableHeight = 1f;

    [Header("--- Filters ---")]
    [SerializeField] private bool filterLowHanging = true;
    [SerializeField] private bool filterLedges = true;
    [SerializeField] private bool filterLowHeight = true;

    [Header("--- Debug ---")]
    [Space(8)]
    [SerializeField] private Color boxColor = Color.white;

    [Space(8)]
    [SerializeField] bool drawGeometryGetterDebug = false;

    [Space(8)]
    [SerializeField] bool drawHeightFieldDebug = false;
    [SerializeField] private Color heightColor = Color.white;
    [SerializeField] private Color gridColor = Color.white;
    [SerializeField] private Color walkableColor = Color.green;
    [SerializeField] private Color notWalkableColor = Color.red;

    private HeightField heightField = new HeightField();
    private int verticesCount = 0;
    private List<Triangle> triangles = new List<Triangle>();
    private List<Transform> trackedTransforms = new List<Transform>();
    public bool geometryChanged = false;
    #endregion

    #region Methods
    private void OnEnable()
    {
        BuildNavMesh();
        UnityEditor.EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private void OnDisable()
    {
        UnityEditor.EditorApplication.hierarchyChanged -= OnHierarchyChanged;
    }

    private void OnHierarchyChanged()
    {
        geometryChanged = true;
    }

    private void OnValidate()
    {
        geometryChanged = true;
    }

    private void Update()
    {
        for(int i = 0; i < trackedTransforms.Count; ++i) 
        {
            if(trackedTransforms[i] == null)
            {
                geometryChanged = true;
            }
            else if (trackedTransforms[i].hasChanged) 
            {
                geometryChanged = true;
                trackedTransforms[i].hasChanged = false;
            }
        }

        if (geometryChanged)
        {
            BuildNavMesh();
            geometryChanged = false;
        }
    }

    private void GetGeometry()
    {
        trackedTransforms.Clear();
        trackedTransforms.Add(transform);
        triangles.Clear();
        verticesCount = 0;

        float walkableThreshold = Mathf.Cos(walkableSlopeAngle * Mathf.Deg2Rad);

        MeshFilter[] meshes = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        foreach (MeshFilter filter in meshes)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;

            trackedTransforms.Add(filter.transform);

            if (!heightField.Bounds.Intersects(filter.GetComponent<Renderer>().bounds))
                continue;

            Vector3[] meshVertices = mesh.vertices;
            int[] meshTriangles = mesh.triangles;
            Vector3[] meshNormals = mesh.normals;

            verticesCount += meshVertices.Length;
            Matrix4x4 localToWorld = filter.transform.localToWorldMatrix;
            Quaternion localToWorldRotation = filter.transform.rotation;
            for (int i = 0; i < meshTriangles.Length; i += 3)
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

    public void BuildNavMesh()
    {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        GetGeometry();
        heightField.CreateHeightField(triangles, size, center + transform.position, cellSize, cellHeight, walkableClimb, walkableHeight, filterLowHanging, filterLedges, filterLowHeight);

        sw.Stop();
        Debug.Log("\nExecution time:  " + sw.Elapsed.TotalMilliseconds + "ms" +
            "\n" + verticesCount + " vertices" +
            "\n" + triangles.Count + " triangles"
            );
    }
    #endregion

    #region Draw methods
    private void OnDrawGizmos()
    {
        if (drawGeometryGetterDebug)
        {
            DrawGeometry();
        }
        if (drawHeightFieldDebug)
        {
            DrawHeightField();
        }
        DrawBoundingBox();
    }

    private void DrawGeometry()
    {
        for (int i = 0; i < triangles.Count; ++i)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(triangles[i].point1, triangles[i].point2);
            Gizmos.DrawLine(triangles[i].point2, triangles[i].point3);
            Gizmos.DrawLine(triangles[i].point3, triangles[i].point1);

            Gizmos.color = triangles[i].areaID == AreaID.WALKABLE ? Color.green : Color.red;
            Vector3 center = (triangles[i].point1 + triangles[i].point2 + triangles[i].point3) / 3f;
            Gizmos.DrawLine(center, center + triangles[i].normal);
        }
    }

    private void DrawHeightField()
    {
        DrawGrid();
    }

    private void DrawBoundingBox()
    {
        Gizmos.color = boxColor;
        Gizmos.DrawWireCube(center + transform.position, size);
    }

    private void DrawGrid()
    {
        List<Cell> cells = heightField.Cells;
        if (cells.Count == 0)
            return;

        Vector3 minBounds = (center + transform.position) - size * 0.5f;
        Vector3 pos = Vector3.zero;
        for (int i = 0; i < cells.Count; ++i)
        {
            // Cell
            if (heightField.CellSize >= 0.2f)
            {
                Gizmos.color = gridColor;
                pos = new Vector3(heightField.CellSize * (cells[i].X + 0.5f), 0f, heightField.CellSize * (cells[i].Z + 0.5f)) + minBounds;
                Gizmos.DrawWireCube(pos, new Vector3(heightField.CellSize, 0f, heightField.CellSize));
            }

            // Spans
            foreach (Span span in cells[i].spans)
            {
                Vector3 height = new Vector3(heightField.CellSize * (cells[i].X + 0.5f), heightField.CellHeight * ((span.max + span.min) / 2f), heightField.CellSize * (cells[i].Z + 0.5f)) + minBounds;
                Gizmos.color = span.area == AreaID.WALKABLE ? walkableColor : notWalkableColor;
                Gizmos.DrawCube(height, new Vector3(heightField.CellSize, (span.max - span.min) * heightField.CellHeight, heightField.CellSize));
            }
        }

        Gizmos.color = heightColor;
        if (heightField.CellHeight >= 0.2f)
        {
            for (int j = 0; j < heightField.CellCountY; ++j)
            {
                pos = new Vector3(center.x + transform.position.x, heightField.CellHeight * j + minBounds.y, center.z + transform.position.z);
                Gizmos.DrawWireCube(pos, new Vector3(size.x, 0f, size.z));
            }
        }
    }
    #endregion
}
