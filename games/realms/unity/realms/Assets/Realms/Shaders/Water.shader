// The sea: one transparent plane at y = 0 coloured by the depth of the ground below (read from
// the camera depth texture), clear grey-green over sand, deep muted blue far out; a line of foam
// on the waterline, soft ripples and passing glints in the open water; the kingdoms' waters
// carry their tint and borders.
Shader "Realms/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.41, 0.69, 0.66, 1)
        _Mid ("Middle", Color) = (0.34, 0.59, 0.63, 1)
        _Deep ("Deep", Color) = (0.21, 0.36, 0.55, 1)
        _Abyss ("Far out", Color) = (0.17, 0.3, 0.48, 1)
        _Foam ("Foam", Color) = (0.88, 0.9, 0.9, 1)
        _Clarity ("See-through at the shore", Range(0, 1)) = 0.35
        _Tint ("Territory tint", Range(0, 1)) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" "IgnoreProjector"="True" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Realms/Shaders/RealmsCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Mid, _Deep, _Abyss, _Foam;
                float _Clarity, _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float3 GroundAt(float2 uv)
            {
                #if UNITY_REVERSED_Z
                    float raw = SampleSceneDepth(uv);
                #else
                    float raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                #endif
                return ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.positionWS.xz;
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 ground = GroundAt(uv);
                // Depth of the bed below this point of the surface, and how far the view travels
                // through the water to reach it.
                float depth = max(0, input.positionWS.y - ground.y);
                float t = _Time.y;

                float3 color = lerp(_Shallow.rgb, _Mid.rgb, smoothstep(0.02, 0.4, depth));
                color = lerp(color, _Deep.rgb, smoothstep(0.35, 1.3, depth));
                color = lerp(color, _Abyss.rgb, smoothstep(1.4, 3.0, depth));
                float alpha = lerp(1 - _Clarity, 1, smoothstep(0.0, 0.85, depth));

                // Soft ripples in the open water: two drifting noise layers, brightness only.
                float n1 = RealmsNoise(p * 1.7 + float2(t * 0.15, t * 0.05));
                float n2 = RealmsNoise(p * 2.3 - float2(t * 0.07, t * 0.13) + 11.3);
                color *= 0.95 + 0.1 * n1 * n2;

                // Glints: short white dashes that come and go on the open water. A dash is 3.5
                // times longer than thick, so its edge is softened over a pixel across it, not
                // along it: a sharper edge would flicker on the thin dashes as the view moves.
                float px = max(length(fwidth(p)), 1e-4);
                float2 cell = floor(p / 0.3);
                float2 f = p / 0.3 - cell;
                float seed = RealmsHash(cell + 0.7);
                float blink = saturate((sin(t * (0.8 + seed) + seed * 40) - 0.75) * 4);
                float2 v = (f - (0.3 + 0.4 * float2(RealmsHash(cell * 1.3), RealmsHash(cell * 0.7 + 5)))) * 0.3;
                v.y *= 3.5;
                float glint = (1 - smoothstep(0.02 - px * 1.75, 0.02 + px * 1.75, length(v))) * blink;
                glint *= smoothstep(0.3, 0.9, depth) * smoothstep(0.2, 0.6, 0.02 / px);
                color = lerp(color, float3(1, 1, 1), glint * 0.5);

                // A crisp line of foam on the waterline.
                float wobble = (RealmsNoise(p * 4 + t * 0.2) - 0.5) * 0.04;
                float foam = 1 - smoothstep(0.02, 0.06, depth + wobble);
                color = lerp(color, _Foam.rgb, foam);
                alpha = max(alpha, foam * 0.95);

                // The sun: shadows of the coast and the props darken the water a little.
                float shadow = lerp(1, RealmsShadow(input.positionWS), 0.45);
                color *= lerp(float3(0.72, 0.8, 0.95), float3(1, 1, 1), shadow);

                color *= RealmsClouds(input.positionWS);
                color = RealmsTint(color, p, _Tint);
                float3 emission = 0;
                float coverage = 0;
                // The grid fades over the deep water, where nothing else breaks up the plain blue.
                color = RealmsLines(color, p, 1, lerp(1, 0.3, smoothstep(0.35, 1.3, depth)), RealmsHudShow(input.positionCS), emission, coverage);
                // Borders and rings stay opaque so they read over the clear shallows.
                alpha = max(alpha, coverage);
                return half4(color + emission, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
