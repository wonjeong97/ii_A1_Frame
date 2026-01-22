Shader "Custom/TextureMask"
{
    Properties
    {
        _MainTex ("Webcam (RGB)", 2D) = "white" {}
        _MaskTex ("Mask Image", 2D) = "white" {} 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        
        Cull Off 
        Lighting Off 
        ZWrite Off 
        ZTest Always

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
            float4 _MainTex_TexelSize; 
            
            sampler2D _MaskTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) 
                    o.uv.y = 1 - o.uv.y;
                #endif
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 maskCol = tex2D(_MaskTex, i.uv);

                float maskValue = 1.0 - maskCol.a;

                col.a *= maskValue;

                return col;
            }
            ENDCG
        }
    }
}