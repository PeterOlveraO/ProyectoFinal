Shader "Game/Weather/LocalRain"
{
    Properties
    {
        _Intensity ("Weather strength", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Name "LocalRain"
            Tags { "LightMode"="SRPDefaultUnlit" }
            // Multiplicative brightening. Even a foreground drop projected over closed
            // ground produces 0 * (1 + rain) = 0, never an additive silhouette over black.
            Blend DstColor One
            ColorMask RGB
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float4 _LocalArea;
                float4 _ClosedRects[9];
                int _ClosedCount;
                float4 _WorldBounds;
                float _HasWorldBounds;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXZ : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.worldXZ = world.xz;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 local = abs(input.worldXZ - _LocalArea.xy);
                clip(_LocalArea.z - max(local.x, local.y));
                if (_HasWorldBounds > .5)
                {
                    float2 inside = min(input.worldXZ - _WorldBounds.xy, _WorldBounds.zw - input.worldXZ);
                    clip(min(inside.x, inside.y));
                }
                [unroll] for (int i = 0; i < 9; i++)
                {
                    if (i >= _ClosedCount) break;
                    float4 rect = _ClosedRects[i];
                    float2 outside = max(rect.xy - input.worldXZ, input.worldXZ - rect.zw);
                    // Keep a little clearance from the fog's feathered frontier as well.
                    clip(max(outside.x, outside.y) - 1.0);
                }
                // A narrow, crisp streak; no generated texture, soft-particle depth copy or noise.
                clip(.24 - abs(input.uv.x - .5));
                half fade = saturate(min(input.uv.y, 1 - input.uv.y) * 5);
                half strength = fade * input.color.a * _Intensity;
                return half4(input.color.rgb * strength, 0);
            }
            ENDHLSL
        }
    }
}
