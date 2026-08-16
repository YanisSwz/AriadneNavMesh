using System;
using System.Collections.Generic;
using UnityEngine;

public struct Span
{
    public Span(int _min, int _max, AreaID _area)
    {
        min = _min;
        max = _max;
        area = _area;
    }

    public int min;
    public int max;
    public AreaID area;
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

    public void Reset()
    {
        spans.Clear();
    }

    public void AddSpan(int min, int max, AreaID area, int flagMergeThreshold)
    {
        Span newSpan = new Span(min, max, area);
        int index = 0;
        bool broke = false;
        while (index < spans.Count)
        {
            Span currentSpan = spans[index];

            if (currentSpan.min > newSpan.max)
            {
                broke = true;
                break;
            }

            if (currentSpan.max < newSpan.min)
            {
                ++index;
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
            }
        }
        if (!broke)
            spans.Add(newSpan);
        else
            spans.Insert(Mathf.Clamp(index, 0, spans.Count), newSpan);
    }
}

[RequireComponent(typeof(BoxCollider))]
public class HeightField : MonoBehaviour
{
    [SerializeField] private BoxCollider boxCollider = new BoxCollider();

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
    [SerializeField] private Color walkableColor = Color.green;
    [SerializeField] private Color notWalkableColor = Color.red;

    public int cellCount = 10;
    public int cellCountX = 10;
    public int cellCountZ = 10;
    public int cellCountY = 10;

    private Vector3 minBounds = Vector3.zero;
    private Vector3 maxBounds = Vector3.zero;
    private List<Cell> cells = new List<Cell>();
    private int walkableClimbSpans = 0;
    private int walkableHeightSpans = 0;
    private float inverseCellSize = 1f;
    private float inverseCellHeight = 1f;

    private static readonly float CLIP_EPSILON = 1e-5f;
    private static readonly int[] NeighbourX = { -1, 0, 1, 0 };
    private static readonly int[] NeighbourZ = { 0, 1, 0, -1 };

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
                Gizmos.color = span.area == AreaID.WALKABLE ? walkableColor : notWalkableColor;
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
    public void CreateHeightField(List<Triangle> triangles)
    {
        Bounds colliderBounds = boxCollider.bounds;
        minBounds = colliderBounds.min;
        maxBounds = colliderBounds.max;
        Vector3 size = boxCollider.size;
        inverseCellSize = 1f / cellSize;
        inverseCellHeight = 1f / cellHeight;
        cellCountX = Mathf.FloorToInt(size.x * inverseCellSize);
        cellCountZ = Mathf.FloorToInt(size.z * inverseCellSize);
        cellCount = cellCountX * cellCountZ;
        cellCountY = Mathf.FloorToInt(size.y * inverseCellHeight);
        walkableClimbSpans = Mathf.CeilToInt(walkableClimb * inverseCellHeight);
        walkableHeightSpans = Mathf.CeilToInt(walkableHeight * inverseCellHeight);

        if (cells == null || cells.Count != cellCount)
        {
            cells.Clear();
            cells.Capacity = cellCount;
            for (int i = 0; i < cellCount; ++i)
            {
                cells.Add(new Cell(i % cellCountX, i / cellCountX));
            }
        }
        else
        {
            for (int i = 0; i < cellCount; ++i)
            {
                cells[i].Reset();
            }
        }

        for (int i = 0; i < triangles.Count; ++i)
        {
            RasterizeTriangle(triangles[i].point1, triangles[i].point2, triangles[i].point3, triangles[i].areaID);
        }

        if (filterLowHanging)
            FilterLowHangingObstacles();
        if (filterLedges)
            FilterLedgeSpans();
        if (filterLowHeight)
            FilterLowHeightSpans();
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
        x0 = Mathf.FloorToInt((triMinAABB.x - minBounds.x) * inverseCellSize);
        x1 = Mathf.FloorToInt((triMaxAABB.x - minBounds.x) * inverseCellSize);

        x0 = Math.Clamp(x0, 0, cellCountX - 1);
        x1 = Math.Clamp(x1, 0, cellCountX - 1);

        // Calculate the footprint of the triangle on the grid's z-axis
        z0 = Mathf.FloorToInt((triMinAABB.z - minBounds.z) * inverseCellSize);
        z1 = Mathf.FloorToInt((triMaxAABB.z - minBounds.z) * inverseCellSize);

        z0 = Math.Clamp(z0, 0, cellCountZ - 1);
        z1 = Math.Clamp(z1, 0, cellCountZ - 1);
    }

    private Vector3 ComputeIntersection(Vector3 point1, Vector3 point2, float offset, int axis)
    {
        Vector3 V = point2 - point1;
        float point1Axis = axis == 0 ? point1.x : point1.z;
        float VAxis = axis == 0 ? V.x : V.z;
        float t = (offset - point1Axis) / VAxis;
        return point1 + t * V;
    }

