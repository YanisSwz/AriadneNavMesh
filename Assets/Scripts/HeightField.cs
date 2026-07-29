using System;
using System.Collections.Generic;
using UnityEngine;

public class Span
{
    public Span(int _min, int _max, AreaID _area)
    {
        min = _min;
        max = _max;
        area = _area;
    }

    public int min = -1;
    public int max = -1;
    public AreaID area = AreaID.NULL;
}

public class Cell
{
    public Cell(int _X, int _Z)
    {
        X = _X;
        Z = _Z;
    }

    public List<Span> spans = new List<Span>();
    public int X = -1;
    public int Z = -1;

    public void AddSpan(int min, int max, AreaID area, int flagMergeThreshold)
    {
        Span newSpan = new Span(min, max, area);
        int index = -1;
        bool broke = false;
        List<Span> oldSpans = new(spans);
        foreach (Span currentSpan in oldSpans)
        {
            ++index;
            if (currentSpan.min > newSpan.max)
            {
                broke = true;
                break;
            }

            if (currentSpan.max < newSpan.min)
            {
                continue;
            }
            else
            {
                // Flag merging
                if (Math.Abs(newSpan.max - currentSpan.max) <= flagMergeThreshold)
                {
                    newSpan.area = (AreaID)Math.Max((int)newSpan.area, (int)currentSpan.area);
                }
                else if(currentSpan.max > newSpan.max)
                {
                    newSpan.area = currentSpan.area;
                }

                // Values merging
                if (currentSpan.min < newSpan.min)
                    newSpan.min = currentSpan.min;
                if (currentSpan.max > newSpan.max)
                    newSpan.max = currentSpan.max;

                spans.RemoveAt(index);
                --index;
            }
        }
        if (!broke)
            spans.Add(newSpan);
        else
            spans.Insert(Mathf.Clamp(index, 0, spans.Count), newSpan);
    }
}

[ExecuteInEditMode]
[RequireComponent(typeof(BoxCollider))]
public class HeightField : MonoBehaviour
{
    public GeometryGetter geometryGetter = null;
    [Range(0.1f, 1f)]
    public float cellSize = 0.5f;
    [Range(0.025f, 1f)]
    public float cellHeight = 0.5f;
    [Range(0, 5)]
    public int flagMergeThreshold = 1;

    [Header("--- Debug ---")]
    [SerializeField] private bool drawDebug = true;
    [SerializeField] private Color boxColor = Color.white;
    [SerializeField] private Color heightColor = Color.white;
    [SerializeField] private Color gridColor = Color.white;
    
    public int cellCount = 10;
    public int cellCountX = 10;
    public int cellCountZ = 10;

    private Vector3 minBounds = Vector3.zero;
    private Vector3 maxBounds = Vector3.zero;
    private BoxCollider boxCollider = new BoxCollider();
    private List<Cell> cells = new List<Cell>();

    public void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    public void Update()
    {
        minBounds = boxCollider.bounds.min;
        maxBounds = boxCollider.bounds.max;
        cellCountX = (int)(boxCollider.size.x / cellSize);
        cellCountZ = (int)(boxCollider.size.z / cellSize);
        cellCount = cellCountX * cellCountZ;

        cells.Clear();
        Vector3 center = Vector3.zero;
        for (int i = 0; i < cellCount; ++i)
        {
            center = new Vector3(cellSize * (i / cellCountZ + 0.5f), cellHeight / 2f, cellSize * (i % cellCountZ + 0.5f)) + minBounds;
            cells.Add(new Cell(i / cellCountZ, i % cellCountZ));
        }

        geometryGetter.GetGeometry();
        if (geometryGetter && geometryGetter.Triangles.Count > 0)
        {
            foreach (Triangle tri in geometryGetter.Triangles)
            {
                RasterizeTriangle(tri.vertices[0], tri.vertices[1], tri.vertices[2], tri.areaID);
            }
        }
    }

    public void OnDrawGizmos()
    {
        if (drawDebug)
        {
            DrawBoundingBox();
            DrawGrid();
        }
    }

