// The rivers: ribbons just above their carved beds (RiverCourse.BuildMesh), clear turquoise at
// the banks and deeper in the middle, with light streaks drifting downstream, white water where
// the river drops, a thin line of foam along each bank and a strip of pale gravel and pebbles
// beside it. Faded out where a river runs into the sea. Drawn before the sea, which lies over
// the mouths; the kingdoms' tint and borders carry over the water as on the sea. Seen from far
// away, when the banks would be a pixel or two, the water spreads over them and the foam fades, so
// the rivers stay clear blue lines.
Shader "Realms/River"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.43, 0.7, 0.67, 1)
        _Deep ("Deep", Color) = (0.28, 0.49, 0.6, 1)
        _Foam ("Foam", Color) = (0.88, 0.9, 0.9, 1)
        _Gravel ("Gravel", Color) = (0.79, 0.76, 0.68, 1)
        _Pebble ("Pebbles", Color) = (0.6, 0.58, 0.55, 1)
        _Speed ("Flow speed", Float) = 0.45
        _Tint ("Territory tint", Range(0, 1)) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" "IgnoreProjector"="True" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
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
            #include "Assets/Realms/Shaders/RealmsCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _Foam, _Gravel, _Pebble;
                float _Speed, _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv0 : TEXCOORD1;
                float4 uv1 : TEXCOORD2;
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
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                return output;
            }

            // Pebbles: one round stone in some of the cells of a jittered grid.
            float Pebbles(float2 q)
            {
                float2 cell = floor(q), f = frac(q);
                float stones = 0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 c = cell + float2(x, y);
                    float h = RealmsHash(c + 3.1);
                    float2 centre = float2(x, y) + 0.2 + 0.6 * float2(RealmsHash(c * 1.7), RealmsHash(c * 0.9 + 4));
                    float r = 0.22 + 0.2 * h;
                    float2 v = f - centre;
                    v.x *= 1.3;
                    float px = max(fwidth(q.x), 1e-4);
                    stones = max(stones, step(0.45, h) * (1 - smoothstep(r - px, r + px, length(v))));
                }
                return stones;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.positionWS.xz;
                float across = input.uv0.x, along = input.uv0.y;
                float waterShare = input.uv1.x, fade = input.uv1.y, rapids = input.uv1.z, bank = input.uv1.w;
                float a = abs(across);
                float px = max(fwidth(across), 1e-4);
                float t = _Time.y;
                float far = smoothstep(0.012, 0.03, length(fwidth(p)));
                waterShare = lerp(waterShare, 0.97, far);

                // Water: deeper in the middle, streaks drifting downstream, white where it drops.
                float centre = saturate(1 - a / waterShare);
                float3 water = lerp(_Shallow.rgb, _Deep.rgb, smoothstep(0.1, 0.85, centre));
                float flow = along - t * _Speed;
                float wobble = RealmsNoise(float2(across * 2.5, flow * 1.3)) * 4.0;
                float streak = sin(flow * 9.0 + wobble + across * 1.5);
                float streaks = smoothstep(0.86, 0.97, streak) * RealmsNoise(float2(across * 3 + 7, flow * 0.8));
                float white = saturate(streaks * 0.8 + rapids * smoothstep(0.35, 0.75, RealmsNoise(float2(across * 4, flow * 3.5)) + rapids * 0.3)) * (1 - far);
                water = lerp(water, _Foam.rgb, white);
                // A thin line of foam where the water meets the bank.
                float line_ = smoothstep(waterShare - 0.16 - px, waterShare - 0.04, a);
                water = lerp(water, _Foam.rgb, line_ * 0.85 * (1 - far));

                // The bank: pale gravel strewn with pebbles, fading out at its outer edge.
                float2 q = float2(across * 4.0, along * 22.0);
                float3 gravel = lerp(_Gravel.rgb, _Pebble.rgb, Pebbles(q) * 0.9);
                gravel *= 0.92 + 0.16 * RealmsNoise(p * 18);
                float inWater = 1 - smoothstep(waterShare - px, waterShare + px, a);
                float3 color = lerp(gravel, water, inWater);
                float bankAlpha = bank * (1 - smoothstep(lerp(waterShare, 1, 0.55), 1, a));
                float alpha = lerp(bankAlpha, 0.92, inWater) * fade;

                // The sun and the clouds, as on the sea.
                float shadow = lerp(1, RealmsShadow(input.positionWS), 0.45);
                color *= lerp(float3(0.72, 0.8, 0.95), float3(1, 1, 1), shadow);
                color *= RealmsClouds(input.positionWS);
                color = RealmsTint(color, p, _Tint);
                float3 emission = 0;
                float coverage = 0;
                color = RealmsLines(color, p, 1, 1, RealmsHudShow(input.positionCS), emission, coverage);
                alpha = max(alpha, coverage * fade);
                return half4(color + emission, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
