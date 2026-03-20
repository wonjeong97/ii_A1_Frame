Shader "Custom/TextureMask_UI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Webcam (UI)", 2D) = "white" {}
        _MaskTex ("Mask Image", 2D) = "white" {} 
        _Color ("Tint", Color) = (1,1,1,1) 
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
                float4 color : COLOR; 
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR; 
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; 
                o.color = v.color * _Color; 
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 maskCol = tex2D(_MaskTex, i.uv);
                
                float maskValue = 1.0 - maskCol.a;
                col.a *= maskValue;
                
                col *= i.color;

                return col;
            }
            ENDCG
        }
    }
}