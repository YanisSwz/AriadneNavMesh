using System.Collections.Generic;
using UnityEngine;

public class AriadneConstants
{
    public const float CLIP_EPSILON = 1e-5f;
    public const int MAX_HEIGHT = 1 << 20;

    public static readonly int[] NeighbourX = { -1, 0, 1, 0 };
    public static readonly int[] NeighbourZ = { 0, 1, 0, -1 };
}

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

    [Header("--- Erosion ---")]
    [Range(0f, 5f)]
    [SerializeField] private float agentRadius = 0.5f;

    [Header("--- Regions ---")]
    [Range(0f, 5f)]
    [SerializeField] private float minRegionArea = 0.5f;

    [Header("--- Debug ---")]
    [Space(8)]
    [SerializeField] private Color boxColor = Color.white;

    [Space(8)]
    [SerializeField] private bool drawGeometryGetterDebug = false;

    [Space(8)]
    [SerializeField] private bool drawHeightFieldDebug = false;
    [SerializeField] private Color heightColor = Color.white;
    [SerializeField] private Color gridColor = Color.white;
    [SerializeField] private Color walkableColor = Color.green;
    [SerializeField] private Color notWalkableColor = Color.red;

    [Space(8)]
    [SerializeField] private bool drawCompactHeightFieldDebug = false;
    [SerializeField] private bool drawDistanceFieldDebug = false;
    [SerializeField] private bool drawRegionsDebug = false;
    [Range(0.1f, 5f)]
    [SerializeField] private float spansDisplayHeight = 1f;

    private HeightField heightField = new HeightField();
    private CompactHeightField compactHeightField = new CompactHeightField();
    private int verticesCount = 0;
    private List<Triangle> triangles = new List<Triangle>();
    private List<Transform> trackedTransforms = new List<Transform>();
    private bool geometryChanged = false;
    private List<Color> colors = new List<Color>();
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
        for (int i = 0; i < trackedTransforms.Count; ++i)
        {
            if (trackedTransforms[i] == null)
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
        compactHeightField.BuildCompactHeightField(heightField);
        compactHeightField.ErodeWalkableArea(Mathf.CeilToInt(agentRadius / cellSize));
        compactHeightField.BuildDistanceField();
        int minRegionAreaVoxel = Mathf.CeilToInt(minRegionArea / cellSize);
        compactHeightField.BuildRegions(minRegionAreaVoxel * minRegionAreaVoxel);

        sw.Stop();
        Debug.Log("\nExecution time:  " + sw.Elapsed.TotalMilliseconds + "ms" +
            "\n" + verticesCount + " vertices" +
            "\n" + triangles.Count + " triangles"
            );

        for (int i = colors.Count; i < compactHeightField.RegionCount; ++i)
        {
            colors.Add(new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f)));
        }
    }
    #endregion

    #region Draw methods
    private void OnDrawGizmos()
    {
        DrawBoundingBox();

        if (drawGeometryGetterDebug)
        {
            DrawGeometry();
        }
        if (drawHeightFieldDebug)
        {
            DrawHeightField();
        }
        if (drawCompactHeightFieldDebug)
        {
            DrawCompactHeightField();
        }
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
        List<Cell> cells = heightField.Cells;
        if (cells.Count == 0)
            return;

        Vector3 minBounds = (center + transform.position) - size * 0.5f;
        Vector3 pos = Vector3.zero;
        for (int i = 0; i < cells.Count; ++i)
        {
            // Cell
            if (cellSize >= 0.2f)
            {
                Gizmos.color = gridColor;
                pos = new Vector3(cellSize * (i % heightField.CellCountX + 0.5f), 0f, cellSize * (i / heightField.CellCountX + 0.5f)) + minBounds;
                Gizmos.DrawWireCube(pos, new Vector3(cellSize, 0f, cellSize));
            }

            // Spans
            foreach (Span span in cells[i].spans)
            {
                pos = new Vector3(cellSize * (i % heightField.CellCountX + 0.5f), cellHeight * ((span.max + span.min) / 2f), cellSize * (i / heightField.CellCountX + 0.5f)) + minBounds;
                Gizmos.color = span.area == AreaID.WALKABLE ? walkableColor : notWalkableColor;
                Gizmos.DrawCube(pos, new Vector3(cellSize, (span.max - span.min) * cellHeight, cellSize));
            }
        }

        Gizmos.color = heightColor;
        if (cellHeight >= 0.2f)
        {
            for (int j = 0; j < heightField.CellCountY; ++j)
            {
                pos = new Vector3(center.x + transform.position.x, cellHeight * j + minBounds.y, center.z + transform.position.z);
                Gizmos.DrawWireCube(pos, new Vector3(size.x, 0f, size.z));
            }
        }
    }

    private void DrawBoundingBox()
    {
        Gizmos.color = boxColor;
        Gizmos.DrawWireCube(center + transform.position, size);
    }

    private void DrawCompactHeightField()
    {
        List<CompactCell> cells = compactHeightField.Cells;
        List<CompactSpan> spans = compactHeightField.Spans;
        List<AreaID> areas = compactHeightField.Areas;
        List<int> distances = compactHeightField.Distances;
        int maxDistance = compactHeightField.MaxDistance;

        if (cells.Count == 0 || spans.Count == 0)
            return;

        Vector3 minBounds = (center + transform.position) - size * 0.5f;
        for (int i = 0; i < cells.Count; ++i)
        {
            // Spans
            for (int j = cells[i].index; j < cells[i].index + cells[i].count; ++j)
            {
                CompactSpan span = spans[j];
                Vector3 position = new Vector3(cellSize * (i % heightField.CellCountX + 0.5f), span.y * cellHeight + spansDisplayHeight * 0.5f, cellSize * (i / heightField.CellCountX + 0.5f)) + minBounds;

                if (drawRegionsDebug)
                {
                    if (spans[j].regionID == 0)
                        continue;

                    Gizmos.color = colors[spans[j].regionID];
                }
                else if (drawDistanceFieldDebug)
                {
                    float color = (float)(distances[j]) / (float)(maxDistance);
                    Gizmos.color = new Color(color, color, color);
                }
                else
                {
                    Gizmos.color = areas[j] == AreaID.WALKABLE ? walkableColor : notWalkableColor;
                }
                Gizmos.DrawCube(position, new Vector3(cellSize, spansDisplayHeight, cellSize));
            }
        }
    }
    #endregion
}
