// Dust puffs (Puffs.cs): a ring of soft clouds round the foot of a building that has just risen
// out of the ground (the visual plan of 2026-11-11, item 2). Every corner of a puff sits at the
// ring's centre on the ground; TEXCOORD0 is the corner (-1..1), TEXCOORD1 the puff's birth time
// (x, on _RealmsPuffNow's clock), its random (y), size (z) and direction round the ring (w,
// radians), TEXCOORD2 how far out it goes (x) and how long it lives (y). The puff's age comes from
// the time, so the mesh changes only when a ring is made: the puff starts near the walls, rolls
// outward and a little up, slowing, grows and fades. Before its birth and after its life it folds
// into a point and draws nothing. The colour is a warm dust, or the vertex colour where its alpha
// is set (a splash in the shallows under a hero); the drifting cloud shadows pass over it as over
// the land.
Shader "Realms/Puff"
{
    Properties
    {
        _Dust ("Dust", Color) = (0.74, 0.68, 0.57, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Realms/Shaders/RealmsCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Dust;
                float _Opacity;
            CBUFFER_END

            // The game's clock (Time.time), set by Puffs every frame.
            float _RealmsPuffNow;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                float4 puff : TEXCOORD1;
                float2 path : TEXCOORD2;
                half4 colour : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 corner : TEXCOORD1;
                float2 look : TEXCOORD2; // opacity, the puff's own random
                float shade : TEXCOORD3; // lighter towards the sun
                half4 colour : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 centre = TransformObjectToWorld(input.positionOS.xyz);
                float age = (_RealmsPuffNow - input.puff.x) / max(input.path.y, 0.01);
                float alive = step(0, age) * step(age, 1);
                age = saturate(age);
                float own = input.puff.y;
                float s, c;
                sincos(input.puff.w, s, c);
                // Out from near the walls, fast at first and slowing; up a little as it rolls.
                float away = input.path.x * lerp(0.55, 1.25, 1 - pow(1 - age, 2.2));
                float3 at = centre + float3(c, 0, s) * away + float3(0, 0.012 + 0.06 * age * (0.6 + 0.8 * own), 0);
                float size = input.puff.z * lerp(0.55, 1.35, sqrt(age)) * alive;
                float turn = own * 6.2831853 + age * (own > 0.5 ? 1.5 : -1.5), ts, tc;
                sincos(turn, ts, tc);
                float2 corner = input.corner;
                float2 turned = float2(corner.x * tc - corner.y * ts, corner.x * ts + corner.y * tc);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                output.positionWS = at + (right * turned.x + up * turned.y) * size;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.corner = corner;
                output.colour = input.colour;
                output.look = float2(smoothstep(0, 0.12, age) * pow(1 - age, 1.6) * alive, own);
                float3 sun = GetMainLight().direction;
                float2 onScreen = float2(dot(sun, right), dot(sun, up));
                float2 sunTurned = float2(onScreen.x * tc + onScreen.y * ts, -onScreen.x * ts + onScreen.y * tc);
                output.shade = dot(corner, normalize(sunTurned + 1e-4)) * saturate(length(sunTurned) * 2);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 corner = input.corner;
                float ragged = RealmsNoise(corner * 2.1 + input.look.y * 23) - 0.5;
                float shape = smoothstep(1, 0.25, length(corner) + ragged * 0.55);
                float alpha = shape * input.look.x * _Opacity;
                clip(alpha - 0.003);
                float3 albedo = lerp(_Dust.rgb, input.colour.rgb, input.colour.a) * (0.84 + 0.2 * saturate(0.5 + 0.5 * input.shade));
                return half4(albedo * RealmsClouds(input.positionWS), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
