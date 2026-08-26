using System.Collections.Generic;
using System;
using UnityEngine;

public struct CompactSpan
{
    public CompactSpan(int y, int height, int[] neighbours = null)
    {
        this.y = y;
        this.height = height;
        if(neighbours != null)
            this.neighbours = neighbours;
        else
            this.neighbours = new int[4] { -1, -1, -1, -1 };
    }

    public int y;
    public int height;
    public int[] neighbours;
}

public struct CompactCell
{
    public CompactCell(int index = -1, int count = 0)
    {
        this.index = index;
        this.count = count;
    }

    public int index;
    public int count;
}

public class CompactHeightField
{
    public List<CompactCell> Cells { get { return cells; } } 
    public List<CompactSpan> Spans { get { return spans; } }
    public List<AreaID> Areas { get { return areas; } }

    private float cellSize = 0.5f;
    private float cellHeight = 0.5f;
    private int spanCount = 0;
    private int cellCount = 10;
    private int cellCountX = 10;
    private int cellCountZ = 10;
    private int cellCountY = 10;
    private int walkableClimb = 0;
    private int walkableHeight = 0;
    private Vector3 minBounds = Vector3.zero;
    private Vector3 maxBounds = Vector3.zero;
    private Bounds bounds = new Bounds();
    private List<CompactCell> cells = new List<CompactCell>();
    private List<CompactSpan> spans = new List<CompactSpan>();
    private List<AreaID> areas = new List<AreaID>();

    private void Allocate(int cellCount, int spanCount)
    {
        AllocateCells(cellCount);
        AllocateSpans(spanCount);
    }

    private void AllocateCells(int cellCount)
    {
        cells.Clear();
        cells.Capacity = cellCount;
        for (int i = 0; i < cellCount; ++i)
        {
            cells.Add(new CompactCell());
        }
    }

    private void AllocateSpans(int spanCount)
    {
        spans.Clear();
        areas.Clear();
        spans.Capacity = spanCount;
        areas.Capacity = spanCount;
        for (int i = 0; i < spanCount; ++i)
        {
            spans.Add(new CompactSpan(0, 0));
            areas.Add(AreaID.NULL);
        }
    }

