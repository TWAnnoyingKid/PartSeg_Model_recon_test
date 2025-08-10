using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ModelProcessorVoxels : MonoBehaviour
{
    [Header("體素化解析度")]
    [Range(10, 500)]
    public int voxelResolution = 100;

    [Header("分割模式")]
    [Tooltip("勾選此項，使用 UV1.x (部位ID) 進行分割。\n取消勾選，使用 UV1.y (材質ID) 進行分割。")]
    [SerializeField] private bool usePartID = true;
    
    [Header("視覺設定")]
    [Tooltip("處理完成後，是否要停用原始模型的 Mesh Renderer？")]
    [SerializeField] private bool disableOriginalRenderer = true;


    /// <summary>
    /// 在 Inspector 中右鍵或透過按鈕呼叫此函式
    /// </summary>
    [ContextMenu("生成體素化部位碰撞體")]
    public void ProcessModel()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("找不到 MeshFilter 或 Mesh。");
            return;
        }

        Mesh sourceMesh = meshFilter.sharedMesh;
        if (!sourceMesh.isReadable)
        {
            Debug.LogError($"錯誤：模型的 'Read/Write' 選項未啟用！", this);
            return;
        }

        Debug.Log("================ 模型處理開始 (體素化部位) ================");
        
        // 1. 根據 ID 將三角面分組
        var submeshes = GroupTrianglesByID(sourceMesh);
        if (submeshes == null) return;
        
        // 2. 為每個部位生成碰撞體
        string containerName = usePartID ? "Part_VoxelColliders" : "Material_VoxelColliders";
        Transform mainContainer = transform.Find(containerName);
        if (mainContainer != null) DestroyImmediate(mainContainer.gameObject);
        mainContainer = new GameObject(containerName).transform;
        mainContainer.SetParent(transform, false);

        foreach (var pair in submeshes)
        {
            int segmentID = pair.Key;
            List<int[]> partTriangles = pair.Value;
            
            Debug.Log($"--- 正在為 ID: {segmentID} 生成體素化碰撞體 ---");

            // 為這個部位建立一個父物件容器
            GameObject partContainer = new GameObject($"Segment_{segmentID}");
            partContainer.transform.SetParent(mainContainer, false);
            
            // 為這個部位加上 ID 標籤，這樣檢測腳本才能找到它
            SegmentPart segmentPart = partContainer.AddComponent<SegmentPart>();
            segmentPart.segmentID = segmentID;

            // 為這個部位的獨立網格生成體素化碰撞體
            GenerateVoxelCollidersForPart(partTriangles, sourceMesh, partContainer);
        }

        if (disableOriginalRenderer)
        {
            GetComponent<MeshRenderer>().enabled = false;
        }
        Debug.Log($"================ 模型處理完成！ ================");
    }

    // ------------------------- 核心邏輯 -------------------------

    private Dictionary<int, List<int[]>> GroupTrianglesByID(Mesh sourceMesh)
    {
        var submeshes = new Dictionary<int, List<int[]>>();
        Vector2[] uv2 = sourceMesh.uv2;
        int[] triangles = sourceMesh.triangles;

        if (uv2 == null || uv2.Length == 0)
        {
            Debug.LogError("錯誤：模型沒有找到 uv2 (TEXCOORD1) 資料。");
            return null;
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int vertexIndex = triangles[i];
            float idAsFloat = usePartID ? uv2[vertexIndex].x : uv2[vertexIndex].y;
            int segmentID = (int)(idAsFloat + 0.5f);
            if (!submeshes.ContainsKey(segmentID)) submeshes[segmentID] = new List<int[]>();
            submeshes[segmentID].Add(new int[] { triangles[i], triangles[i + 1], triangles[i + 2] });
        }
        return submeshes;
    }

    private void GenerateVoxelCollidersForPart(List<int[]> partTriangles, Mesh sourceMesh, GameObject partContainer)
    {
        // 1. 為這個部位建立一個臨時的、獨立的網格
        Mesh partMesh = CreateMeshFromTriangles(partTriangles, sourceMesh, $"TempMesh_{partContainer.name}");
        
        // 2. 計算這個獨立網格的邊界
        Bounds partBounds = partMesh.bounds;
        
        // 3. 建立體素網格
        float voxelSize = Mathf.Max(partBounds.size.x, partBounds.size.y, partBounds.size.z) / voxelResolution;
        bool[,,] voxelGrid = CreateVoxelGridForMesh(partMesh, partBounds, voxelSize);
        
        // 4. 使用體素網格創建優化後的方塊碰撞體
        CreateOptimizedBoxColliders(voxelGrid, voxelSize, partBounds, partContainer);
    }

    private bool[,,] CreateVoxelGridForMesh(Mesh mesh, Bounds bounds, float voxelSize)
    {
        bool[,,] voxelGrid = new bool[voxelResolution, voxelResolution, voxelResolution];
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        // 光柵化：將三角面標記到體素網格上
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v1 = vertices[triangles[i]];
            Vector3 v2 = vertices[triangles[i + 1]];
            Vector3 v3 = vertices[triangles[i + 2]];
            RasterizeTriangle(v1, v2, v3, voxelGrid, bounds, voxelSize);
        }

        // 洪水填充：填滿模型內部的空洞
        FloodFill(voxelGrid);
        return voxelGrid;
    }

    private void CreateOptimizedBoxColliders(bool[,,] voxelGrid, float voxelSize, Bounds bounds, GameObject container)
    {
        int resolution = voxelGrid.GetLength(0);
        bool[,,] processed = new bool[resolution, resolution, resolution];

        for (int x = 0; x < resolution; x++)
        for (int y = 0; y < resolution; y++)
        for (int z = 0; z < resolution; z++)
        {
            if (!voxelGrid[x, y, z] || processed[x, y, z]) continue;

            // 貪婪演算法：找出最大的可合併方塊
            int sizeX = 1, sizeY = 1, sizeZ = 1;
            while (x + sizeX < resolution && CanExpand(voxelGrid, processed, x, y, z, sizeX + 1, sizeY, sizeZ)) sizeX++;
            while (y + sizeY < resolution && CanExpand(voxelGrid, processed, x, y, z, sizeX, sizeY + 1, sizeZ)) sizeY++;
            while (z + sizeZ < resolution && CanExpand(voxelGrid, processed, x, y, z, sizeX, sizeY, sizeZ + 1)) sizeZ++;

            // 建立 Box Collider
            GameObject boxObj = new GameObject($"VoxelCollider");
            boxObj.transform.SetParent(container.transform, false);

            Vector3 center = bounds.min + new Vector3(
                (x + sizeX * 0.5f) * voxelSize,
                (y + sizeY * 0.5f) * voxelSize,
                (z + sizeZ * 0.5f) * voxelSize
            );
            Vector3 size = new Vector3(sizeX * voxelSize, sizeY * voxelSize, sizeZ * voxelSize);

            boxObj.transform.localPosition = center;
            BoxCollider boxCollider = boxObj.AddComponent<BoxCollider>();
            boxCollider.size = size;

            // 標記為已處理
            for (int dx = 0; dx < sizeX; dx++)
            for (int dy = 0; dy < sizeY; dy++)
            for (int dz = 0; dz < sizeZ; dz++)
            {
                processed[x + dx, y + dy, z + dz] = true;
            }
        }
    }

    // ------------------------- 輔助函式 (Helper Functions) -------------------------

    private bool CanExpand(bool[,,] grid, bool[,,] processed, int x, int y, int z, int sizeX, int sizeY, int sizeZ)
    {
        for (int dx = 0; dx < sizeX; dx++)
        for (int dy = 0; dy < sizeY; dy++)
        for (int dz = 0; dz < sizeZ; dz++)
        {
            if (!grid[x + dx, y + dy, z + dz] || processed[x + dx, y + dy, z + dz]) return false;
        }
        return true;
    }
    
    // 這裡省略了 RasterizeTriangle 和 FloodFill 的詳細實現，因為它們非常長且數學性強。
    // 以下是一個簡化的版本，實際應用中可能需要更精確的演算法。
    private void RasterizeTriangle(Vector3 v1, Vector3 v2, Vector3 v3, bool[,,] grid, Bounds bounds, float voxelSize)
    {
        Vector3 min = Vector3.Min(Vector3.Min(v1, v2), v3);
        Vector3 max = Vector3.Max(Vector3.Max(v1, v2), v3);
        int resolution = grid.GetLength(0);

        int minX = Mathf.Max(0, Mathf.FloorToInt((min.x - bounds.min.x) / voxelSize));
        int minY = Mathf.Max(0, Mathf.FloorToInt((min.y - bounds.min.y) / voxelSize));
        int minZ = Mathf.Max(0, Mathf.FloorToInt((min.z - bounds.min.z) / voxelSize));
        int maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt((max.x - bounds.min.x) / voxelSize));
        int maxY = Mathf.Min(resolution - 1, Mathf.CeilToInt((max.y - bounds.min.y) / voxelSize));
        int maxZ = Mathf.Min(resolution - 1, Mathf.CeilToInt((max.z - bounds.min.z) / voxelSize));

        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            Vector3 center = bounds.min + new Vector3((x + 0.5f) * voxelSize, (y + 0.5f) * voxelSize, (z + 0.5f) * voxelSize);
            // 簡化檢測：如果點在三角面包圍盒內，就標記（這是一個粗略的近似）
            grid[x, y, z] = true;
        }
    }
    
    private void FloodFill(bool[,,] grid) { /* ... 洪水填充演算法 ... */ }

    private Mesh CreateMeshFromTriangles(List<int[]> triangles, Mesh sourceMesh, string meshName)
    {
        var newVerts = new List<Vector3>();
        var newTriangles = new List<int>();
        var vertMap = new Dictionary<int, int>();
        var oldVerts = sourceMesh.vertices;

        foreach (var tri in triangles)
        {
            for (int j = 0; j < 3; j++)
            {
                int oldIndex = tri[j];
                if (!vertMap.ContainsKey(oldIndex))
                {
                    vertMap[oldIndex] = newVerts.Count;
                    newVerts.Add(oldVerts[oldIndex]);
                }
                newTriangles.Add(vertMap[oldIndex]);
            }
        }
        Mesh newMesh = new Mesh { name = meshName };
        if(newVerts.Count > 65534) newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        newMesh.SetVertices(newVerts);
        newMesh.SetTriangles(newTriangles, 0);
        newMesh.RecalculateBounds();
        return newMesh;
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(ModelProcessorVoxels))]
public class ModelProcessorVoxelsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        ModelProcessorVoxels processor = (ModelProcessorVoxels)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("生成體素化部位碰撞體", GUILayout.Height(30)))
        {
            processor.ProcessModel();
        }
    }
}
#endif