using System;
using System.Collections.Generic;
using UnityEngine;

public class Cell
{
    public Cell(int _X, int _Z, float _minSpan = 0f, float _maxSpan = 0f)
    {
        X = _X;
        Z = _Z;
        minSpan = _minSpan;
        maxSpan = _maxSpan;
    }

    public float minSpan;
    public float maxSpan;
    public int X;
    public int Z;
}

[ExecuteInEditMode]
[RequireComponent(typeof(BoxCollider))]
public class HeightField : MonoBehaviour
{
    [SerializeField] Transform v0;
    [SerializeField] Transform v1;
    [SerializeField] Transform v2;

    int z0 = 0;
    int z1 = 0;
    int x0 = 0;
    int x1 = 0;

    public GeometryGetter geometryGetter = null;
    public int cellCount = 10;
    public int cellCountX = 10;
    public int cellCountZ = 10;
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
            cells.Add(new Cell(i / cellCountZ, i % cellCountZ, minBounds.y, minBounds.y));
        }

        RasterizeTriangle(v0.position, v1.position, v2.position);


        //if (geometryGetter && geometryGetter.Vertices.Count > 0)
        //{
        //    for (int i = 0; i < geometryGetter.Indices.Count; i += 3)
        //    {
        //        Vector3 v0 = geometryGetter.Vertices[geometryGetter.Indices[i]];
        //        Vector3 v1 = geometryGetter.Vertices[geometryGetter.Indices[i + 1]];
        //        Vector3 v2 = geometryGetter.Vertices[geometryGetter.Indices[i + 2]];
        //        RasterizeTriangle(v0, v1, v2);
        //    }
        //}
    }

    public void OnDrawGizmos()
    {
        DrawGrid();
    }

    #region Draw functions
    private void DrawGrid()
    {
        if (cells.Count == 0)
            return;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < cellCount; ++i)
        {
            center = new Vector3(cellSize * (cells[i].X + 0.5f), cellHeight / 2f, cellSize * (cells[i].Z + 0.5f)) + minBounds;
            Gizmos.DrawWireCube(center, new Vector3(cellSize, cellHeight, cellSize));
        }

        Gizmos.color = Color.green;
        for (int i = 0; i < cellCount; ++i)
        {
            Vector3 height = new Vector3(cellSize * (cells[i].X + 0.5f) + minBounds.x, (cells[i].maxSpan + cells[i].minSpan) / 2f, cellSize * (cells[i].Z + 0.5f) + minBounds.z);
            if (cells[i].maxSpan - cells[i].minSpan > 0f)
                Gizmos.DrawCube(height, new Vector3(cellSize, cells[i].maxSpan - cells[i].minSpan, cellSize));
        }
        Gizmos.color = Color.white;
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

        // use -1 rather than 0 to cut the polygon properly at the start of the tile
        z0 = Math.Clamp(z0, -1, cellCountZ - 1);
        z1 = Math.Clamp(z1, 0, cellCountZ - 1);


        // Calculate the footprint of the triangle on the grid's x-axis
        x0 = (int)((triMinAABB[0] - minBounds[0]) / cellSize);
        x1 = (int)((triMaxAABB[0] - minBounds[0]) / cellSize);

        // use -1 rather than 0 to cut the polygon properly at the start of the tile
        x0 = Math.Clamp(x0, -1, cellCountX - 1);
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

                cells[z + x * cellCountZ].minSpan = minHeight;
                cells[z + x * cellCountZ].maxSpan = maxHeight;
            }
        }
    }
}