    #region Draw functions
    private void DrawGrid()
    {
        if (cells.Count == 0)
            return;

        Gizmos.color = gridColor;

        Vector3 center = Vector3.zero;
        if (cellSize >= 0.2f)
        {
            for (int i = 0; i < cellCount; ++i)
            {
                center = new Vector3(cellSize * (cells[i].X + 0.5f), 0f, cellSize * (cells[i].Z + 0.5f)) + minBounds;
                Gizmos.DrawWireCube(center, new Vector3(cellSize, 0f, cellSize));
            }
        }

        Gizmos.color = heightColor;

        if (cellHeight >= 0.2f)
        {
            for (int j = 0; j < (int)(boxCollider.size.y / cellHeight); ++j)
            {
                center = new Vector3(boxCollider.transform.position.x, cellHeight * (j) + minBounds.y, boxCollider.transform.position.z);
                Gizmos.DrawWireCube(center, new Vector3(boxCollider.size.x, 0f, boxCollider.size.z));
            }
        }

        for (int i = 0; i < cellCount; ++i)
        {
            foreach (Span span in cells[i].spans)
            {
                Vector3 height = new Vector3(cellSize * (cells[i].X + 0.5f), cellHeight * ((span.max + span.min) / 2f), cellSize * (cells[i].Z + 0.5f)) + minBounds;
                Gizmos.color = span.area == AreaID.WALKABLE ? Color.green : Color.red;
                Gizmos.DrawCube(height, new Vector3(cellSize, (span.max - span.min) * cellHeight, cellSize));
            }
        }
        Gizmos.color = Color.white;
    }

    private void DrawBoundingBox()
    {
        Gizmos.color = boxColor;
        Gizmos.DrawWireCube(boxCollider.transform.position, boxCollider.size);
    }
    #endregion

    private void GetBoundingBox(Vector3 point1, Vector3 point2, Vector3 point3, ref Vector3 triMinAABB, ref Vector3 triMaxAABB)
    {
        triMinAABB.x = Mathf.Min(point1.x, Mathf.Min(point2.x, point3.x));
        triMinAABB.y = Mathf.Min(point1.y, Mathf.Min(point2.y, point3.y));
        triMinAABB.z = Mathf.Min(point1.z, Mathf.Min(point2.z, point3.z));

        triMaxAABB.x = Mathf.Max(point1.x, Mathf.Max(point2.x, point3.x));
        triMaxAABB.y = Mathf.Max(point1.y, Mathf.Max(point2.y, point3.y));
        triMaxAABB.z = Mathf.Max(point1.z, Mathf.Max(point2.z, point3.z));
    }

    private void MapToGrid(Vector3 triMinAABB, Vector3 triMaxAABB, ref int x0, ref int x1, ref int z0, ref int z1)
    {
        // Calculate the footprint of the triangle on the grid's x-axis
        x0 = (int)((triMinAABB[0] - minBounds[0]) / cellSize);
        x1 = (int)((triMaxAABB[0] - minBounds[0]) / cellSize);

        x0 = Math.Clamp(x0, 0, cellCountX - 1);
        x1 = Math.Clamp(x1, 0, cellCountX - 1);

        // Calculate the footprint of the triangle on the grid's z-axis
        z0 = (int)((triMinAABB[2] - minBounds[2]) / cellSize);
        z1 = (int)((triMaxAABB[2] - minBounds[2]) / cellSize);

        z0 = Math.Clamp(z0, 0, cellCountZ - 1);
        z1 = Math.Clamp(z1, 0, cellCountZ - 1);
    }

    private List<Vector3> ProcessSegment(Vector3 axis, Vector3 cellPos, Vector3 point1, Vector3 point2)
    {
        List<Vector3> points = new List<Vector3>();
        float D1 = Vector3.Dot(Vector3.Cross(axis, point1 - cellPos), Vector3.up);
        float D2 = Vector3.Dot(Vector3.Cross(axis, point2 - cellPos), Vector3.up);
        // We get away with this because we only ever use X and Z components (on a 2D grid)
        Vector3 axisNormal = Quaternion.AngleAxis(90f, Vector3.up) * axis;

        if (D1 > 0f)
        {
            points.Add(point1);
            if (D2 > 0f)
                points.Add(point2);
            else
                points.Add(ComputeIntersection(point1, point2, cellPos, axisNormal));
        }
        else if (D2 > 0f)
        {
            points.Add(ComputeIntersection(point1, point2, cellPos, axisNormal));
            points.Add(point2);
        }

        return points;
    }

