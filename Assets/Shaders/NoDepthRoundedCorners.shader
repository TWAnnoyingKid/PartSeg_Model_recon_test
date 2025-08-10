Shader "Custom/NoDepthRoundedCorners" {
    Properties {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        // 如果 RoundedCorners 有其他屬性，也可在這裡定義
    }
    SubShader {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Lighting Off
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                half2 texcoord : TEXCOORD0;
            };

            fixed4 _Color;
            sampler2D _MainTex;

            v2f vert (appdata_t v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 texCol = tex2D(_MainTex, i.texcoord);
                return texCol * i.color;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
