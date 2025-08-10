Shader "Custom/OcclusionOnly" 
{
    SubShader
    {
        Tags { "Queue"="Geometry-1" "RenderType"="Opaque" }
        Pass
        {
            ZWrite On        // 啟用深度寫入
            ZTest LEqual     // 如果物件在相機前方(同等或更近)才畫深度
            Cull Back        // 背面裁切
            ColorMask 0      // 不渲染任何顏色 (只寫入深度)

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // 只寫深度，不輸出顏色
                return float4(0,0,0,0);
            }
            ENDCG
        }
    }
}
