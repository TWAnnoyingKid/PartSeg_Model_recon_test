using UnityEngine;
using TMPro;

/// <summary>
/// 【球體專用 - 觸發器最終版】
/// 附加在球體上，使用 Is Trigger 模式來偵測與模型部位的「進入」和「離開」事件。
/// 這個版本效能高，且不會有任何不必要的物理反應，是 XR 互動的標準作法。
/// </summary>
public class SphereContactDetector : MonoBehaviour
{
    [Header("UI 設定")]
    [Tooltip("用來顯示 Segment ID 的 TextMeshProUGUI 元件")]
    [SerializeField] private TMP_Text segText;
    
    [Header("顯示設定")]
    [SerializeField] private string defaultText = "接觸部位: 無";
    [SerializeField] private string prefixText = "接觸部位 ID: ";

    private int lastContactedID = -1;

    void Start()
    {
        // 確保 Rigidbody 存在且為 Kinematic，這是觸發器工作的最佳設定
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null || !rb.isKinematic)
        {
            Debug.LogWarning("SphereContactDetector 警告：為了獲得最佳的觸發器效果，建議為球體加上 Rigidbody 並將其設定為 Is Kinematic。", this);
        }

        if (segText != null)
        {
            segText.text = defaultText;
        }
    }

    /// <summary>
    /// 當 Collider 進入這個物件的觸發器時，會被呼叫。
    /// </summary>
    /// <param name="other">進入觸發器區域的另一個物件的 Collider</param>
    private void OnTriggerEnter(Collider other)
    {
        // 嘗試從接觸到的物件上獲取 SegmentPart 腳本
        SegmentPart part = other.GetComponent<SegmentPart>();

        // 如果成功獲取到，代表我們碰到了正確的部位
        if (part != null)
        {
            lastContactedID = part.segmentID;
            UpdateUI(lastContactedID);
        }
    }
    
    /// <summary>
    /// 當 Collider 停留在這個物件的觸發器內時，每一幀都會被呼叫。
    /// (如果只需要在進入時更新一次，可以省略此函式)
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        SegmentPart part = other.GetComponent<SegmentPart>();
        if (part != null)
        {
            // 如果當前顯示的ID不是我們正在接觸的ID，就更新它
            if (lastContactedID != part.segmentID)
            {
                lastContactedID = part.segmentID;
                UpdateUI(lastContactedID);
            }
        }
    }


    /// <summary>
    /// 當 Collider 離開這個物件的觸發器時，會被呼叫。
    /// </summary>
    /// <param name="other">離開觸發器區域的另一個物件的 Collider</param>
    private void OnTriggerExit(Collider other)
    {
        // 檢查離開的物件是否是我們上一次接觸的那個部位
        SegmentPart part = other.GetComponent<SegmentPart>();
        if (part != null && part.segmentID == lastContactedID)
        {
            // 如果是，就重設 ID 和 UI
            lastContactedID = -1;
            UpdateUI(lastContactedID);
        }
    }

    private void UpdateUI(int id)
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