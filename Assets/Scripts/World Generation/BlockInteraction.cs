using UnityEngine;
using UnityEngine.InputSystem;

public class BlockInteraction : MonoBehaviour
{
    [Header("Settings")]
    public float reachDistance = 10f;
    public VoxelGrid voxelGrid;
    public Camera playerCamera;

    [Header("Block Info")]
    public string selectedBlockType = "GrassBlock";

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (playerCamera == null) playerCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null || playerCamera == null || voxelGrid == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryModifyBlock(destroy: true);
        }
        else if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryModifyBlock(destroy: false);
        }
    }

    private void TryModifyBlock(bool destroy)
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, reachDistance))
        {
            // Verify if the targeted object is actually part of the voxel grid
            bool isVoxelBlock = hit.collider.GetComponent<BlockData>() != null ||
                                (voxelGrid != null && hit.transform.IsChildOf(voxelGrid.transform));

            if (!isVoxelBlock) return; // Ignore standalone scene objects like Sphere or Box

            float scale = voxelGrid.blockScale;

            if (destroy)
            {
                Vector3 targetPoint = hit.point - (hit.normal * 0.01f);
                Vector3Int gridPos = GetGridPosition(targetPoint, scale);
                voxelGrid.DestroyBlockAt(gridPos);
                Debug.Log($"[BlockInteraction] Destroyed block at grid position: {gridPos}");
            }
            else
            {
                Vector3 targetPoint = hit.point + (hit.normal * 0.01f);
                Vector3Int gridPos = GetGridPosition(targetPoint, scale);
                voxelGrid.PlaceBlockAt(gridPos);
                Debug.Log($"[BlockInteraction] Placed block ({selectedBlockType}) at grid position: {gridPos}");
            }
        }
    }

    private Vector3Int GetGridPosition(Vector3 worldPos, float scale)
    {
        return new Vector3Int(
            Mathf.FloorToInt((worldPos.x / scale) + 0.5f),
            Mathf.FloorToInt((worldPos.y / scale) + 0.5f),
            Mathf.FloorToInt((worldPos.z / scale) + 0.5f)
        );
    }
}