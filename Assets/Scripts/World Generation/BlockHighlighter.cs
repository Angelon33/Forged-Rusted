using UnityEngine;
using TMPro;

public class BlockHighlighter : MonoBehaviour
{
    [Header("Settings")]
    public float reachDistance = 10f;
    public Camera playerCamera;
    public VoxelGrid voxelGrid;
    public Color outlineColor = Color.yellow;
    public float lineWidth = 0.02f;

    [Header("UI Reference")]
    public TextMeshProUGUI targetBlockText;

    private LineRenderer lineRenderer;

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        SetupLineRenderer();
    }

    private void SetupLineRenderer()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = outlineColor;
        lineRenderer.endColor = outlineColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 16;
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;
    }

    void Update()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, reachDistance))
        {
            lineRenderer.enabled = true;

            // Check if object belongs to the Voxel Grid
            bool isVoxelBlock = (voxelGrid != null && hit.transform.IsChildOf(voxelGrid.transform))
                                || hit.collider.GetComponent<BlockData>() != null;

            if (isVoxelBlock)
            {
                // 1. VOXEL GRID HIGHLIGHT
                float scale = (voxelGrid != null) ? voxelGrid.blockScale : 0.5f;
                Vector3 targetPoint = hit.point - (hit.normal * 0.01f);
                Vector3Int gridPos = GetGridPosition(targetPoint, scale);
                Vector3 blockWorldPos = new Vector3(gridPos.x, gridPos.y, gridPos.z) * scale;

                DrawCubeOutline(blockWorldPos, scale);

                if (targetBlockText != null)
                {
                    BlockData blockData = hit.collider.GetComponent<BlockData>();
                    string blockName = (blockData != null) ? blockData.blockType : "Block";
                    targetBlockText.text = $"Looking at: {blockName}";
                }
            }
            else
            {
                // 2. STANDALONE OBJECT HIGHLIGHT (Oriented & Padded Bounding Box)
                DrawOrientedBoundsOutline(hit.collider);

                if (targetBlockText != null)
                {
                    string objectName = CleanObjectName(hit.collider.gameObject.name);
                    targetBlockText.text = $"Looking at: {objectName}";
                }
            }
        }
        else
        {
            lineRenderer.enabled = false;

            if (targetBlockText != null)
            {
                targetBlockText.text = "";
            }
        }
    }

    // Draws oriented bounding box that rotates with the object and includes a slight padding margin
    private void DrawOrientedBoundsOutline(Collider col)
    {
        Transform t = col.transform;
        Vector3 center;
        Vector3 size;

        if (col is BoxCollider box)
        {
            center = box.center;
            size = box.size;
        }
        else if (col is SphereCollider sphere)
        {
            center = sphere.center;
            size = Vector3.one * (sphere.radius * 2f);
        }
        else
        {
            // Fallback for MeshCollider or CapsuleCollider
            center = t.InverseTransformPoint(col.bounds.center);
            size = t.InverseTransformVector(col.bounds.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        // Add 2% padding so the wireframe hovers cleanly outside the mesh
        Vector3 extents = (size * 0.5f) * 1.02f;

        // Local corners around the collider center
        Vector3[] localCorners = new Vector3[8]
        {
            center + new Vector3(-extents.x, -extents.y, -extents.z),
            center + new Vector3( extents.x, -extents.y, -extents.z),
            center + new Vector3( extents.x, -extents.y,  extents.z),
            center + new Vector3(-extents.x, -extents.y,  extents.z),
            center + new Vector3(-extents.x,  extents.y, -extents.z),
            center + new Vector3( extents.x,  extents.y, -extents.z),
            center + new Vector3( extents.x,  extents.y,  extents.z),
            center + new Vector3(-extents.x,  extents.y,  extents.z)
        };

        // Transform local corners into world space using object's rotation & position
        Vector3 p0 = t.TransformPoint(localCorners[0]);
        Vector3 p1 = t.TransformPoint(localCorners[1]);
        Vector3 p2 = t.TransformPoint(localCorners[2]);
        Vector3 p3 = t.TransformPoint(localCorners[3]);
        Vector3 p4 = t.TransformPoint(localCorners[4]);
        Vector3 p5 = t.TransformPoint(localCorners[5]);
        Vector3 p6 = t.TransformPoint(localCorners[6]);
        Vector3 p7 = t.TransformPoint(localCorners[7]);

        Vector3[] points = new Vector3[]
        {
            p0, p1, p2, p3, p0,
            p4, p5, p6, p7, p4,
            p5, p1, p2, p6, p7, p3
        };

        lineRenderer.SetPositions(points);
    }

    // Draws fixed grid cube wireframe
    private void DrawCubeOutline(Vector3 center, float size)
    {
        float h = size * 0.505f;

        Vector3 p0 = center + new Vector3(-h, -h, -h);
        Vector3 p1 = center + new Vector3(h, -h, -h);
        Vector3 p2 = center + new Vector3(h, -h, h);
        Vector3 p3 = center + new Vector3(-h, -h, h);

        Vector3 p4 = center + new Vector3(-h, h, -h);
        Vector3 p5 = center + new Vector3(h, h, -h);
        Vector3 p6 = center + new Vector3(h, h, h);
        Vector3 p7 = center + new Vector3(-h, h, h);

        Vector3[] points = new Vector3[]
        {
            p0, p1, p2, p3, p0,
            p4, p5, p6, p7, p4,
            p5, p1, p2, p6, p7, p3
        };

        lineRenderer.SetPositions(points);
    }

    private Vector3Int GetGridPosition(Vector3 worldPos, float scale)
    {
        return new Vector3Int(
            Mathf.FloorToInt((worldPos.x / scale) + 0.5f),
            Mathf.FloorToInt((worldPos.y / scale) + 0.5f),
            Mathf.FloorToInt((worldPos.z / scale) + 0.5f)
        );
    }

    private string CleanObjectName(string rawName)
    {
        return rawName.Replace("(Clone)", "").Trim();
    }
}