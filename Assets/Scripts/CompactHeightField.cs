using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

public struct CompactSpan
{
    public CompactSpan(int y, int height, int regionID = 0, int[] neighbours = null)
    {
        this.y = y;
        this.height = height;
        this.regionID = regionID;
        if (neighbours != null)
            this.neighbours = neighbours;
        else
            this.neighbours = new int[4] { -1, -1, -1, -1 };
    }

    public int y;
    public int height;
    public int regionID;
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

public struct Region
{
    public Region(int ID = 0, int spanCount = 0, AreaID area = AreaID.NULL, Dictionary<int, int> connections = null)
    {
        this.ID = ID;
        this.spanCount = spanCount;
        this.area = area;
        if (connections != null)
            this.connections = connections;
        else
            this.connections = new Dictionary<int, int>();
    }

    public int ID;
    public int spanCount;
    public AreaID area;
    public Dictionary<int, int> connections;
}

public class CompactHeightField
{
    public List<CompactCell> Cells { get { return cells; } }
    public List<CompactSpan> Spans { get { return spans; } }
    public List<AreaID> Areas { get { return areas; } }
    public List<int> Distances { get { return distances; } }
    public int MaxDistance { get { return maxDistance; } }
    public int RegionCount { get { return regionCount; } }

    private float cellSize = 0.5f;
    private float cellHeight = 0.5f;
    private int spanCount = 0;
    private int cellCount = 10;
    private int cellCountX = 10;
    private int cellCountZ = 10;
    private int cellCountY = 10;
    private int walkableClimb = 0;
    private int walkableHeight = 0;
    private int maxDistance = 0;
    private int regionCount = 0;
    private Vector3 minBounds = Vector3.zero;
    private Vector3 maxBounds = Vector3.zero;
    private Bounds bounds = new Bounds();
    private List<CompactCell> cells = new List<CompactCell>();
    private List<CompactSpan> spans = new List<CompactSpan>();
    private List<AreaID> areas = new List<AreaID>();
    private List<int> distances = new List<int>();
    private Dictionary<int, Region> regions = new Dictionary<int, Region>();

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
        distances.Clear();
        spans.Capacity = spanCount;
        areas.Capacity = spanCount;
        distances.Capacity = spanCount;
        for (int i = 0; i < spanCount; ++i)
        {
            spans.Add(new CompactSpan(0, 0));
            areas.Add(AreaID.NULL);
            distances.Add(1 << 20);
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

        for (int z = 0; z < cellCountZ; ++z)
        {
            for (int x = 0; x < cellCountX; ++x)
            {
                CompactCell currentCell = cells[x + z * cellCountX];
                for (int i = currentCell.index; i < currentCell.index + currentCell.count; ++i)
                {
                    CompactSpan currentSpan = spans[i];

                    for (int dir = 0; dir < 4; ++dir)
                    {
                        int neighourX = x + AriadneConstants.NeighbourX[dir];
                        int neighbourZ = z + AriadneConstants.NeighbourZ[dir];
                        if (neighourX < 0 || neighbourZ < 0 || neighourX >= cellCountX || neighbourZ >= cellCountZ)
                        {
                            continue;
                        }

                        CompactCell neighbourCell = cells[neighourX + neighbourZ * cellCountX];
                        for (int j = neighbourCell.index; j < neighbourCell.index + neighbourCell.count; ++j)
                        {
                            CompactSpan neighbourSpan = spans[j];
                            int bottom = Math.Max(currentSpan.y, neighbourSpan.y);
                            int top = Math.Min(currentSpan.y + currentSpan.height, neighbourSpan.y + neighbourSpan.height);

                            if ((top - bottom) >= walkableHeight && Math.Abs(currentSpan.y - neighbourSpan.y) <= walkableClimb)
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

        // Lower neighbours pass
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

        // Higher neighbours pass
        for (int z = cellCountZ - 1; z >= 0; --z)
        {
            for (int x = cellCountX - 1; x >= 0; --x)
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
        for (int i = 0; i < spanCount; ++i)
        {
            if (distanceToBoundary[i] < minBoundaryDistance)
            {
                areas[i] = AreaID.NULL;
            }
        }
    }

    public void BuildDistanceField()
    {
        // Mark boundary cells
        for (int z = 0; z < cellCountZ; ++z)
        {
            for (int x = 0; x < cellCountX; ++x)
            {
                CompactCell cell = cells[x + z * cellCountX];
                for (int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    CompactSpan span = spans[i];
                    AreaID area = areas[i];

                    int neighboursCount = 0;
                    for (int dir = 0; dir < 4; ++dir)
                    {
                        if (span.neighbours[dir] != -1)
                        {
                            if (area == areas[span.neighbours[dir]])
                                ++neighboursCount;
                        }
                    }
                    if (neighboursCount != 4)
                        distances[i] = 0;
                }
            }
        }

        // Lower neighbours pass
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
                        if (distances[aIndex] + 2 < distances[i])
                        {
                            distances[i] = distances[aIndex] + 2;
                        }

                        // (-1, -1)
                        if (neighbourSpan.neighbours[3] != -1)
                        {
                            int bIndex = neighbourSpan.neighbours[3];
                            if (distances[bIndex] + 3 < distances[i])
                            {
                                distances[i] = distances[bIndex] + 3;
                            }
                        }
                    }

                    if (span.neighbours[3] != -1)
                    {
                        // (0, -1)
                        int aIndex = span.neighbours[3];
                        CompactSpan neighbourSpan = spans[aIndex];
                        if (distances[aIndex] + 2 < distances[i])
                        {
                            distances[i] = distances[aIndex] + 2;
                        }

                        // (1, -1)
                        if (neighbourSpan.neighbours[2] != -1)
                        {
                            int bIndex = neighbourSpan.neighbours[2];
                            if (distances[bIndex] + 3 < distances[i])
                            {
                                distances[i] = distances[bIndex] + 3;
                            }
                        }
                    }
                }
            }
        }

        // Higher neighbours pass
        for (int z = cellCountZ - 1; z >= 0; --z)
        {
            for (int x = cellCountX - 1; x >= 0; --x)
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
                        if (distances[aIndex] + 2 < distances[i])
                        {
                            distances[i] = distances[aIndex] + 2;
                        }

                        // (1, 1)
                        if (neighbourSpan.neighbours[1] != -1)
                        {
                            int bIndex = neighbourSpan.neighbours[1];
                            if (distances[bIndex] + 3 < distances[i])
                            {
                                distances[i] = distances[bIndex] + 3;
                            }
                        }
                    }

                    if (span.neighbours[1] != -1)
                    {
                        // (0, 1)
                        int aIndex = span.neighbours[1];
                        CompactSpan neighbourSpan = spans[aIndex];
                        if (distances[aIndex] + 2 < distances[i])
                        {
                            distances[i] = distances[aIndex] + 2;
                        }

                        // (-1, 1)
                        if (neighbourSpan.neighbours[0] != -1)
                        {
                            int bIndex = neighbourSpan.neighbours[0];
                            if (distances[bIndex] + 3 < distances[i])
                            {
                                distances[i] = distances[bIndex] + 3;
                            }
                        }
                    }
                }
            }
        }

