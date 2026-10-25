// The sky as space: a deep dark with a faint nebula and stars, made from the
// view's direction alone (a skybox: RenderSettings.skybox).
Shader "FinishTheGame/Space"
{
    Properties
    {
        _Dark ("The dark", Color) = (0.008, 0.01, 0.025, 1)
        _Nebula ("The nebula", Color) = (0.12, 0.06, 0.2, 1)
        _Stars ("Stars a side of a cell's cube", Float) = 260
        _Share ("Cells with a star", Range(0, 1)) = 0.06
        _Panorama ("The sky (equirectangular; black: none)", 2D) = "black" {}
        _PanoramaShare ("The panorama's share", Range(0, 1)) = 0
        _Glow ("The twilight at the horizon", Color) = (0.95, 0.55, 0.35, 1)
        _GlowHeight ("Its height (sine of the elevation)", Float) = 0.18
        _Turn ("The panorama's turn, degrees", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Dark, _Nebula, _Glow;
                float _Stars, _Share, _PanoramaShare, _GlowHeight, _Turn;
            CBUFFER_END

            TEXTURE2D(_Panorama);
            SAMPLER(sampler_Panorama);

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.direction = v.positionOS.xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // One layer of stars: a star in some of the cells of a grid round the view.
            half Stars(float3 d, float cells, float share)
            {
                float3 p = d * cells;
                float3 cell = floor(p);
                float h = Hash(cell);
                if (h > share) return 0;
                float3 at = float3(Hash(cell + 1.7), Hash(cell + 3.1), Hash(cell + 5.3)) * 0.6 + 0.2;
                float r = length(frac(p) - at);
                return smoothstep(0.18, 0.0, r) * (0.4 + 0.6 * Hash(cell + 9.2));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                // A slow, faint band of nebula across the sky.
                float band = exp(-pow(dot(d, normalize(float3(0.3, 0.8, 0.5))) * 3.0, 2.0));
                float swirl = 0.5 + 0.5 * sin(d.x * 7.0 + sin(d.z * 5.0) * 2.0) * sin(d.y * 6.0 + d.x * 3.0);
                half3 colour = _Dark.rgb + _Nebula.rgb * band * swirl * 0.6;
                half star = Stars(d, _Stars, _Share) + 0.6 * Stars(d.zxy, _Stars * 2.3, _Share * 1.5);
                colour += star * lerp(half3(0.75, 0.82, 1.0), half3(1.0, 0.9, 0.75), Hash(floor(d * _Stars)));
                // The panorama, if any: the sky painted, its lower half (which
                // painters fill with land) not there: the horizon's colour below it.
                if (_PanoramaShare > 0)
                {
                    // Painted as the sky over a horizon (its bottom row): across
                    // half a turn, mirrored for the other half (no seam).
                    float az = frac(atan2(d.x, d.z) / (2.0 * PI) + 0.5 + _Turn / 360.0);
                    az = abs(az * 2.0 - 1.0);
                    float el = asin(clamp(d.y, -1.0, 1.0)) / PI;          // -0.5..0.5
                    float v = saturate(el * 2.0);
                    half3 painted = SAMPLE_TEXTURE2D_LOD(_Panorama, sampler_Panorama, float2(az, v), 0).rgb;
                    colour = lerp(colour, painted, _PanoramaShare);
                }
                // The twilight at the horizon, into which the land's haze fades.
                float glow = exp(-max(d.y, 0.0) / _GlowHeight);
                colour = lerp(colour, _Glow.rgb, glow * _Glow.a);
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
