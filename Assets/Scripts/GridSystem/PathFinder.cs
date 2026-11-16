using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Binary-heap A* on the 4-connected cell grid. Buffers are reused between calls (no per-call allocations).
/// The start cell may be non-walkable (a worker standing next to something), the goal must be walkable.
/// </summary>
public static class PathFinder
{
    private static float[] gScore;
    private static int[] parent;
    private static int[] closedStamp;
    private static int[] openStamp;
    private static int stamp;
    private static int gridW, gridH;

    // heap storage
    private static int[] heapCell = new int[256];
    private static float[] heapF = new float[256];
    private static int heapCount;

    private static readonly int[] dx = { 0, -1, 1, 0 };
    private static readonly int[] dy = { -1, 0, 0, 1 };

    private static void EnsureBuffers(int w, int h)
    {
        if (gridW == w && gridH == h && gScore != null) return;
        gridW = w; gridH = h;
        int n = w * h;
        gScore = new float[n];
        parent = new int[n];
        closedStamp = new int[n];
        openStamp = new int[n];
        stamp = 0;
    }

    private static void HeapPush(int cell, float f)
    {
        if (heapCount == heapCell.Length)
        {
            System.Array.Resize(ref heapCell, heapCount * 2);
            System.Array.Resize(ref heapF, heapCount * 2);
        }
        int i = heapCount++;
        while (i > 0)
        {
            int p = (i - 1) >> 1;
            if (heapF[p] <= f) break;
            heapCell[i] = heapCell[p];
            heapF[i] = heapF[p];
            i = p;
        }
        heapCell[i] = cell;
        heapF[i] = f;
    }

    private static int HeapPop()
    {
        int top = heapCell[0];
        heapCount--;
        if (heapCount > 0)
        {
            int cell = heapCell[heapCount];
            float f = heapF[heapCount];
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1;
                if (l >= heapCount) break;
                int r = l + 1;
                int c = (r < heapCount && heapF[r] < heapF[l]) ? r : l;
                if (heapF[c] >= f) break;
                heapCell[i] = heapCell[c];
                heapF[i] = heapF[c];
                i = c;
            }
            heapCell[i] = cell;
            heapF[i] = f;
        }
        return top;
    }

    /// <summary>Fills <paramref name="result"/> with the cells to walk (start excluded, goal included).</summary>
    public static bool FindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> result)
    {
        result.Clear();
        var grid = GridManager.instance;
        if (grid == null) return false;
        if (!grid.InBounds(start) || !grid.InBounds(goal)) return false;
        if (start == goal) return true;
        if (!grid.GetCell(goal).Walkable) return false;

        int w = grid.width, h = grid.height;
        EnsureBuffers(w, h);
        stamp++;
        heapCount = 0;

        int startIdx = start.y * w + start.x;
        int goalIdx = goal.y * w + goal.x;

        gScore[startIdx] = 0f;
        parent[startIdx] = -1;
        openStamp[startIdx] = stamp;
        HeapPush(startIdx, Manhattan(start.x, start.y, goal.x, goal.y));

        while (heapCount > 0)
        {
            int cur = HeapPop();
            if (closedStamp[cur] == stamp) continue; // stale duplicate
            closedStamp[cur] = stamp;

            if (cur == goalIdx)
            {
                Reconstruct(cur, startIdx, w, result);
                return true;
            }

            int cx = cur % w, cy = cur / w;
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dx[d], ny = cy + dy[d];
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int ni = ny * w + nx;
                if (closedStamp[ni] == stamp) continue;

                Cell nc = grid.GetCell(nx, ny);
                if (!nc.Walkable) continue;

                float tentative = gScore[cur] + nc.moveCost;
                if (openStamp[ni] != stamp || tentative < gScore[ni])
                {
                    openStamp[ni] = stamp;
                    gScore[ni] = tentative;
                    parent[ni] = cur;
                    HeapPush(ni, tentative + Manhattan(nx, ny, goal.x, goal.y));
                }
            }
        }
        return false;
    }

    private static float Manhattan(int ax, int ay, int bx, int by)
    {
        return Mathf.Abs(ax - bx) + Mathf.Abs(ay - by);
    }

    private static void Reconstruct(int goalIdx, int startIdx, int w, List<Vector2Int> result)
    {
        int cur = goalIdx;
        while (cur != startIdx && cur >= 0)
        {
            result.Add(new Vector2Int(cur % w, cur / w));
            cur = parent[cur];
        }
        result.Reverse();
    }
}
