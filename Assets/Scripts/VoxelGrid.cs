using UnityEngine;

public class VoxelGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 20;         // Grid width (X axis)
    public int depth = 20;         // Grid depth (Z axis)
    public int height = 1;         // Height of the initial floor layer
    public float blockScale = 1f;

    [Header("Gizmos / Visual Grid")]
    public bool showGizmos = true;
    public Color gridColor = Color.green;

    // 3D array storing references to created block GameObjects
    private GameObject[,,] blocks;

    void Start()
    {
        GenerateFloor();
    }

    // Generates only the base floor grid (1 layer high by default)
    void GenerateFloor()
    {
        // Allocate space for height = 1 (or more if set in inspector)
        blocks = new GameObject[width, height, depth];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    // Create a basic Unity cube primitive
                    GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);

                    // Position the block in world space
                    block.transform.position = new Vector3(x, y, z) * blockScale;
                    block.transform.parent = transform; // Keep Hierarchy clean
                    block.name = $"Block_{x}_{y}_{z}";

                    // Store reference in array
                    blocks[x, y, z] = block;
                }
            }
        }
    }

    // Destroys a block at specific grid coordinates
    public void DestroyBlockAt(Vector3Int gridPos)
    {
        if (IsInsideGrid(gridPos))
        {
            if (blocks[gridPos.x, gridPos.y, gridPos.z] != null)
            {
                Destroy(blocks[gridPos.x, gridPos.y, gridPos.z]);
                blocks[gridPos.x, gridPos.y, gridPos.z] = null;
                Debug.Log($"Block destroyed at position: {gridPos}");
            }
        }
    }

    // Checks if given coordinates are within grid range
    public bool IsInsideGrid(Vector3Int pos)
    {
        return pos.x >= 0 && pos.x < width &&
               pos.y >= 0 && pos.y < height &&
               pos.z >= 0 && pos.z < depth;
    }

    // Draws visual grid lines in the Unity Scene editor window
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = gridColor;

        // Draw boundary box for the floor grid
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                Vector3 center = new Vector3(x, 0, z) * blockScale;
                Gizmos.DrawWireCube(center, Vector3.one * blockScale);
            }
        }
    }
}