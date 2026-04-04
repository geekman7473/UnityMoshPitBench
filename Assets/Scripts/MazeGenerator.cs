using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates a procedural maze using the recursive backtracker algorithm.
/// Builds physical walls from cube primitives, a floor, and places a reward at the center.
/// Seeded RNG for deterministic layout.
/// </summary>
public class MazeGenerator : MonoBehaviour
{
    [Header("Maze Dimensions")]
    public int mazeWidth = 25;
    public int mazeHeight = 25;
    public float cellSize = 3f;
    public float wallHeight = 3f;
    public float wallThickness = 0.3f;

    [Header("Seed")]
    public int seed = 12345;

    [Header("Appearance")]
    public Color wallColor = new Color(0.72f, 0.60f, 0.35f, 1f);   // straw/corn color
    public Color floorColor = new Color(0.35f, 0.28f, 0.15f, 1f);  // dirt brown
    public Color rewardColor = new Color(1f, 0.85f, 0.1f, 1f);     // golden

    // Cell wall bitmask flags
    const int NORTH = 1, EAST = 2, SOUTH = 4, WEST = 8;

    int[,] _cells;
    bool[,] _visited;
    Material _wallMat;
    Material _floorMat;

    /// <summary>World-space position of the center cell.</summary>
    public Vector3 CenterWorldPos => new Vector3(
        (mazeWidth / 2) * cellSize + cellSize / 2f,
        0f,
        (mazeHeight / 2) * cellSize + cellSize / 2f
    );

    /// <summary>Total world-space size of the maze.</summary>
    public Vector3 MazeWorldSize => new Vector3(mazeWidth * cellSize, wallHeight, mazeHeight * cellSize);

    public void Generate()
    {
        _wallMat = CreateMaterial(wallColor, 0f, 0.3f);
        _floorMat = CreateMaterial(floorColor, 0f, 0.2f);

        GenerateMazeData();
        BuildPhysicalWalls();
        CreateFloor();
        CreateReward();
    }

    /// <summary>Returns a random valid cell world position for spawning agents.</summary>
    public Vector3 GetRandomCellPosition(System.Random rng)
    {
        int x = rng.Next(mazeWidth);
        int y = rng.Next(mazeHeight);
        return new Vector3(
            x * cellSize + cellSize / 2f,
            0f,
            y * cellSize + cellSize / 2f
        );
    }

    // ── Maze generation (recursive backtracker) ────────────────────