        maxDistance = 0;
        for (int i = 0; i < spanCount; ++i)
            maxDistance = Math.Max(distances[i], maxDistance);
    }

    public void BuildRegions(int minRegionArea)
    {
        regions.Clear();
        List<int> regionIDs = new List<int>();
        regionIDs.Capacity = spanCount;
        for (int i = 0; i < spanCount; ++i)
        {
            regionIDs.Add(0);
        }

        int level = (maxDistance + 1) & ~1;
        int nextRegionID = 1;

        while (level > 0)
        {
            level = Math.Max(level - 2, 0);

            Expand(level, ref regionIDs);
            SeedRegion(level, ref regionIDs, ref nextRegionID);
            Expand(level, ref regionIDs);
        }

        regionCount = nextRegionID;

        FilterAndMergeRegions(regionIDs, minRegionArea);
    }

    private void Expand(int level, ref List<int> regionIDs)
    {
        bool expanded = false;
        do
        {
            expanded = false;
            for (int z = 0; z < cellCountZ; ++z)
            {
                for (int x = 0; x < cellCountX; ++x)
                {
                    CompactCell cell = cells[x + z * cellCountX];
                    for (int i = cell.index; i < cell.index + cell.count; ++i)
                    {
                        if (areas[i] == AreaID.NULL)
                            continue;
                        if (regionIDs[i] != 0)
                            continue;
                        if (distances[i] < level)
                            continue;

                        for (int dir = 0; dir < 4; ++dir)
                        {
                            int neigbourIndex = spans[i].neighbours[dir];
                            if (neigbourIndex == -1)
                                continue;
                            if (areas[neigbourIndex] == AreaID.NULL || areas[neigbourIndex] != areas[i])
                                continue;

                            if (regionIDs[neigbourIndex] != 0)
                            {
                                regionIDs[i] = regionIDs[neigbourIndex];
                                expanded = true;
                                break;
                            }
                        }
                    }
                }
            }
        } while (expanded);
    }