    private Vector3 ComputeIntersection(Vector3 point1, Vector3 point2, Vector3 point3, Vector3 axisNormal)
    {
        Vector3 N = axisNormal.normalized;
        Vector3 V = (point2 - point1).normalized;
        float t = Vector3.Dot(point3 - point1, N) / Vector3.Dot(V, N);
        return point1 + t * V;
    }

    private List<Vector3> ClipPoly(Vector3 axis, Vector3 cellPos, List<Vector3> vertices)
    {
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < vertices.Count; ++i)
        {
            points.AddRange(ProcessSegment(axis, cellPos, vertices[i], vertices[(i + 1) % vertices.Count]));
        }

        return points;
    }

    private void RasterizeTriangle(Vector3 point1, Vector3 point2, Vector3 point3, AreaID triAreaID)
    {
        Vector3 triMinAABB = new Vector3();
        Vector3 triMaxAABB = new Vector3();
        GetBoundingBox(point1, point2, point3, ref triMinAABB, ref triMaxAABB);
        int x0 = -1;
        int x1 = -1;
        int z0 = -1;
        int z1 = -1;
        if (boxCollider.bounds.Intersects(new Bounds((triMinAABB + triMaxAABB) / 2f, triMaxAABB - triMinAABB)))
            MapToGrid(triMinAABB, triMaxAABB, ref x0, ref x1, ref z0, ref z1);
        else
            return;

        for (int z = z0; z <= z1; ++z)
        {
            List<Vector3> polygonPoints = new List<Vector3>();
            polygonPoints.Add(point1);
            polygonPoints.Add(point2);
            polygonPoints.Add(point3);

            Vector3 cellPos = new Vector3(cellSize * x0, cellHeight / 2f, cellSize * z) + minBounds;
            // Clip to row
            polygonPoints = ClipPoly(-Vector3.right, cellPos, polygonPoints);
            polygonPoints = ClipPoly(Vector3.right, cellPos + Vector3.forward * cellSize + Vector3.right * cellSize, polygonPoints);

            for (int x = x0; x <= x1; ++x)
            {
                cellPos = new Vector3(cellSize * x, cellHeight / 2f, cellSize * z) + minBounds;

                List<Vector3> clippedPoly = new List<Vector3>(polygonPoints);
                // Clip to column
                clippedPoly = ClipPoly(Vector3.forward, cellPos, polygonPoints);
                clippedPoly = ClipPoly(-Vector3.forward, cellPos + Vector3.forward * cellSize + Vector3.right * cellSize, clippedPoly);

                if (clippedPoly.Count == 0)
                    continue;

                // Add spans
                float minHeight = Mathf.Infinity;
                float maxHeight = -Mathf.Infinity;

                for (int i = 0; i < clippedPoly.Count; ++i)
                {
                    if (clippedPoly[i].y < minHeight)
                        minHeight = clippedPoly[i].y;
                    if (clippedPoly[i].y > maxHeight)
                        maxHeight = clippedPoly[i].y;
                }

                minHeight -= minBounds[1];
                maxHeight -= minBounds[1];

                // Skip span if completely oustide of heightfield
                if (maxHeight < 0f)
                    continue;
                if (minHeight > maxBounds[1] - minBounds[1])
                    continue;

                // Clamp span to heighfield
                if (minHeight < 0f)
                    minHeight = 0f;
                if (maxHeight > maxBounds[1] - minBounds[1])
                    maxHeight = maxBounds[1] - minBounds[1];

                // Map spans to cell indices
                int minIndex = Math.Clamp((int)Math.Floor(minHeight / cellHeight), 0, (int)(boxCollider.size.y / cellHeight));
                int maxIndex = Math.Clamp((int)Math.Ceiling(maxHeight / cellHeight), 0, (int)(boxCollider.size.y / cellHeight));

                cells[z + x * cellCountZ].AddSpan(minIndex, maxIndex, triAreaID, flagMergeThreshold);
            }
        }
    }
}