    private int ProcessSegment(Span<float> deltas, float offset, int axis, int pointIndex, Vector3 point1, Vector3 point2, Span<Vector3> insidePoints, Span<Vector3> outPoints, out int outPointsCount)
    {
        int pointsCount = 0;
        outPointsCount = 0;

        float D1 = deltas[pointIndex];
        float D2 = deltas[pointIndex + 1 == deltas.Length ? 0 : pointIndex + 1];

        if (D1 > -CLIP_EPSILON)
        {
            insidePoints[0] = point1;
            if (D2 > -CLIP_EPSILON)
            {
                pointsCount = 1;
            }
            else
            {
                Vector3 interSectionPoint = ComputeIntersection(point1, point2, offset, axis);
                insidePoints[1] = interSectionPoint;
                pointsCount = 2;

                outPoints[0] = interSectionPoint;
                outPointsCount = 1;
            }

        }
        else if (D2 > -CLIP_EPSILON)
        {
            Vector3 interSectionPoint = ComputeIntersection(point1, point2, offset, axis);
            insidePoints[0] = interSectionPoint;
            pointsCount = 1;

            outPoints[0] = point1;
            outPoints[1] = interSectionPoint;
            outPointsCount = 2;
        }
        else
        {
            outPoints[0] = point1;
            outPointsCount = 1;
        }

        return pointsCount;
    }

