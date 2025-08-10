Shader "Unlit/SegmentIDColor"
{
    // 這個著色器沒有任何需要從編輯器調整的屬性
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            // appdata 結構定義了從 3D 模型傳入到「頂點著色器」的每個頂點的資料
            struct appdata
            {
                float4 vertex : POSITION;       // 頂點位置
                float2 uv     : TEXCOORD0;      // uv0 用於貼圖
                float2 uv1    : TEXCOORD1;      // uv1 用它的 x 分量來儲存 ID
            };

            // v2f 結構 (vertex to fragment)：定義了從「頂點著色器」傳遞到「片元著色器」的資料
            struct v2f
            {
                float4 vertex    : SV_POSITION;  // 處理過的頂點裁切空間位置
                float  segmentID : TEXCOORD1;    // 將從 uv1.x 讀取的 ID 存放在這裡，傳遞給片元著色器
            };

            // 頂點著色器 (Vertex Shader)：對模型的每一個頂點執行一次
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex); // 將頂點的本地座標轉換到螢幕的裁切空間座標
                o.segmentID = v.uv1.x; // 讀取第二組 UV 的 x 座標，並將它存入 v2f 結構以便傳遞
                return o;
            }
            
            // 片元著色器 (Fragment Shader)：對螢幕上的每一個像素執行一次，來決定它的最終顏色
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col;
                // 由於浮點數可能有微小誤差，使用 > 0.5, > 1.5 這樣的比較會比直接用 == 更穩健
                if (i.segmentID > 3.5) {
                    col = fixed4(1.0, 1.0, 0.0, 1.0); // ID 4 = 黃色
                } else if (i.segmentID > 2.5) {
                    col = fixed4(0.0, 0.0, 1.0, 1.0); // ID 3 = 藍色
                } else if (i.segmentID > 1.5) {
                    col = fixed4(0.0, 1.0, 0.0, 1.0); // ID 2 = 綠色
                } else if (i.segmentID > 0.5) {
                    col = fixed4(1.0, 0.0, 0.0, 1.0); // ID 1 = 紅色
                } else {
                    col = fixed4(1.0, 0.0, 1.0, 1.0); // ID 0 = 紫色
                }
                
                return col; // 回傳最終計算出的像素顏色
            }
            ENDCG
        }
    }
}