    public void BuildCompactHeightField(HeightField heightField)
    {
        cellSize = heightField.CellSize;
        cellHeight = heightField.CellHeight;
        cellCount = heightField.CellCount;
        cellCountX = heightField.CellCountX;
        cellCountY = heightField.CellCountY;
        cellCountZ = heightField.CellCountZ;
        walkableClimb = heightField.WalkableClimb;
        walkableHeight = heightField.WalkableHeight;
        bounds = heightField.Bounds;
        minBounds = bounds.min;
        maxBounds = bounds.max;
        maxBounds[1] += walkableHeight * cellHeight;
        spanCount = GetHeightfieldSpanCount(heightField);

        Allocate(cellCount, spanCount);

        int currentCellIndex = 0;
        for (int i = 0; i < cellCount; ++i)
        {
            Cell currentCell = heightField.Cells[i];
            if (currentCell.spans.Count == 0)
                continue;

            CompactCell cell = cells[i];
            cell.index = currentCellIndex;
            cell.count = 0;

            for (int j = 0; j < currentCell.spans.Count; ++j)
            {
                if (currentCell.spans[j].area != AreaID.NULL)
                {
                    int bottom = currentCell.spans[j].max;
                    int top = j + 1 < currentCell.spans.Count ? currentCell.spans[j + 1].min : AriadneConstants.MAX_HEIGHT;
                    CompactSpan span = spans[currentCellIndex];
                    span.y = bottom;
                    span.height = top - bottom;
                    spans[currentCellIndex] = span;
                    areas[currentCellIndex] = currentCell.spans[j].area;
                    ++currentCellIndex;
                    ++cell.count;
                }
            }

            cells[i] = cell;
        }

        for(int z = 0; z < cellCountZ; ++z) 
        {
            for (int x = 0; x < cellCountX; ++x) 
            {
                CompactCell currentCell = cells[x + z * cellCountX];
                for(int i = currentCell.index; i < currentCell.index + currentCell.count; ++i) 
                {
                    CompactSpan currentSpan = spans[i];

                    for(int dir = 0; dir < 4; ++dir) 
                    {
                        int neighourX = x + AriadneConstants.NeighbourX[dir];
                        int neighbourZ = z + AriadneConstants.NeighbourZ[dir];
                        if(neighourX < 0 || neighbourZ < 0 || neighourX >= cellCountX || neighbourZ >= cellCountZ) 
                        {
                            continue;
                        }

                        CompactCell neighbourCell = cells[neighourX + neighbourZ * cellCountX];
                        for(int j = neighbourCell.index; j < neighbourCell.index + neighbourCell.count; ++j) 
                        {
                            CompactSpan neighbourSpan = spans[j];
                            int bottom = Math.Max(currentSpan.y, neighbourSpan.y);
                            int top = Math.Min(currentSpan.y + currentSpan.height, neighbourSpan.y + neighbourSpan.height);
                        
                            if((top - bottom) >= walkableHeight && Math.Abs(currentSpan.y - neighbourSpan.y) <= walkableClimb) 
                            {
                                currentSpan.neighbours[dir] = j;
                                spans[i] = currentSpan;
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    private int GetHeightfieldSpanCount(HeightField heightField)
    {
        int spanCount = 0;
        for (int i = 0; i < heightField.CellCount; ++i)
        {
            for (int j = 0; j < heightField.Cells[i].spans.Count; ++j)
            {
                if (heightField.Cells[i].spans[j].area != AreaID.NULL)
                    ++spanCount;
            }
        }

        return spanCount;
    }

    public void ErodeWalkableArea(int radius)
    {
        int[] distanceToBoundary = new int[spanCount];
        Array.Fill(distanceToBoundary, 1 << 20);

        // Mark the boundary cells
        for (int z = 0; z < cellCountZ; ++z)
        {
            for (int x = 0; x < cellCountX; ++x)
            {
                CompactCell cell = cells[x + z * cellCountX];
                int maxSpanIndex = cell.index + cell.count;
                for (int i = cell.index; i < maxSpanIndex; ++i)
                {
                    // Check for spans that have been marked unwalkable by manually-authored areas
                    if (areas[i] == AreaID.NULL)
                    {
                        distanceToBoundary[i] = 0;
                        continue;
                    }

                    CompactSpan span = spans[i];

                    int neighbourCount = 0;
                    for (int dir = 0; dir < 4; ++dir)
                    {
                        if (span.neighbours[dir] == -1)
                        {
                            break;
                        }

                        if (areas[span.neighbours[dir]] == AreaID.NULL)
                        {
                            break;
                        }

                        ++neighbourCount;
                    }

                    // If not surrounded by neighbours, this is a boundary cell
                    if (neighbourCount != 4)
                    {
                        distanceToBoundary[i] = 0;
                    }
                }
            }
        }

        int newDistance;

        // South-West neighbours pass
        for (int z = 0; z < cellCountZ; ++z)
        {
            for (int x = 0; x < cellCountX; ++x)
            {
                CompactCell cell = cells[x + z * cellCountX];
                int maxSpanIndex = cell.index + cell.count;
                for (int i = cell.index; i < maxSpanIndex; ++i)
                {
                    CompactSpan span = spans[i];

                    if (span.neighbours[0] != -1)
                    {
                        // (-1, 0)
                        int aIndex = span.neighbours[0];
                        CompactSpan neighbourSpan = spans[aIndex];
                        newDistance = Math.Min(distanceToBoundary[aIndex] + 2, 1 << 20);
                        if (newDistance < distanceToBoundary[i])
                        {
                            distanceToBoundary[i] = newDistance;
                        }

                        // (-1, -1)
                        if (neighbourSpan.neighbours[3] != -1)
                        {
                            int bIndex = neighbourSpan.neighbours[3];
                            newDistance = Math.Min(distanceToBoundary[bIndex] + 3, 1 << 20);
                            if (newDistance < distanceToBoundary[i])
                            {
                                distanceToBoundary[i] = newDistance;
                            }
                        }
                    }

                    if (span.neighbours[3] != -1)
                    {
                        // (0, -1)
                        int aIndex = span.neighbours[3];
                        CompactSpan neighbourSpan = spans[aIndex];
                        newDistance = Math.Min(distanceToBoundary[aIndex] + 2, 1 << 20);
                        if (newDistance < distanceToBoundary[i])
                        {
                            distanceToBoundary[i] = newDistance;
                        }

                        // (1, -1)
                        if (neighbourSpan.neighbours[2] != -1)
                        {
                            int bIndex = neighbourSpan.neighbours[2];
                            newDistance = Math.Min(distanceToBoundary[bIndex] + 3, 1 << 20);
                            if (newDistance < distanceToBoundary[i])
                            {
                                distanceToBoundary[i] = newDistance;
                            }
                        }
                    }
                }
            }
        }

        // North-East neighbours pass
        for (int z = cellCountZ - 1; z >= 0; --z)
        {
            for (int x = cellCountX - 1; x >= 0 ; --x)
            {
                CompactCell cell = cells[x + z * cellCountX];
                int maxSpanIndex = cell.index + cell.count;
                for (int i = cell.index; i < maxSpanIndex; ++i)
                {
                    CompactSpan span = spans[i];

                    if (span.neighbours[2] != -1)
                    {
                        // (1, 0)
                        int aIndex = span.neighbours[2];
                        CompactSpan neighbourSpan = spans[aIndex];
                        newDistance = Math.Min(distanceToBoundary[aIndex] + 2, 1 << 20);
                        if (newDistance < distanceToBoundary[i])
                        {
                            distanceToBoundary[i] = newDistance;
                        }

                        // (1, 1)
                        if (neighbourSpan.neighbours[1] != -1)
                        {
                            int bIndex = neighbourSpan.neighbours[1];
                            newDistance = Math.Min(distanceToBoundary[bIndex] + 3, 1 << 20);
                            if (newDistance < distanceToBoundary[i])
                            {
                                distanceToBoundary[i] = newDistance;
                            }
                        }
                    }

                    if (span.neighbours[1] != -1)
                    {
                        // (0, 1)
                        int aIndex = span.neighbours[1];
                        CompactSpan neighbourSpan = spans[aIndex];
                        newDistance = Math.Min(distanceToBoundary[aIndex] + 2, 1 << 20);
                        if (newDistance < distanceToBoundary[i])
                        {
                            distanceToBoundary[i] = newDistance;
                        }

                        // (-1, 1)
                        if (neighbourSpan.neighbours[0] != -1)
                        {
                            int bIndex = neighbourSpan.neighbours[0];
                            newDistance = Math.Min(distanceToBoundary[bIndex] + 3, 1 << 20);
                            if (newDistance < distanceToBoundary[i])
                            {
                                distanceToBoundary[i] = newDistance;
                            }
                        }
                    }
                }
            }
        }

        int minBoundaryDistance = radius * 2;
        for(int i = 0; i < spanCount; ++i) 
        {
            if (distanceToBoundary[i] < minBoundaryDistance)
            {
                areas[i] = AreaID.NULL;
            }
        }
    }
}
