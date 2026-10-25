// Rays of white light out of a spirit (Villager.cs), on a quad turned to the
// camera: thin rays at every angle, slowly turning and breathing, a soft
// white glow at their heart; some of the rays, as light through a prism,
// split into a rainbow across their width (each colour a ray of its own, a
// little turned from the others). Light added over the picture; _Fade
// brings it in and out.
Shader "FinishTheGame/Rays"
{
    Properties
    {
        [HDR] _Tint ("Light", Color) = (1.45, 1.41, 1.36, 1)
        _Count ("Rays", Float) = 34
        _Spread ("A rainbow's spread, radians", Float) = 0.035
        _Fade ("Fade (0-1)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+20" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Count, _Spread, _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }

            float Hash(float x) { return frac(sin(x * 127.1) * 43758.5453); }

            // The rays at an angle: each ray's own width, length and flicker.
            float Rays(float angle, float r)
            {
                float t = _Time.y;
                float slot = (angle / (2.0 * PI) + 0.5) * _Count + t * 0.15;
                float id = floor(slot), f = frac(slot) - 0.5;
                float h = Hash(id + 3.0);
                float width = lerp(0.04, 0.16, h);
                float length = lerp(0.45, 1.0, Hash(id + 17.0)) * (0.8 + 0.2 * sin(t * (0.6 + h) + h * 40.0));
                float ray = exp(-f * f / (width * width * 0.5));
                return ray * saturate(1.0 - r / length) * step(0.25, h);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv);
                float angle = atan2(i.uv.y, i.uv.x);
                // Some rays (by their own chance) split into colours across their width.
                float slot = floor((angle / (2.0 * PI) + 0.5) * _Count + _Time.y * 0.15);
                float prism = step(0.7, Hash(slot + 51.0)) * _Spread;
                half3 light = half3(Rays(angle - prism, r), Rays(angle, r), Rays(angle + prism, r));
                // A soft white heart.
                light += exp(-r * r * 30.0) * 0.6;
                return half4(_Tint.rgb * light * _Fade * saturate(1.0 - r), 0);
            }
            ENDHLSL
        }
    }
}
