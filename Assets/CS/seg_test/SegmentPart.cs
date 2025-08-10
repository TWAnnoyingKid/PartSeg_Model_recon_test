using UnityEngine;

/// <summary>
/// 一個簡單的資料容器，像標籤一樣附加在自動生成的部位碰撞體物件上，
/// 用來儲存該部位的 ID。
/// </summary>
public class SegmentPart : MonoBehaviour
{
    [Tooltip("這個部位的 ID")]
    public int segmentID = -1;
}