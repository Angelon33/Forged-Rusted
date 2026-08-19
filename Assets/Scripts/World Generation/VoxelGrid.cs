using UnityEngine;

public class VoxelGrid : MonoBehaviour
{
    [Header("Grid Bounds")]
    public int width = 40;
    public int depth = 40;
    public int height = 20;

    [Header("Generation Settings")]
    public int initialLayers = 1;
    public float blockScale = 0.5f;

    [Header("Viewport Gizmos")]
    public bool showGizmos = true;
    public Color gridColor = Color.green;

    private GameObject[,,] blocks;

    void Start()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        blocks = new GameObject[width, height, depth];

        int spawnHeight = Mathf.Min(initialLayers, height);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < spawnHeight; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    CreateBlockAt(x, y, z);
                }
            }
        }
    }

    private void CreateBlockAt(int x, int y, int z)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.transform.position = new Vector3(x, y, z) * blockScale;
        block.transform.localScale = Vector3.one * blockScale;
        block.transform.parent = transform;
        block.name = $"Block_{x}_{y}_{z}";

        // Attach BlockData script
        BlockData data = block.AddComponent<BlockData>();
        data.blockType = "GrassBlock";

        blocks[x, y, z] = block;
    }

    public void PlaceBlockAt(Vector3Int gridPos)
    {
        if (IsInsideGrid(gridPos))
        {
            if (blocks[gridPos.x, gridPos.y, gridPos.z] == null)
            {
                CreateBlockAt(gridPos.x, gridPos.y, gridPos.z);
            }
        }
    }

    public void DestroyBlockAt(Vector3Int gridPos)
    {
        if (IsInsideGrid(gridPos))
        {
            if (blocks[gridPos.x, gridPos.y, gridPos.z] != null)
            {
                Destroy(blocks[gridPos.x, gridPos.y, gridPos.z]);
                blocks[gridPos.x, gridPos.y, gridPos.z] = null;
            }
        }
    }

    public bool IsInsideGrid(Vector3Int pos)
    {
        return pos.x >= 0 && pos.x < width &&
               pos.y >= 0 && pos.y < height &&
               pos.z >= 0 && pos.z < depth;
    }

    // Renders wireframe outline ONLY inside Scene View (Viewport)
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = gridColor;

        Vector3 size = new Vector3(width * blockScale, height * blockScale, depth * blockScale);
        Vector3 center = transform.position + (size * 0.5f) - (Vector3.one * blockScale * 0.5f);

        Gizmos.DrawWireCube(center, size);
    }
}