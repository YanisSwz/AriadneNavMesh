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
                else if (currentSpan.max > newSpan.max)
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
    public float walkableClimb = 0.75f;
    public float walkableHeight = 1f;

    [Header("--- Debug ---")]
    [SerializeField] private bool drawDebug = true;
    [SerializeField] private bool filterLowHanging = true;
    [SerializeField] private bool filterLedges = true;
    [SerializeField] private bool filterLowHeight = true;
    [SerializeField] private Color boxColor = Color.white;
    [SerializeField] private Color heightColor = Color.white;
    [SerializeField] private Color gridColor = Color.white;

    public int cellCount = 10;
    public int cellCountX = 10;
    public int cellCountZ = 10;
    public int cellCountY = 10;

    private Vector3 minBounds = Vector3.zero;
    private Vector3 maxBounds = Vector3.zero;
    private BoxCollider boxCollider = new BoxCollider();
    private List<Cell> cells = new List<Cell>();
    private int walkableClimbSpans = 0;
    private int walkableHeightSpans = 0;
    private float inverseCellSize = 1f;
    private float inverseCellHeight = 1f;

    public void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    public void Update()
    {
        CreateHeightField();
    }

    #region Draw functions
    public void OnDrawGizmos()
    {
        if (drawDebug)
        {
            DrawBoundingBox();
            DrawGrid();
        }
    }

    private void DrawGrid()
    {
        if (cells.Count == 0)
            return;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < cellCount; ++i)
        {
            // Cell
            if (cellSize >= 0.2f)
            {
                Gizmos.color = gridColor;
                center = new Vector3(cellSize * (cells[i].X + 0.5f), 0f, cellSize * (cells[i].Z + 0.5f)) + minBounds;
                Gizmos.DrawWireCube(center, new Vector3(cellSize, 0f, cellSize));
            }

            // Spans
            foreach (Span span in cells[i].spans)
            {
                Vector3 height = new Vector3(cellSize * (cells[i].X + 0.5f), cellHeight * ((span.max + span.min) / 2f), cellSize * (cells[i].Z + 0.5f)) + minBounds;
                Gizmos.color = span.area == AreaID.WALKABLE ? Color.green : Color.red;
                Gizmos.DrawCube(height, new Vector3(cellSize, (span.max - span.min) * cellHeight, cellSize));
            }
        }

        Gizmos.color = heightColor;
        if (cellHeight >= 0.2f)
        {
            for (int j = 0; j < cellCountY; ++j)
            {
                center = new Vector3(boxCollider.transform.position.x, cellHeight * j + minBounds.y, boxCollider.transform.position.z);
                Gizmos.DrawWireCube(center, new Vector3(boxCollider.size.x, 0f, boxCollider.size.z));
            }
        }
    }

    private void DrawBoundingBox()
    {
        Gizmos.color = boxColor;
        Gizmos.DrawWireCube(boxCollider.transform.position, boxCollider.size);
    }
    #endregion

    #region Rasterization functions
    private void CreateHeightField()
    {
        minBounds = boxCollider.bounds.min;
        maxBounds = boxCollider.bounds.max;
        inverseCellSize = 1f / cellSize;
        inverseCellHeight = 1f / cellHeight;
        cellCountX = Mathf.FloorToInt(boxCollider.size.x * inverseCellSize);
        cellCountZ = Mathf.FloorToInt(boxCollider.size.z * inverseCellSize);
        cellCount = cellCountX * cellCountZ;
        cellCountY = Mathf.FloorToInt(boxCollider.size.y * inverseCellHeight);
        walkableClimbSpans = Mathf.CeilToInt(walkableClimb * inverseCellHeight);
        walkableHeightSpans = Mathf.CeilToInt(walkableHeight * inverseCellHeight);

        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        cells.Clear();
        for (int i = 0; i < cellCount; ++i)
        {
            cells.Add(new Cell(i % cellCountX, i / cellCountX));
        }

        if (geometryGetter)
        {
            geometryGetter.GetGeometry();
            if (geometryGetter.Triangles.Count > 0)
            {
                
                for(int i = 0; i < geometryGetter.Triangles.Count; ++i)
                {
                    RasterizeTriangle(geometryGetter.Triangles[i].vertices[0], geometryGetter.Triangles[i].vertices[1], geometryGetter.Triangles[i].vertices[2], geometryGetter.Triangles[i].areaID);
                }
                sw.Stop();
                Debug.Log("\nExecution time:  " + sw.Elapsed.TotalMilliseconds + "ms");

                if (filterLowHanging)
                    FilterLowHangingObstacles();
                if (filterLedges)
                    FilterLedgeSpans();
                if (filterLowHeight)
                    FilterLowHeightSpans();
            }
        }
    }

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
        x0 = (int)((triMinAABB[0] - minBounds[0]) * inverseCellSize);
        x1 = (int)((triMaxAABB[0] - minBounds[0]) * inverseCellSize);

        x0 = Math.Clamp(x0, 0, cellCountX - 1);
        x1 = Math.Clamp(x1, 0, cellCountX - 1);

        // Calculate the footprint of the triangle on the grid's z-axis
        z0 = (int)((triMinAABB[2] - minBounds[2]) * inverseCellSize);
        z1 = (int)((triMaxAABB[2] - minBounds[2]) * inverseCellSize);

        z0 = Math.Clamp(z0, 0, cellCountZ - 1);
        z1 = Math.Clamp(z1, 0, cellCountZ - 1);
    }
    
    private Vector3 ComputeIntersection(Vector3 point1, Vector3 point2, Vector3 point3, Vector3 axisNormal)
    {
        Vector3 V = point2 - point1;
        float t = Vector3.Dot(point3 - point1, axisNormal) / Vector3.Dot(V, axisNormal);
        return point1 + t * V;
    }

    private int ProcessSegment(Vector3 axisNormal, Vector3 cellPos, Vector3 point1, Vector3 point2, Span<Vector3> points)
    {
        int pointsCount = 0;

        float D1 = Vector3.Dot(axisNormal, point1 - cellPos);
        float D2 = Vector3.Dot(axisNormal, point2 - cellPos);

        if (D1 > 0f)
        {
            points[0] = point1;
            if (D2 > 0f)
            {
                pointsCount = 1;
            }
            else
            {
                points[1] = ComputeIntersection(point1, point2, cellPos, axisNormal);
                pointsCount = 2;
            }

        }
        else if (D2 > 0f)
        {
            points[0] = ComputeIntersection(point1, point2, cellPos, axisNormal);
            pointsCount = 1;
        }

        return pointsCount;
    }

    private int ClipPoly(Vector3 axisNormal, Vector3 cellPos, Span<Vector3> segmentPoints, Span<Vector3> inputPolygon, int verticesCount, Span<Vector3> outputPolygon)
    {
        int pointsCount = 0;
         
        for (int i = 0; i < verticesCount; ++i)
        {
            int count = ProcessSegment(axisNormal, cellPos, inputPolygon[i], inputPolygon[i + 1 == verticesCount ? 0 : i + 1], segmentPoints);
            if(count >= 1) 
               outputPolygon[pointsCount++] = segmentPoints[0];
            if(count == 2) 
               outputPolygon[pointsCount++] = segmentPoints[1];
        }

        return pointsCount;
    }

    private void RasterizeTriangle(Vector3 point1, Vector3 point2, Vector3 point3, AreaID triAreaID)
    {
        Vector3 triMinAABB = Vector3.zero;
        Vector3 triMaxAABB = Vector3.zero;
        GetBoundingBox(point1, point2, point3, ref triMinAABB, ref triMaxAABB);
        int x0 = -1;
        int x1 = -1;
        int z0 = -1;
        int z1 = -1;

        if (!boxCollider.bounds.Intersects(new Bounds((triMinAABB + triMaxAABB) * 0.5f, triMaxAABB - triMinAABB)))
            return;

        MapToGrid(triMinAABB, triMaxAABB, ref x0, ref x1, ref z0, ref z1);

        Span<Vector3> polygon = stackalloc Vector3[12];
        Span<Vector3> bufferA = stackalloc Vector3[12];
        Span<Vector3> bufferB = stackalloc Vector3[12];
        Span<Vector3> segmentPoints = stackalloc Vector3[2];
        Vector3 cellPos = Vector3.zero;

        for (int z = z0; z <= z1; ++z)
        {
            bufferA[0] = point1;
            bufferA[1] = point2;
            bufferA[2] = point3;

            cellPos.z = cellSize * z + minBounds.z;
            cellPos.x = cellSize * x0 + minBounds.x;

            // Clip to row
            int rowCount = ClipPoly(Vector3.forward, cellPos, segmentPoints, bufferA, 3, bufferB);
            rowCount = ClipPoly(-Vector3.forward, cellPos + Vector3.forward * cellSize, segmentPoints, bufferB, rowCount, polygon);

            for (int x = x0; x <= x1; ++x)
            {
                cellPos.x = cellSize * x + minBounds.x;

                // Clip to column
                int columnCount = ClipPoly(Vector3.right, cellPos, segmentPoints, polygon, rowCount, bufferA);
                columnCount = ClipPoly(-Vector3.right, cellPos + Vector3.right * cellSize, segmentPoints, bufferA, columnCount, bufferB);

                if (columnCount < 3)
                    continue;

                // Add spans
                float minHeight = Mathf.Infinity;
                float maxHeight = -Mathf.Infinity;

                for (int i = 0; i < columnCount; ++i)
                {
                    if (bufferB[i].y < minHeight)
                        minHeight = bufferB[i].y;
                    if (bufferB[i].y > maxHeight)
                        maxHeight = bufferB[i].y;
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
                int minIndex = Math.Clamp(Mathf.FloorToInt(minHeight * inverseCellHeight), 0, cellCountY);
                int maxIndex = Math.Clamp(Mathf.CeilToInt(maxHeight * inverseCellHeight), 0, cellCountY);

                cells[x + z * cellCountX].AddSpan(minIndex, maxIndex, triAreaID, walkableClimbSpans);
            }
        }
    }
    #endregion

    #region Filter functions
    private void FilterLowHangingObstacles()
    {
        for(int i = 0; i < cellCount; ++i) 
        {
            Span previousSpan = null;
            bool previousWalkable = false;
            AreaID previousAreadID = AreaID.NULL;

            foreach(Span span in cells[i].spans) 
            {
                bool walkable = span.area != AreaID.NULL;
                if(!walkable && previousWalkable && span.max - previousSpan.max <= walkableClimbSpans)
                {
                    span.area = previousAreadID;
                }

                previousWalkable = walkable;
                previousAreadID = span.area;
                previousSpan = span;
            }
        }
    }

    private void FilterLedgeSpans() 
    {
        for (int z = 0; z < cellCountZ; ++z)
        {
            for (int x = 0; x < cellCountX; ++x)
            {
                for (int j = 0; j < cells[x + z * cellCountX].spans.Count; ++j)
                {
                    Span span = cells[x + z * cellCountX].spans[j];
                    if (span.area == AreaID.NULL)
                        continue;

                    int floor = span.max;
                    int ceiling = j + 1 < cells[x + z * cellCountX].spans.Count ? cells[x + z * cellCountX].spans[j + 1].min : int.MaxValue;

                    int lowestNeighbourFloorDifference = int.MaxValue;

                    int lowestTraversableNeighbourFloor = span.max;
                    int highestTraversableNeighbourFloor = span.max;

                    for (int direction = 0; direction < 4; ++direction)
                    {
                        int neighbourX = x + GetNeighbourX(direction);
                        int neighbourZ = z + GetNeighbourZ(direction);

                        if(neighbourX < 0 || neighbourZ < 0 || neighbourX >= cellCountX || neighbourZ >= cellCountZ) 
                        {
                            lowestNeighbourFloorDifference = -walkableClimbSpans - 1;
                            break;
                        }

                        Span neighbourSpan = cells[neighbourX + neighbourZ * cellCountX].spans.Count > 0 ? cells[neighbourX + neighbourZ * cellCountX].spans[0] : null;
                        int neighbourCeiling = neighbourSpan != null ? neighbourSpan.min : int.MaxValue;

                        if (Math.Min(ceiling, neighbourCeiling) - floor >= walkableHeightSpans)
                        {
                            lowestNeighbourFloorDifference = -walkableClimbSpans - 1;
                            break;
                        }

                        for (int k = 0; k < cells[neighbourX + neighbourZ * cellCountX].spans.Count; ++k) 
                        {
                            neighbourSpan = cells[neighbourX + neighbourZ * cellCountX].spans[k];
                            int neighbourFloor = neighbourSpan.max;
                            neighbourCeiling = k + 1 < cells[neighbourX + neighbourZ * cellCountX].spans.Count ? cells[neighbourX + neighbourZ * cellCountX].spans[k+1].min : int.MaxValue;

                            if (Math.Min(ceiling, neighbourCeiling) - Math.Max(floor, neighbourFloor) < walkableHeightSpans)
                            {
                                continue;
                            }

                            int neighbourFloorDifference = neighbourFloor - floor;
                            lowestNeighbourFloorDifference = Math.Min(lowestNeighbourFloorDifference, neighbourFloorDifference);

                            if (Math.Abs(neighbourFloorDifference) <= walkableClimbSpans)
                            {
                                lowestTraversableNeighbourFloor = Math.Min(lowestTraversableNeighbourFloor, neighbourFloor);
                                highestTraversableNeighbourFloor = Math.Max(highestTraversableNeighbourFloor, neighbourFloor);
                            }
                            else if(neighbourFloorDifference < -walkableClimbSpans) 
                            {
                                break;
                            }
                        }
                    }

                    if(lowestNeighbourFloorDifference < -walkableClimbSpans) 
                    {
                        span.area = AreaID.NULL;
                    }
                    else if(highestTraversableNeighbourFloor - lowestTraversableNeighbourFloor > walkableClimbSpans)
                    {  
                        span.area = AreaID.NULL; 
                    }
                }
            }
        }
    }

    private int GetNeighbourX(int index) 
    {
        int[] offset = { -1, 0, 1, 0 };
        return offset[index];
    }

    private int GetNeighbourZ(int index)
    {
        int[] offset = { 0, 1, 0, -1 };
        return offset[index];
    }

    private void FilterLowHeightSpans() 
    {
        for(int i = 0; i < cellCount; ++i) 
        {
            for(int j = 0; j < cells[i].spans.Count; ++j) 
            {
                int floor = cells[i].spans[j].max;
                int ceiling = j + 1 < cells[i].spans.Count ? cells[i].spans[j + 1].min : int.MaxValue;
            
                if(ceiling - floor < walkableHeightSpans)
                {
                    cells[i].spans[j].area = AreaID.NULL;
                }
            }
        }
    }
    #endregion
}