    void GenerateMazeData()
    {
        System.Random rng = new System.Random(seed);
        _cells = new int[mazeWidth, mazeHeight];
        _visited = new bool[mazeWidth, mazeHeight];

        // All cells start with all 4 walls
        for (int x = 0; x < mazeWidth; x++)
            for (int y = 0; y < mazeHeight; y++)
                _cells[x, y] = NORTH | EAST | SOUTH | WEST;

        // Iterative DFS with explicit stack
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        _visited[0, 0] = true;
        stack.Push(new Vector2Int(0, 0));

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> neighbors = GetUnvisitedNeighbors(current);

            if (neighbors.Count > 0)
            {
                Vector2Int next = neighbors[rng.Next(neighbors.Count)];
                RemoveWallBetween(current, next);
                _visited[next.x, next.y] = true;
                stack.Push(next);
            }
            else
            {
                stack.Pop();
            }
        }
    }

    List<Vector2Int> GetUnvisitedNeighbors(Vector2Int cell)
    {
        List<Vector2Int> n = new List<Vector2Int>(4);
        if (cell.y < mazeHeight - 1 && !_visited[cell.x, cell.y + 1])
            n.Add(new Vector2Int(cell.x, cell.y + 1));
        if (cell.y > 0 && !_visited[cell.x, cell.y - 1])
            n.Add(new Vector2Int(cell.x, cell.y - 1));
        if (cell.x < mazeWidth - 1 && !_visited[cell.x + 1, cell.y])
            n.Add(new Vector2Int(cell.x + 1, cell.y));
        if (cell.x > 0 && !_visited[cell.x - 1, cell.y])
            n.Add(new Vector2Int(cell.x - 1, cell.y));
        return n;
    }

    void RemoveWallBetween(Vector2Int a, Vector2Int b)
    {
        if (b.y == a.y + 1) { _cells[a.x, a.y] &= ~NORTH; _cells[b.x, b.y] &= ~SOUTH; }
        else if (b.y == a.y - 1) { _cells[a.x, a.y] &= ~SOUTH; _cells[b.x, b.y] &= ~NORTH; }
        else if (b.x == a.x + 1) { _cells[a.x, a.y] &= ~EAST; _cells[b.x, b.y] &= ~WEST; }
        else if (b.x == a.x - 1) { _cells[a.x, a.y] &= ~WEST; _cells[b.x, b.y] &= ~EAST; }
    }

    // ── Physical wall construction ─────────────────────────────────

    void BuildPhysicalWalls()
    {
        // Horizontal walls (along X axis, at Z boundaries)
        // y ranges from 0 to mazeHeight: each y is the boundary between row y-1 and row y
        for (int y = 0; y <= mazeHeight; y++)
        {
            for (int x = 0; x < mazeWidth; x++)
            {
                bool wallExists;
                if (y == 0)
                    wallExists = true; // south boundary
                else if (y == mazeHeight)
                    wallExists = true; // north boundary
                else
                    wallExists = (_cells[x, y - 1] & NORTH) != 0;

                if (wallExists)
                {
                    Vector3 pos = new Vector3(
                        x * cellSize + cellSize / 2f,
                        wallHeight / 2f,
                        y * cellSize
                    );
                    CreateWall(pos, new Vector3(cellSize + wallThickness, wallHeight, wallThickness));
                }
            }
        }

        // Vertical walls (along Z axis, at X boundaries)
        for (int x = 0; x <= mazeWidth; x++)
        {
            for (int y = 0; y < mazeHeight; y++)
            {
                bool wallExists;
                if (x == 0)
                    wallExists = true; // west boundary
                else if (x == mazeWidth)
                    wallExists = true; // east boundary
                else
                    wallExists = (_cells[x - 1, y] & EAST) != 0;

                if (wallExists)
                {
                    Vector3 pos = new Vector3(
                        x * cellSize,
                        wallHeight / 2f,
                        y * cellSize + cellSize / 2f
                    );
                    CreateWall(pos, new Vector3(wallThickness, wallHeight, cellSize + wallThickness));
                }
            }
        }
    }

    void CreateWall(Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = position;
        wall.transform.localScale = scale;
        wall.isStatic = true;

        Renderer rend = wall.GetComponent<Renderer>();
        rend.sharedMaterial = _wallMat;
    }

    // ── Floor ──────────────────────────────────────────────────────

    void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "MazeFloor";
        floor.transform.SetParent(transform, false);
        floor.transform.localPosition = new Vector3(
            mazeWidth * cellSize / 2f,
            -0.05f,
            mazeHeight * cellSize / 2f
        );
        floor.transform.localScale = new Vector3(
            mazeWidth * cellSize + 2f,
            0.1f,
            mazeHeight * cellSize + 2f
        );
        floor.isStatic = true;

        Renderer rend = floor.GetComponent<Renderer>();
        rend.sharedMaterial = _floorMat;
    }

    // ── Reward at center ───────────────────────────────────────────

    void CreateReward()
    {
        Vector3 center = CenterWorldPos;

        // Golden glowing sphere
        GameObject reward = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        reward.name = "Reward";
        reward.transform.SetParent(transform, false);
        reward.transform.localPosition = center + Vector3.up * 1.5f;
        reward.transform.localScale = Vector3.one * 1.2f;

        // Remove collider so it doesn't block NavMesh
        Object.Destroy(reward.GetComponent<Collider>());

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.name = "RewardMaterial";
        mat.color = rewardColor;
        mat.SetFloat("_Metallic", 0.8f);
        mat.SetFloat("_Smoothness", 0.9f);
        // Emissive glow
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", rewardColor * 2f);
        reward.GetComponent<Renderer>().material = mat;

        // Tall beacon light column
        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "Beacon";
        beacon.transform.SetParent(transform, false);
        beacon.transform.localPosition = center + Vector3.up * 8f;
        beacon.transform.localScale = new Vector3(0.15f, 8f, 0.15f);
        Object.Destroy(beacon.GetComponent<Collider>());

        Material beaconMat = new Material(shader);
        beaconMat.name = "BeaconMaterial";
        beaconMat.color = new Color(1f, 0.9f, 0.3f, 0.5f);
        beaconMat.SetFloat("_Surface", 1);
        beaconMat.SetFloat("_Blend", 0);
        beaconMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        beaconMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        beaconMat.SetFloat("_ZWrite", 0);
        beaconMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        beaconMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        beaconMat.EnableKeyword("_EMISSION");
        beaconMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.3f) * 3f);
        beacon.GetComponent<Renderer>().material = beaconMat;
    }

    // ── Helpers ────────────────────────────────────────────────────

    Material CreateMaterial(Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }
}
