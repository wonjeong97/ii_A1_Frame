Shader "UI/GridLine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,0) 
        _LineColor ("Line Color", Color) = (0.627, 0.612, 0.584, 1) // #A09C95
        
        // Grid Size를 X(가로)와 Y(세로)로 분리
        _GridSizeX ("Grid Size X", Float) = 6
        _GridSizeY ("Grid Size Y", Float) = 5
        
        _Thickness ("Line Thickness", Range(0, 0.5)) = 0.02
        
        // 점선 패턴의 반복 빈도 (높을수록 촘촘함)
        _DashFreq ("Dash Frequency", Float) = 50
        
        _MaskTex ("Mask Texture", 2D) = "black" {}
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp] 
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };
            
            fixed4 _Color;
            fixed4 _LineColor;
            
            // 선언부 분리
            float _GridSizeX;
            float _GridSizeY;
            
            float _Thickness;
            float _DashFreq;
            sampler2D _MaskTex;
            
            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 gridPos = float2(uv.x * _GridSizeX, uv.y * _GridSizeY);
                
                float2 dist = abs(frac(gridPos) - 0.5);
                float2 lineAlpha = step(0.5 - dist, _Thickness);
                
                float dashPatternX = step(0, sin(gridPos.x * _DashFreq));
                float dashPatternY = step(0, sin(gridPos.y * _DashFreq));
                
                float showVertical = lineAlpha.x * dashPatternY;
                float showHorizontal = lineAlpha.y * dashPatternX;

                float isLine = max(showVertical, showHorizontal);

                // 색상 합성
                fixed4 finalColor = lerp(_Color, _LineColor, isLine);
                finalColor.a *= IN.color.a;
                
                fixed4 maskCol = tex2D(_MaskTex, IN.texcoord);
                float alphaFactor = 1.0 - maskCol.r;
                finalColor.a *= alphaFactor;

                return finalColor;
            }
            ENDCG
        }
    }
}