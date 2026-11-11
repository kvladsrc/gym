// Chimney smoke (SmokePlumes): every corner of a puff sits at its chimney's top, TEXCOORD0 is the
// corner (-1..1) and TEXCOORD1 the puff's place in the cycle (x, 0..1), the chimney's seed (y)
// and its size (z). The puff's age comes from the time: it rises quickly and then slows, the
// wind (_RealmsWind) carries it further and further, it wobbles, turns, grows and fades, and
// starts again at the chimney. Fresh smoke is grey, older smoke paler; the side towards the sun
// is lighter, and the drifting cloud shadows pass over it as over the land. Thin and small: at
// 0.7 opacity and twice the size the smoke stood in front of the roofs as dense whitish columns,
// like steam (review of 2026-11-10).
Shader "Realms/Smoke"
{
    Properties
    {
        _Fresh ("Fresh smoke", Color) = (0.56, 0.56, 0.58, 1)
        _Old ("Old smoke", Color) = (0.8, 0.79, 0.77, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.35
        _Life ("Seconds a puff lives", Float) = 7
        _Rise ("Height a puff rises", Float) = 0.42
        _Drift ("Distance the wind carries a puff", Float) = 0.32
        _Size ("Puff half-width, young and old", Vector) = (0.012, 0.055, 0, 0)
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
                half4 _Fresh, _Old;
                float _Opacity, _Life, _Rise, _Drift;
                float4 _Size;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                float4 puff : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 corner : TEXCOORD1;
                float4 look : TEXCOORD2; // age, opacity, the puff's own random, the lit side's lightening
                float2 sun : TEXCOORD3;  // the sun's direction on screen
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 chimney = TransformObjectToWorld(input.positionOS.xyz);
                float seed = input.puff.y;
                float life = _Life * (0.85 + 0.3 * seed);
                float cycles = _Time.y / life + input.puff.x + seed * 7.31;
                float age = frac(cycles);
                // Each round of a puff gets its own shape and path.
                float own = RealmsHash(float2(floor(cycles), seed * 91.7 + input.puff.x * 13.1));
                float2 toward = _RealmsWind.xy, side = float2(-toward.y, toward.x);
                float rise = _Rise * (0.8 + 0.4 * own) * (1 - pow(1 - age, 1.8));
                float drift = _Drift * _RealmsWind.z * pow(age, 1.3) * (0.7 + 0.6 * RealmsGust(chimney.xz));
                float wobble = (sin(_Time.y * 0.9 + seed * 40 + age * 5) * 0.025 + (own - 0.5) * 0.06) * age;
                float3 centre = chimney + float3(0, rise, 0) + float3(toward.x, 0, toward.y) * drift
                    + float3(side.x, 0, side.y) * wobble;
                float size = input.puff.z * lerp(_Size.x, _Size.y, pow(age, 0.6)) * (0.8 + 0.4 * own);
                float turn = own * 6.2831853 + age * (own > 0.5 ? 1.2 : -1.2), s, c;
                sincos(turn, s, c);
                float2 corner = input.corner;
                float2 turned = float2(corner.x * c - corner.y * s, corner.x * s + corner.y * c);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                output.positionWS = centre + (right * turned.x + up * turned.y) * size;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.corner = corner;
                float fade = smoothstep(0, 0.08, age) * pow(1 - age, 1.5);
                output.look = float4(age, fade, own, 0);
                // The sun's direction on screen, in the puff's own turned frame.
                float3 sun = GetMainLight().direction;
                float2 onScreen = float2(dot(sun, right), dot(sun, up));
                output.sun = float2(onScreen.x * c + onScreen.y * s, -onScreen.x * s + onScreen.y * c);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 corner = input.corner;
                float d = length(corner);
                float ragged = RealmsNoise(corner * 2.3 + input.look.z * 17) - 0.5;
                float shape = smoothstep(1, 0.3, d + ragged * 0.6);
                float alpha = shape * input.look.y * _Opacity;
                clip(alpha - 0.003);
                float3 albedo = lerp(_Fresh.rgb, _Old.rgb, saturate(input.look.x * 1.7));
                float towards = dot(corner, normalize(input.sun + 1e-4)) * saturate(length(input.sun) * 2);
                albedo *= 0.86 + 0.18 * saturate(0.5 + 0.5 * towards);
                return half4(albedo * RealmsClouds(input.positionWS), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
