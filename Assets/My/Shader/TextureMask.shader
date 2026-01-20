Shader "Custom/TextureMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Webcam (UI)", 2D) = "white" {}
        _MaskTex ("Mask Image", 2D) = "white" {} 
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; 
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 maskCol = tex2D(_MaskTex, i.uv);

                // 마스크 이미지: 도형 부분(Alpha 0) -> 보임(1), 배경(Alpha 1) -> 안보임(0)
                float maskValue = 1.0 - maskCol.a;

                // [수정됨] 배경색 합성 대신, 알파값(투명도)에 마스크 적용
                // maskValue가 1이면 불투명(보임), 0이면 투명(안보임)
                col.a *= maskValue;

                return col;
            }
            ENDCG
        }
    }
}