    private int ClipPoly(int axis, float offset, Span<Vector3> segmentPoints, Span<Vector3> outSegmentPoints, Span<Vector3> inputPolygon, int verticesCount, Span<Vector3> inOutputPolygon, Span<Vector3> outOutputPolygon, out int outVerticesCount)
    {
        int pointsCount = 0;
        outVerticesCount = 0;
        Span<float> verticesAxisDelta = stackalloc float[verticesCount];
        for (int i = 0; i < verticesCount; ++i)
        {
            float coord = axis == 0 ? inputPolygon[i].x : inputPolygon[i].z;
            verticesAxisDelta[i] = offset - coord;
        }

        for (int i = 0; i < verticesCount; ++i)
        {
            int outCount = 0;
            int count = ProcessSegment(verticesAxisDelta, offset, axis, i, inputPolygon[i], inputPolygon[i + 1 == verticesCount ? 0 : i + 1], segmentPoints, outSegmentPoints, out outCount);
            if (count >= 1)
                inOutputPolygon[pointsCount++] = segmentPoints[0];
            if (count == 2)
                inOutputPolygon[pointsCount++] = segmentPoints[1];

            if (outCount >= 1)
                outOutputPolygon[outVerticesCount++] = outSegmentPoints[0];
            if (outCount == 2)
                outOutputPolygon[outVerticesCount++] = outSegmentPoints[1];
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
        Span<Vector3> rowBufferA = stackalloc Vector3[12];
        Span<Vector3> rowBufferB = stackalloc Vector3[12];
        Span<Vector3> rowRemainderBuffer = stackalloc Vector3[12];
        Span<Vector3> rowInput = rowBufferA;
        Span<Vector3> rowRemainder = rowRemainderBuffer;
        Span<Vector3> columnRemainderBuffer = stackalloc Vector3[12];

        Span<Vector3> segmentPoints = stackalloc Vector3[2];
        Span<Vector3> outSegmentPoints = stackalloc Vector3[2];
        Vector3 cellPos = Vector3.zero;

        rowBufferA[0] = point1;
        rowBufferA[1] = point2;
        rowBufferA[2] = point3;
        int rowCount = 3;

        for (int z = z0; z <= z1; ++z)
        {
            cellPos.z = cellSize * z + minBounds.z;

            // Clip to row
            int outRowCount = 0;
            int currentRowCount = ClipPoly(2, cellPos.z + cellSize, segmentPoints, outSegmentPoints, rowInput, rowCount, rowBufferB, rowRemainder, out outRowCount);

            Span<Vector3> temp = rowInput;
            rowInput = rowRemainder;
            rowRemainder = temp;
            rowCount = outRowCount;

            if (currentRowCount < 3)
                continue;

            // Find the row's actual X extent
            float minX = rowBufferB[0].x;
            float maxX = rowBufferB[0].x;
            for (int i = 1; i < currentRowCount; ++i)
            {
                if (rowBufferB[i].x < minX)
                    minX = rowBufferB[i].x;
                if (rowBufferB[i].x > maxX)
                    maxX = rowBufferB[i].x;
            }

            int rowX0 = Mathf.FloorToInt((minX - minBounds.x) * inverseCellSize);
            int rowX1 = Mathf.FloorToInt((maxX - minBounds.x) * inverseCellSize);
            rowX0 = Math.Clamp(rowX0, x0, x1);
            rowX1 = Math.Clamp(rowX1, x0, x1);

            Span<Vector3> columnInput = rowBufferB;
            Span<Vector3> columnRemainder = columnRemainderBuffer;
            int columnCount = currentRowCount;
            for (int x = rowX0; x <= rowX1; ++x)
            {
                cellPos.x = cellSize * x + minBounds.x;

                // Clip to column
                int outColumnCount = 0;
                int cellCount = ClipPoly(0, cellPos.x + cellSize, segmentPoints, outSegmentPoints, columnInput, columnCount, polygon, columnRemainder, out outColumnCount);

                Span<Vector3> colTemp = columnInput;
                columnInput = columnRemainder;
                columnRemainder = colTemp;
                columnCount = outColumnCount;

                if (cellCount < 3)
                    continue;

                // Add spans
                float minHeight = Mathf.Infinity;
                float maxHeight = -Mathf.Infinity;

                for (int i = 0; i < cellCount; ++i)
                {
                    if (polygon[i].y < minHeight)
                        minHeight = polygon[i].y;
                    if (polygon[i].y > maxHeight)
                        maxHeight = polygon[i].y;
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
        for (int i = 0; i < cellCount; ++i)
        {
            Span previousSpan = new Span();
            bool previousWalkable = false;
            AreaID previousAreadID = AreaID.NULL;
            Cell currentCell = cells[i];

            for (int j = 0; j < currentCell.spans.Count; ++j)
            {
                Span currentSpan = currentCell.spans[j];
                bool walkable = currentSpan.area != AreaID.NULL;
                if (!walkable && previousWalkable && currentSpan.max - previousSpan.max <= walkableClimbSpans)
                {
                    currentSpan.area = previousAreadID;
                    currentCell.spans[j] = currentSpan;
                }

                previousWalkable = walkable;
                previousAreadID = currentSpan.area;
                previousSpan = currentSpan;
            }
        }
    }

    private void FilterLedgeSpans()
    {
        for (int z = 0; z < cellCountZ; ++z)
        {
            for (int x = 0; x < cellCountX; ++x)
            {
                Cell currentCell = cells[x + z * cellCountX];
                for (int j = 0; j < currentCell.spans.Count; ++j)
                {
                    Span span = currentCell.spans[j];
                    if (span.area == AreaID.NULL)
                        continue;

                    int floor = span.max;
                    int ceiling = j + 1 < currentCell.spans.Count ? currentCell.spans[j + 1].min : int.MaxValue;

                    int lowestNeighbourFloorDifference = int.MaxValue;

                    int lowestTraversableNeighbourFloor = span.max;
                    int highestTraversableNeighbourFloor = span.max;

                    for (int direction = 0; direction < 4; ++direction)
                    {
                        int neighbourX = x + NeighbourX[direction];
                        int neighbourZ = z + NeighbourZ[direction];

                        if (neighbourX < 0 || neighbourZ < 0 || neighbourX >= cellCountX || neighbourZ >= cellCountZ)
                        {
                            lowestNeighbourFloorDifference = -walkableClimbSpans - 1;
                            break;
                        }

                        Cell neighbourCell = cells[neighbourX + neighbourZ * cellCountX];
                        Span? neighbourSpan = neighbourCell.spans.Count > 0 ? neighbourCell.spans[0] : null;
                        int neighbourCeiling = neighbourSpan != null ? neighbourSpan.Value.min : int.MaxValue;

                        if (Math.Min(ceiling, neighbourCeiling) - floor >= walkableHeightSpans)
                        {
                            lowestNeighbourFloorDifference = -walkableClimbSpans - 1;
                            break;
                        }

                        for (int k = 0; k < neighbourCell.spans.Count; ++k)
                        {
                            neighbourSpan = neighbourCell.spans[k];
                            int neighbourFloor = neighbourSpan.Value.max;
                            neighbourCeiling = k + 1 < neighbourCell.spans.Count ? neighbourCell.spans[k + 1].min : int.MaxValue;

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
                            else if (neighbourFloorDifference < -walkableClimbSpans)
                            {
                                break;
                            }
                        }
                    }

                    if (lowestNeighbourFloorDifference < -walkableClimbSpans)
                    {
                        span.area = AreaID.NULL;
                        currentCell.spans[j] = span;
                    }
                    else if (highestTraversableNeighbourFloor - lowestTraversableNeighbourFloor > walkableClimbSpans)
                    {
                        span.area = AreaID.NULL;
                        currentCell.spans[j] = span;
                    }
                }
            }
        }
    }

    private void FilterLowHeightSpans()
    {
        for (int i = 0; i < cellCount; ++i)
        {
            List<Span> spans = cells[i].spans;
            for (int j = 0; j < spans.Count; ++j)
            {
                int floor = spans[j].max;
                int ceiling = j + 1 < spans.Count ? spans[j + 1].min : int.MaxValue;

                if (ceiling - floor < walkableHeightSpans)
                {
                    Span span = spans[j];
                    span.area = AreaID.NULL;
                    spans[j] = span;
                }
            }
        }
    }
    #endregion
}
