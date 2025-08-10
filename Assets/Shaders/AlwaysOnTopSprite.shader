Shader "Custom/AlwaysOnTopSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        // 讓此材質在所有 Geometry 之後繪製，
        // 同時不受深度影響 (ZTest Always)。
        Tags { 
            "Queue"="Overlay"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
        }

        Pass
        {
            ZWrite Off       // 不要寫入深度
            ZTest Always     // 忽略深度檢測，總是繪製
            Cull Off         // 不裁面
            Blend SrcAlpha OneMinusSrcAlpha  // 透明混合

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            v2f vert (appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = TRANSFORM_TEX(IN.texcoord, _MainTex);
                // 將頂點的頂色 * 你在材質中設定的顏色
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float4 frag (v2f IN) : SV_Target
            {
                float4 c = tex2D(_MainTex, IN.uv) * IN.color;
                return c;
            }
            ENDCG
        }
    }
}
