using UnityEngine;
using TMPro;
public class SphereContactDetectorVoxels : MonoBehaviour
{
    [Header("UI 設定")]
    [Tooltip("用來顯示 Segment ID 的 TextMeshProUGUI 元件")]
    [SerializeField] private TMP_Text segText;
    
    [Header("顯示設定")]
    [Tooltip("沒有接觸時顯示的預設文字")]
    [SerializeField] private string defaultText = "接觸部位: 無";
    [Tooltip("找到接觸時顯示的文字前綴")]
    [SerializeField] private string prefixText = "接觸部位 ID: ";

    private int currentContactedID = -1;

    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null || !rb.isKinematic)
        {
            Debug.LogWarning("SphereContactDetectorVoxels 警告：為了獲得最佳的觸發器效果，建議為球體加上 Rigidbody 並將其設定為 Is Kinematic。", this);
        }

        // 初始化UI顯示
        if (segText != null)
        {
            segText.text = defaultText;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 從接觸到的碰撞體(other)向上查找其父物件，看是否掛載了 SegmentPart 腳本
        SegmentPart part = other.GetComponentInParent<SegmentPart>();

        // 如果成功獲取到，代表我們碰到了正確的部位
        if (part != null)
        {
            // 只有在接觸到新的ID時才更新
            if (currentContactedID != part.segmentID)
            {
                currentContactedID = part.segmentID;
                UpdateUI(currentContactedID);
            }
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        SegmentPart part = other.GetComponentInParent<SegmentPart>();
        if (part != null)
        {
            // 如果當前顯示的ID不是我們正在接觸的ID，就更新它
            if (currentContactedID != part.segmentID)
            {
                currentContactedID = part.segmentID;
                UpdateUI(currentContactedID);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 檢查離開的物件是否是我們上一次接觸的那個部位
        SegmentPart part = other.GetComponentInParent<SegmentPart>();
        if (part != null && part.segmentID == currentContactedID)
        {
            // 如果是，就重設 ID 和 UI
            currentContactedID = -1;
            UpdateUI(currentContactedID);
        }
    }

    private void UpdateUI(int id) // 根據傳入的 ID 更新 UI 文字
    {
        if (segText == null) return;

        if (id >= 0)
        {
            segText.text = prefixText + id.ToString();
        }
        else
        {
            segText.text = defaultText;
        }
    }
}