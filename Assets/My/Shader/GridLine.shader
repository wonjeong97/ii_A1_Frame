Shader "UI/GridLine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,0) 
        _LineColor ("Line Color", Color) = (0.627, 0.612, 0.584, 1) // #A09C95
        _GridSize ("Grid Size", Float) = 10
        _Thickness ("Line Thickness", Range(0, 0.5)) = 0.02
        
        // [추가] 점선 패턴의 반복 빈도 (높을수록 촘촘함)
        _DashFreq ("Dash Frequency", Float) = 50 
        
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
            float _GridSize;
            float _Thickness;
            float _DashFreq; // [추가] 점선 빈도 변수

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

                // 1. 그리드 좌표 계산
                float2 gridPos = uv * _GridSize;
                
                // 2. 선 위치 계산 (0.5, 1.5... 반정수 위치에 선 그리기)
                // (칸의 중앙을 가로지르는 선 -> 점을 네모 안에 가두는 형태 유지)
                float2 dist = abs(frac(gridPos) - 0.5);
                float2 lineAlpha = step(0.5 - dist, _Thickness);
                
                // 3. [수정] 점선 패턴 생성
                // sin 함수를 이용해 주기적으로 0과 1을 오가는 패턴 생성
                // 가로선(Horizontal)은 X축 좌표에 따라 패턴이 생김
                float dashPatternX = step(0, sin(gridPos.x * _DashFreq));
                // 세로선(Vertical)은 Y축 좌표에 따라 패턴이 생김
                float dashPatternY = step(0, sin(gridPos.y * _DashFreq));

                // 4. 선과 패턴 합성
                // 세로선(lineAlpha.x)은 Y축 패턴(dashPatternY) 적용
                float showVertical = lineAlpha.x * dashPatternY;
                // 가로선(lineAlpha.y)은 X축 패턴(dashPatternX) 적용
                float showHorizontal = lineAlpha.y * dashPatternX;

                float isLine = max(showVertical, showHorizontal);

                // 색상 합성 (점 관련 로직은 삭제됨)
                fixed4 finalColor = lerp(_Color, _LineColor, isLine);
                finalColor.a *= IN.color.a;

                return finalColor;
            }
            ENDCG
        }
    }
}