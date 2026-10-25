// A spirit in a body (SpiritQuest.cs: Hilda's, on the road, in her finest
// clothes), as a white radiance, a diamond's: its own painted look kept in
// its tones only, pearl-white and lit from within, not by the world (no
// shadow, no fog):
// - its many small facets catch the sun and flash, each flash split into a
//   colour of the spectrum as a diamond's fire;
// - its rim is white light;
// - it comes into being from the ground up (_Fade 0 to 1), the line where it
//   becomes ragged with noise, white-hot, fringed with colours; and goes so;
// - its hem never quite is: it breaks into glittering motes.
// _Frame tells where its feet are and how tall it is (Villager.cs). Its
// nearest faces go first into the depth (the first pass), so that it is seen
// through as one body, not an arm through the chest.
Shader "FinishTheGame/Ghost"
{
    Properties
    {
        _MainTex ("Look", 2D) = "white" {}
        [HDR] _White ("Its white", Color) = (1.45, 1.41, 1.36, 1)
        _Kept ("Its own colours kept (0-1)", Range(0, 1)) = 0.55
        [HDR] _Rim ("Rim", Color) = (2.2, 2.1, 2.0, 1)
        _Fire ("Facets' fire", Float) = 5
        _Facets ("Facets a metre", Float) = 18
        [HDR] _Edge ("Edge (coming into being)", Color) = (5, 4.8, 4.5, 1)
        _Solid ("How solid it is", Range(0, 1)) = 0.8
        _Fade ("Come into being (0-1)", Range(0, 1)) = 1
        _Frame ("Feet's height, its height, m", Vector) = (0, 1.7, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _White, _Rim, _Edge;
            half _Kept, _Fire, _Facets;
            half _Solid, _Fade;
            float4 _Frame;
        CBUFFER_END
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
        };

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float3 Hash3(float3 p) { return float3(Hash(p), Hash(p + 31.7), Hash(p + 73.1)); }

        float Noise(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + 1), f.x), f.y), f.z);
        }

        // A colour of the spectrum, x round it (0-1).
        half3 Spectrum(float x) { return saturate(abs(frac(x + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0); }

        // Its height's share at a place, 0 at its feet, 1 at its top.
        float Up(float3 positionWS) { return saturate((positionWS.y - _Frame.x) / max(_Frame.y, 0.1)); }

        // How far into being a place is: > 0 there, < 0 not yet; the line ragged.
        float Being(float3 positionWS)
        {
            float n = Noise(positionWS * 5.0) * 0.6 + Noise(positionWS * 13.0) * 0.4;
            return (_Fade * 1.35 - 0.12) - (Up(positionWS) + 0.22 * (n - 0.5));
        }

        // Its hem breaking into motes: 1 where it is whole, 0 where it is not;
        // the motes glitter in place, coming and going.
        float Hem(float3 positionWS)
        {
            float up = Up(positionWS) + 0.06 * (Noise(positionWS * 7.0 + float3(0, -_Time.y * 0.5, 0)) - 0.5);
            float whole = smoothstep(0.02, 0.15, up);
            float glitter = Hash(floor(positionWS * 40.0) + floor(_Time.y * 6.0));
            return max(whole, step(0.82, glitter) * smoothstep(-0.05, 0.05, up));
        }

        Varyings vert(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "Depth"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Varyings i) : SV_Target
            {
                clip(Being(i.positionWS));
                clip(Hem(i.positionWS) - 0.5);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Light"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Varyings i) : SV_Target
            {
                float being = Being(i.positionWS);
                clip(being);
                float hem = Hem(i.positionWS);
                clip(hem - 0.01);
                float up = Up(i.positionWS);
                float3 n = normalize(i.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                float facing = saturate(abs(dot(n, view)));

                // Pearl-white, its own look kept in its tones (and a little of its colours).
                half3 look = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                half tone = dot(look, half3(0.3, 0.55, 0.15));
                half3 colour = _White.rgb * lerp(tone.xxx * 0.75 + 0.3, look * 1.15 + 0.08, _Kept);

                // The rim: white light.
                half rim = pow(1.0 - facing, 3.0);
                colour += _Rim.rgb * rim;

                // Its facets: each a little plane of its own, flashing where it
                // turns the sun (or a slow wandering light) to the eye, the flash
                // split into a colour of the spectrum.
                float3 cell = floor(i.positionWS * _Facets);
                float3 tilt = Hash3(cell) - 0.5;
                float3 facet = normalize(n + tilt * 0.9);
                float3 mirrored = reflect(-view, facet);
                float3 sun = normalize(_MainLightPosition.xyz);
                float3 wander = normalize(float3(sin(_Time.y * 0.7), 0.6, cos(_Time.y * 0.53)));
                float glint = pow(saturate(dot(mirrored, sun)), 120.0) + 0.8 * pow(saturate(dot(mirrored, wander)), 160.0)
                    + 0.7 * pow(saturate(dot(mirrored, view)), 200.0) * step(0.85, Hash(cell + floor(_Time.y * 3.0)));
                float seed = Hash(cell + 11.0);
                half3 fire = lerp(half3(1, 1, 1), Spectrum(seed + dot(mirrored, sun) * 2.0), 0.75);
                colour += fire * glint * _Fire;

                // The line where it comes into being: white-hot, fringed with colours.
                half edge = 1.0 - saturate(being / 0.08);
                colour += _Edge.rgb * edge * edge * lerp(half3(1, 1, 1), Spectrum(being * 9.0 + up), 0.5 * edge);
                // A mote of the hem: a spark of its light.
                colour = lerp(_Edge.rgb * 0.6, colour, saturate(hem * 1.5 - 0.5));
                return half4(colour, saturate(_Solid + 0.5 * rim + edge + glint) * hem);
            }
            ENDHLSL
        }
    }
}
