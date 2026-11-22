Shader "Game/WorldZoneFog"
{
    Properties
    {
        _Feather ("Soft edge (world units)", Float) = 2
    }
    SubShader
    {
        // Ground, water, paths and shore write opaque depth. Fog precedes every sprite.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Geometry+499" }
        Pass
        {
            Name "WorldZoneFog"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset 0, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ClosedRects[9];
                int _ClosedCount;
                float _Feather;
                float4 _RevealRect;
                float _RevealAlpha;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 worldXZ : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.worldXZ = world.xz;
                output.positionCS = TransformWorldToHClip(world);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float distanceToClosed = 100000;
                [unroll] for (int i = 0; i < 9; i++)
                {
                    if (i >= _ClosedCount) break;
                    float4 rect = _ClosedRects[i];
                    float2 outside = max(max(rect.xy - input.worldXZ, input.worldXZ - rect.zw), 0);
                    distanceToClosed = min(distanceToClosed, length(outside));
                }
                // Entire closed region is solid black. The soft edge extends into open
                // ground, so hidden terrain cannot leak through a translucent closed side.
                half alpha = 1 - smoothstep(0, max(.001, _Feather), distanceToClosed);
                float2 revealOutside = max(max(_RevealRect.xy - input.worldXZ, input.worldXZ - _RevealRect.zw), 0);
                alpha = max(alpha, _RevealAlpha * (1 - smoothstep(0, max(.001, _Feather), length(revealOutside))));
                clip(alpha - .001);
                return half4(0, 0, 0, alpha);
            }
            ENDHLSL
        }
    }
}
