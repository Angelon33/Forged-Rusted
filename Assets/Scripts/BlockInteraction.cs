using UnityEngine;

public class BlockInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float reachDistance = 10f; // Maximum distance the player can interact with blocks
    public VoxelGrid voxelGrid;       // Reference to the VoxelGrid script in the scene

    void Update()
    {
        // Detect left mouse click
        if (Input.GetMouseButtonDown(0))
        {
            TryDestroyBlock();
        }
    }

    // Casts a ray from the screen center to find and destroy a targeted block
    private void TryDestroyBlock()
    {
        // Create a ray pointing forward from the middle of the camera view
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, reachDistance))
        {
            // Offset the hit point slightly inward to ensure we get the inside of the hit cube
            Vector3 targetPoint = hit.point - (hit.normal * 0.1f);

            // Convert world space point into integer grid coordinates
            Vector3Int gridPos = new Vector3Int(
                Mathf.FloorToInt(targetPoint.x + 0.5f),
                Mathf.FloorToInt(targetPoint.y + 0.5f),
                Mathf.FloorToInt(targetPoint.z + 0.5f)
            );

            // Send destruction request to the VoxelGrid
            if (voxelGrid != null)
            {
                voxelGrid.DestroyBlockAt(gridPos);
            }
        }
    }
}