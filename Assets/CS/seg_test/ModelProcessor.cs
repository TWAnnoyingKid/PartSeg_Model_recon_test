using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ModelProcessor : MonoBehaviour
{
    [Tooltip("處理完成後，是否要停用原始模型的 Mesh Renderer？")]
    [SerializeField] private bool disableOriginalRenderer = true;

    [Header("使用部位或材質分割")]
    [Tooltip("勾選此項，使用 UV1.x (部位ID) 進行分割。\n取消勾選，使用 UV1.y (材質ID) 進行分割。")]
    [SerializeField] private bool usePartID = true;

    [Header("自動分割設定")]
    [Tooltip("單一 mesh collider 最大頂點數上限")]
    [SerializeField] private int vertexLimitPerCollider = 65000;

    public void ProcessModelIntoParts()
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
            Debug.LogError($"err:模型的 'Read/Write' 選項未啟用！", this);
            return;
        }

        var submeshes = new Dictionary<int, List<int[]>>();
        Vector2[] uv2 = sourceMesh.uv2;
        int[] triangles = sourceMesh.triangles;

        if (uv2 == null || uv2.Length == 0)
        {
            Debug.LogError("err:模型沒有找到 uv2 (TEXCOORD1) 資料。");
            return;
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int vertexIndex = triangles[i];
            int segmentID = (int)(usePartID ? uv2[vertexIndex].x : uv2[vertexIndex].y); // 使用 UV1.x 或 UV1.y 來獲取 ID
            if (!submeshes.ContainsKey(segmentID)) submeshes[segmentID] = new List<int[]>();
            submeshes[segmentID].Add(new int[] { triangles[i], triangles[i + 1], triangles[i + 2] });
        }

        string containerName = usePartID ? "Part_Colliders" : "Material_Colliders";
        Transform colliderContainer = transform.Find(containerName);
        if (colliderContainer != null) DestroyImmediate(colliderContainer.gameObject);
        colliderContainer = new GameObject(containerName).transform;
        colliderContainer.SetParent(transform, false);

        foreach (var pair in submeshes)
        {
            int segmentID = pair.Key;
            List<int[]> partTriangles = pair.Value;
            
            Debug.Log($"正在為 ID: {segmentID} 建立碰撞體... 原始三角面數: {partTriangles.Count}");

            List<List<int[]>> chunks = SplitTrianglesByVertexLimit(partTriangles);

            for (int i = 0; i < chunks.Count; i++)
            {
                List<int[]> chunkTriangles = chunks[i];
                string objectName = chunks.Count > 1 ? $"SegmentCollider_{segmentID}_Chunk{i}" : $"SegmentCollider_{segmentID}";
                
                GameObject partObject = new GameObject(objectName);
                partObject.transform.SetParent(colliderContainer, false);

                Mesh newMesh = CreateMeshFromTriangles(chunkTriangles, sourceMesh, $"SegmentMesh_{segmentID}_Chunk{i}");
                
                MeshCollider meshCollider = partObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = newMesh;
                meshCollider.convex = false;

                SegmentPart segmentPart = partObject.AddComponent<SegmentPart>();
                segmentPart.segmentID = segmentID;

                Debug.Log($" -> {objectName} 建立完成，包含 {newMesh.vertexCount} 個頂點。");
            }
        }

        if (disableOriginalRenderer)
        {
            GetComponent<MeshRenderer>().enabled = false;
        }

        Debug.Log("done");
    }

    // 將一個大的三角面列表分割成多個小的區塊，確保每個區塊的頂點數不超過上限。
    private List<List<int[]>> SplitTrianglesByVertexLimit(List<int[]> originalTriangles)
    {
        var chunks = new List<List<int[]>>();
        if (originalTriangles.Count == 0) return chunks;

        var currentChunkTriangles = new List<int[]>();
        var currentChunkUniqueVerts = new HashSet<int>();

        foreach (var tri in originalTriangles)
        {
            bool canAdd = true;
            // 檢查加入這個三角面的三個頂點後，是否會超過上限
            foreach (int vertIndex in tri)
            {
                // 只有當這是一個「新」頂點，且加入後會「正好」超過上限時，才需要分割
                if (!currentChunkUniqueVerts.Contains(vertIndex) && currentChunkUniqueVerts.Count >= vertexLimitPerCollider)
                {
                    canAdd = false;
                    break;
                }
            }

            // 如果當前區塊已滿，無法再加入新頂點
            if (!canAdd && currentChunkTriangles.Count > 0)
            {
                chunks.Add(currentChunkTriangles); // 將已滿的舊區塊儲存起來
                // 開始一個全新的區塊
                currentChunkTriangles = new List<int[]>();
                currentChunkUniqueVerts = new HashSet<int>();
            }

            // 將這個三角面加入到當前的區塊
            currentChunkTriangles.Add(tri);
            foreach (int vertIndex in tri)
            {
                currentChunkUniqueVerts.Add(vertIndex);
            }
        }

        // 將最後一個（或唯一一個）區塊也加進去
        if (currentChunkTriangles.Count > 0)
        {
            chunks.Add(currentChunkTriangles);
        }

        if (chunks.Count > 1)
        {
             Debug.LogWarning($"一個大部位已被自動分割成 {chunks.Count} 個小區塊以符合頂點數上限");
        }

        return chunks;
    }

    // 從指定的三角面列表建立一個新的網格物件
    private Mesh CreateMeshFromTriangles(List<int[]> triangles, Mesh sourceMesh, string meshName)
    {
        var newVerts = new List<Vector3>();
        var newNormals = new List<Vector3>();
        var newUv0 = new List<Vector2>();
        var newTriangles = new List<int>();
        var vertMap = new Dictionary<int, int>();

        var oldVerts = sourceMesh.vertices;
        var oldNormals = sourceMesh.normals;
        var oldUv0 = sourceMesh.uv;

        foreach (var tri in triangles)
        {
            for (int j = 0; j < 3; j++)
            {
                int oldIndex = tri[j];
                if (!vertMap.ContainsKey(oldIndex))
                {
                    vertMap[oldIndex] = newVerts.Count;
                    newVerts.Add(oldVerts[oldIndex]);
                    if(oldNormals.Length > oldIndex) newNormals.Add(oldNormals[oldIndex]);
                    if(oldUv0.Length > oldIndex) newUv0.Add(oldUv0[oldIndex]);
                }
                newTriangles.Add(vertMap[oldIndex]);
            }
        }

        Mesh newMesh = new Mesh();
        newMesh.name = meshName;
        if(newVerts.Count > 65534) newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        newMesh.SetVertices(newVerts);
        newMesh.SetNormals(newNormals);
        newMesh.SetUVs(0, newUv0);
        newMesh.SetTriangles(newTriangles, 0);
        newMesh.RecalculateBounds();
        return newMesh;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ModelProcessor))]
public class ModelProcessorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        ModelProcessor processor = (ModelProcessor)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("生成碰撞體 (Process Model)"))
        {
            processor.ProcessModelIntoParts();
        }
    }
}
#endif