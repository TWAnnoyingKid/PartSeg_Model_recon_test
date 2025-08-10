using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraToggle : MonoBehaviour
{
    public Camera unityCamera; // Unity
    public Camera arCamera;    // AR
    public Button toggleButton;

    private bool isARActive = false;

    void Start()
    {
        if (unityCamera == null || arCamera == null || toggleButton == null)
        {
            Debug.LogError("找不到相機或切換按鈕");
            return;
        }

        arCamera.gameObject.SetActive(false); // 切換相機
        toggleButton.onClick.AddListener(ToggleCamera);
    }

    void ToggleCamera()
    {
        isARActive = !isARActive;
        unityCamera.gameObject.SetActive(!isARActive);
        arCamera.gameObject.SetActive(isARActive);
    }
}
