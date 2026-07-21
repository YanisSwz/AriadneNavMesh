using System;
using System.Collections.Generic;
using UnityEngine;

public class Span
{
    public Span(float _min, float _max)
    {
        min = _min;
        max = _max;
    }

    public float min = -1;
    public float max = -1;

    public bool Intersects(Span other)
    {
        return (other.min >= min && other.min <= max) || (other.max <= max && other.max >= min);
    }
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

    public void AddSpan(float min, float max)
    {
        Span newSpan = new Span(min, max);
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
    [SerializeField] Transform v0;
    [SerializeField] Transform v1;
    [SerializeField] Transform v2;
    [SerializeField] Color spanColor = Color.white;
    [SerializeField] Color boxColor = Color.white;

    int z0 = 0;
    int z1 = 0;
    int x0 = 0;
    int x1 = 0;

    public GeometryGetter geometryGetter = null;
    public int cellCount = 10;
    public int cellCountX = 10;
    public int cellCountZ = 10;
    [Range(0.1f, 1f)]
    public float cellSize = 1;
    public float cellHeight = 2;
    public Vector3 minBounds = Vector3.zero;
    public Vector3 maxBounds = Vector3.zero;
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

        //RasterizeTriangle(v0.position, v1.position, v2.position);
        geometryGetter.GetAllVertices();
        if (geometryGetter && geometryGetter.Vertices.Count > 0)
        {
            for (int i = 0; i < geometryGetter.Indices.Count; i += 3)
            {
                Vector3 v0 = geometryGetter.Vertices[geometryGetter.Indices[i]];
                Vector3 v1 = geometryGetter.Vertices[geometryGetter.Indices[i + 1]];
                Vector3 v2 = geometryGetter.Vertices[geometryGetter.Indices[i + 2]];
                RasterizeTriangle(v0, v1, v2);
            }
        }
    }

    public void OnDrawGizmos()
    {
        DrawBoundingBox();
        DrawGrid();
    }

    #region Draw functions
    private void DrawGrid()
    {
        if (cells.Count == 0)
            return;

        if (cellSize >= 0.15f)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < cellCount; ++i)
            {
                center = new Vector3(cellSize * (cells[i].X + 0.5f), cellHeight / 2f, cellSize * (cells[i].Z + 0.5f)) + minBounds;
                Gizmos.DrawWireCube(center, new Vector3(cellSize, cellHeight, cellSize));
            }
        }

        Gizmos.color = spanColor;

        for (int i = 0; i < cellCount; ++i)
        {
            foreach (Span span in cells[i].spans)
            {
                Vector3 height = new Vector3(cellSize * (cells[i].X + 0.5f) + minBounds.x, (span.max + span.min) / 2f, cellSize * (cells[i].Z + 0.5f) + minBounds.z);
                Gizmos.DrawCube(height, new Vector3(cellSize, span.max - span.min, cellSize));
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

    private void MapToGrid(Vector3 triMinAABB, Vector3 triMaxAABB)
    {
        // Calculate the footprint of the triangle on the grid's z-axis
        z0 = (int)((triMinAABB[2] - minBounds[2]) / cellSize);
        z1 = (int)((triMaxAABB[2] - minBounds[2]) / cellSize);

        z0 = Math.Clamp(z0, 0, cellCountZ - 1);
        z1 = Math.Clamp(z1, 0, cellCountZ - 1);


        // Calculate the footprint of the triangle on the grid's x-axis
        x0 = (int)((triMinAABB[0] - minBounds[0]) / cellSize);
        x1 = (int)((triMaxAABB[0] - minBounds[0]) / cellSize);

        x0 = Math.Clamp(x0, 0, cellCountX - 1);
        x1 = Math.Clamp(x1, 0, cellCountX - 1);
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

    private void RasterizeTriangle(Vector3 point1, Vector3 point2, Vector3 point3)
    {
        Vector3 triMinAABB = new Vector3();
        Vector3 triMaxAABB = new Vector3();
        GetBoundingBox(point1, point2, point3, ref triMinAABB, ref triMaxAABB);
        if (boxCollider.bounds.Intersects(new Bounds((triMinAABB + triMaxAABB) / 2f, triMaxAABB - triMinAABB)))
            MapToGrid(triMinAABB, triMaxAABB);
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

                // Skip span if completely oustide of heightfield
                if (maxHeight < boxCollider.bounds.min.y)
                    continue;
                if (minHeight > boxCollider.bounds.max.y)
                    continue;

                // Clamp span to heighfield
                if (minHeight < boxCollider.bounds.min.y)
                    minHeight = boxCollider.bounds.min.y;
                if (maxHeight > boxCollider.bounds.max.y)
                    maxHeight = boxCollider.bounds.max.y;

                cells[z + x * cellCountZ].AddSpan(minHeight, maxHeight);
            }
        }
    }
}