    private void SeedRegion(int level, ref List<int> regionIDs, ref int nextRegionID) 
    {
        for (int z = 0; z < cellCountZ; ++z)
        {
            for (int x = 0; x < cellCountX; ++x)
            {
                CompactCell cell = cells[x + z * cellCountX];
                for (int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (areas[i] == AreaID.NULL)
                        continue;
                    if (regionIDs[i] != 0)
                        continue;
                    if (distances[i] < level)
                        continue;

                    regionIDs[i] = nextRegionID;
                    regions[nextRegionID] = new Region(nextRegionID, 1, areas[i]);
                    Queue<int> neighbours = new Queue<int>();
                    neighbours.Enqueue(i);

                    while (neighbours.Count > 0)
                    {
                        int current = neighbours.Dequeue();
                        for (int dir = 0; dir < 4; ++dir)
                        {
                            int neighourIndex = spans[current].neighbours[dir];
                            if (neighourIndex == -1)
                                continue;
                            if (areas[neighourIndex] == AreaID.NULL || areas[neighourIndex] != areas[current])
                                continue;
                            if (regionIDs[neighourIndex] != 0)
                                continue;
                            if (distances[neighourIndex] < level)
                                continue;

                            regionIDs[neighourIndex] = nextRegionID;
                            neighbours.Enqueue(neighourIndex);
                        }
                    }

                    ++nextRegionID;
                }
            }
        }
    }

    // TODO: rework remove/merge logic and add areaID merging
    private void FilterAndMergeRegions(List<int> regionIDs, int minRegionArea)
    {
        // Build region metadata
        for (int i = 0; i < spanCount; ++i)
        {
            if (regionIDs[i] == 0)
                continue;

            Region region = regions[regionIDs[i]];
            ++region.spanCount;

            CompactSpan span = spans[i];
            for (int dir = 0; dir < 4; ++dir)
            {
                int neighbourIndex = span.neighbours[dir];
                if (neighbourIndex != -1)
                {
                    if (regionIDs[neighbourIndex] != 0 && regionIDs[neighbourIndex] != regionIDs[i])
                    {
                        region.connections[regionIDs[neighbourIndex]] = region.connections.GetValueOrDefault(regionIDs[neighbourIndex]) + 1;
                    }
                }
            }

            regions[regionIDs[i]] = region;
        }

        // Discard or merge tiny regions
        Dictionary<int, Region> regionsCopy = new Dictionary<int, Region>(regions);
        foreach (KeyValuePair<int, Region> entry in regionsCopy)
        {
            Region region = entry.Value;
            if (region.spanCount < minRegionArea)
            {
                if (region.connections.Count == 0)
                {
                    // Reset spans
                    for (int j = 0; j < spanCount; ++j)
                    {
                        if (regionIDs[j] == region.ID)
                            regionIDs[j] = 0;
                    }
                }
                else
                {
                    int bestNeighbourID = region.connections.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
                    Region mergeRegion = regions[bestNeighbourID];

                    // Update spans
                    for (int j = 0; j < spanCount; ++j)
                    {
                        if (regionIDs[j] == region.ID)
                            regionIDs[j] = mergeRegion.ID;
                    }

                    // Update connections
                    foreach (KeyValuePair<int, int> connection in region.connections)
                    {
                        if (connection.Key == mergeRegion.ID)
                            continue;

                        mergeRegion.connections[connection.Key] = mergeRegion.connections.GetValueOrDefault(connection.Key) + connection.Value;
                        regions[connection.Key].connections[mergeRegion.ID] = regions[connection.Key].connections.GetValueOrDefault(mergeRegion.ID) + connection.Value;
                    }
                    foreach (KeyValuePair<int, Region> r in regions)
                    {
                        r.Value.connections.Remove(region.ID);
                    }
                }
                regions.Remove(region.ID);
            }
        }

        // Assign region ID to compact spans
        for (int i = 0; i < spanCount; ++i)
        {
            CompactSpan span = spans[i];
            span.regionID = regionIDs[i];
            spans[i] = span;
        }
    }
}
