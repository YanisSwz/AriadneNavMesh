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

    public int GetHeightfieldSpanCount(HeightField heightField)
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
}
