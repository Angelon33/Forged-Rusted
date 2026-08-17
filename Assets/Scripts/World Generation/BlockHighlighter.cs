using UnityEngine;
using TMPro;

public class BlockHighlighter : MonoBehaviour
{
    [Header("Settings")]
    public Camera playerCamera;
    public VoxelGrid voxelGrid;
    public Color outlineColor = Color.yellow;
    public float lineWidth = 0.02f;
    public Material outlineMaterial; // Added missing outline material reference

    [Header("UI Reference")]
    public TextMeshProUGUI targetBlockText;

    private LineRenderer lineRenderer;
    private float reachDistance = 5f; // Automatically updated from BlockInteraction
    private OutlineObject currentHighlightedObject; // Added missing highlight tracker

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;

        // Fetch reach distance from BlockInteraction on the same GameObject
        BlockInteraction interaction = GetComponent<BlockInteraction>();
        if (interaction != null)
        {
            reachDistance = interaction.reachDistance;
        }

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
            bool isVoxelBlock = (voxelGrid != null && hit.transform.IsChildOf(voxelGrid.transform))
                                || hit.collider.GetComponent<BlockData>() != null;

            if (isVoxelBlock)
            {
                // Clear any prop highlight
                ClearCurrentHighlight();

                // 1. VOXEL GRID CUBE HIGHLIGHT
                lineRenderer.enabled = true;
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
                // Disable cube line wireframe
                lineRenderer.enabled = false;

                // 2. COMPLEX OBJECT MESH HIGHLIGHT
                OutlineObject outlineObj = hit.collider.GetComponentInParent<OutlineObject>();

                // Automatically add component if missing on standalone props
                if (outlineObj == null)
                {
                    outlineObj = hit.collider.gameObject.AddComponent<OutlineObject>();
                }

                if (currentHighlightedObject != outlineObj)
                {
                    ClearCurrentHighlight();
                    currentHighlightedObject = outlineObj;
                    currentHighlightedObject.outlineMaterial = outlineMaterial;
                    currentHighlightedObject.EnableHighlight(true);
                }

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
            ClearCurrentHighlight();

            if (targetBlockText != null)
            {
                targetBlockText.text = "";
            }
        }
    }

    private void ClearCurrentHighlight()
    {
        if (currentHighlightedObject != null)
        {
            currentHighlightedObject.EnableHighlight(false);
            currentHighlightedObject = null;
        }
    }

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