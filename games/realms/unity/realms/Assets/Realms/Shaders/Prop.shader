// Props and models: vertex colours (the generated low-poly props) or a base texture (the studio
// models, some with a normal map baked from a denser surface; their loudest colours are calmed to
// the map's palette), lit by the display-colour model with a soft rim, plus a light along the
// outline where _RimLight asks for one (the battle's figures). The models have no tangents:
// the normal map's frame comes from the screen-space derivatives of the position and the UVs. Stone
// props (vertex alpha below 1, see MeshKit) take the rock texture's pattern from the side and scree
// from above, and snow where the alpha rises towards 1, with a ragged edge that climbs higher on
// steep faces. With _Sway on, plants bend in the wind by their second UVs (how far, their phase;
// see RealmsSway), in every pass, so their shadows move with them. With _Cutaway on, a plant
// standing in front of a town or hero screen's subject goes whole with its shadow, by where it
// is rooted (third UVs, ModelBatch). _ZTest lets a copy of the material draw over everything on
// the land (HeroView: the chosen hero's ring and way).
Shader "Realms/Prop"
{
    Properties
    {
        _BaseMap ("Base map", 2D) = "white" {}
        _BaseColor ("Base colour", Color) = (1, 1, 1, 1)
        _NormalMap ("Normal map (tangent space, +Y up)", 2D) = "bump" {}
        _NormalScale ("Normal strength", Range(0, 2)) = 1
        _UseNormalMap ("Use normal map", Range(0, 1)) = 0
        _NormalSRGB ("Normal map sampled as sRGB", Range(0, 1)) = 0
        _UseVertexColor ("Use vertex colour", Range(0, 1)) = 1
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Rim ("Rim", Range(0, 1)) = 0.25
        _RimLight ("Rim light, added at the outline", Color) = (0, 0, 0, 0)
        _Ground ("Ground layers", 2DArray) = "" {}
        _GroundDetail ("Rock texture", Range(0, 1)) = 0.9
        _RockScale ("Rock texture tile, world units", Float) = 0.7
        _Snow ("Snow", Color) = (0.91, 0.92, 0.93, 1)
        _Sway ("Sway in the wind (second UVs)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test (the hero's way and ring: always)", Float) = 4
        _Cutaway ("Dissolves in front of a screen's subject (plants)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Realms/Shaders/RealmsCommon.hlsl"
        #include "Assets/Realms/Shaders/RealmsGround.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _Snow, _RimLight;
            float _UseVertexColor, _Saturation, _Rim, _GroundDetail, _RockScale;
            float _NormalScale, _UseNormalMap, _NormalSRGB, _Sway, _Cutaway;
        CBUFFER_END
        float3 _LightDirection, _LightPosition;

        // The town and hero screens' cutaway (RealmsCut): the plants standing in front of the
        // screen's subject go with their shadows. A plant cut away whole is not drawn at all;
        // one cut in part (rooted on the band's narrow edge, or while the screen fades in and
        // out) thins out by an ordered 4x4 dither on the pixel instead of being sliced.
        static const uint CutDither[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        float CutOf(float2 root)
        {
            float cut = 0;
            [branch]
            if (_Cutaway > 0.5 && _RealmsCutEye.z > 0)
                cut = RealmsCut(root);
            return cut;
        }

        // Every corner of a plant cut away whole beyond the clip space's right side: none of its
        // triangles is drawn.
        float4 Culled(float4 positionCS, float cut)
        {
            return cut > 0.999 ? float4(2, 2, 2, 1) : positionCS;
        }

        void Cutaway(float cut, float4 positionCS)
        {
            [branch]
            if (cut <= 0)
                return;
            uint2 q = uint2(positionCS.xy) & 3;
            clip((CutDither[q.y * 4 + q.x] + 0.5) / 16 - cut);
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            float2 sway : TEXCOORD1;
            float2 root : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float4 color : TEXCOORD2;
            float2 uv : TEXCOORD3;
            float cut : TEXCOORD4;
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
            output.positionWS += RealmsSway(output.positionWS, input.sway * _Sway);
            output.cut = CutOf(input.root);
            output.positionCS = Culled(TransformWorldToHClip(output.positionWS), output.cut);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.color = lerp(float4(1, 1, 1, 1), input.color, _UseVertexColor);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            return output;
        }

        Varyings ShadowVert(Attributes input)
        {
            Varyings output = Vert(input);
            float3 lightDirection = _LightDirection;
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                lightDirection = normalize(_LightPosition - output.positionWS);
            #endif
            output.positionCS = Culled(ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, lightDirection))), output.cut);
            return output;
        }

        // The normal map's tangent-space normal applied with a frame from the derivatives of the
        // position and the UVs (Schüler, "Normal mapping without precomputed tangents").
        float3 Perturb(float3 n, float3 dpx, float3 dpy, float2 duvx, float2 duvy, float3 t)
        {
            float3 dpyPerp = cross(dpy, n);
            float3 dpxPerp = cross(n, dpx);
            float3 tangent = dpyPerp * duvx.x + dpxPerp * duvy.x;
            float3 bitangent = dpyPerp * duvx.y + dpxPerp * duvy.y;
            float scale = rsqrt(max(1e-12, max(dot(tangent, tangent), dot(bitangent, bitangent))));
            return normalize((tangent * t.x + bitangent * t.y) * scale + n * t.z);
        }

        // The map is muted and only the kingdoms' colours are loud, so a model's own paint keeps to
        // that: colours above a saturation knee keep a third of the excess and lose a little
        // brightness with it (golden thatch turns to straw). Vertex colours, the stones' tints and
        // the kingdoms' colours on the generated props, are left as they are.
        float3 Calm(float3 paint)
        {
            const float Knee = 0.55, Keep = 0.35, Dim = 0.18;
            float3 c = LinearToSRGB(paint);
            float hi = max(c.r, max(c.g, c.b)), lo = min(c.r, min(c.g, c.b));
            float s = (hi - lo) / max(hi, 1e-4);
            if (s <= Knee)
                return paint;
            float excess = (s - Knee) / (1 - Knee);
            c = (hi - (hi - c) * ((Knee + (s - Knee) * Keep) / s)) * (1 - Dim * excess);
            return SRGBToLinear(c);
        }

        half4 Forward(Varyings input, bool front : SV_IsFrontFace) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            Cutaway(input.cut, input.positionCS);
            float3 n = normalize(input.normalWS) * (front ? 1 : -1);
            float3 dpx = ddx(input.positionWS), dpy = ddy(input.positionWS);
            float2 duvx = ddx(input.uv), duvy = ddy(input.uv);
            [branch]
            if (_UseNormalMap > 0.5)
            {
                float3 packed = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv).rgb;
                packed = _NormalSRGB > 0.5 ? LinearToSRGB(packed) : packed;
                float3 t = packed * 2 - 1;
                t.xy *= _NormalScale;
                n = Perturb(n, dpx, dpy, duvx, duvy, normalize(t));
            }
            float3 albedo = Calm(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb) * input.color.rgb;
            float3 q = input.positionWS / _RockScale;
            float3 qx = ddx(q), qy = ddy(q);
            [branch]
            if (input.color.a < 0.995 && _GroundDetail > 0)
            {
                // Stone: rock from the side, scree on top; snow over it towards the summit.
                float3 rock = GroundTriplanar(q, qx, qy, n, GROUND_ROCK, GROUND_SCREE).rgb;
                albedo *= lerp(1, rock, _GroundDetail);
                float rise = (input.color.a - 0.02) / 0.97;
                float ragged = RealmsNoise(input.positionWS.xz * 7 + input.positionWS.y * 5) - 0.5;
                float snow = rise > 0 ? smoothstep(0.22, 0.3, rise + ragged * 0.35 + (n.y - 0.45) * 0.3) : 0;
                [branch]
                if (snow > 0)
                {
                    float3 flake = GroundTriplanar(q * 0.8, qx * 0.8, qy * 0.8, n, GROUND_SNOW, GROUND_SNOW).rgb;
                    albedo = lerp(albedo, _Snow.rgb * lerp(1, flake, _GroundDetail * 0.6), snow);
                }
            }
            float grey = dot(albedo, float3(0.2126, 0.7152, 0.0722));
            albedo = max(0, lerp(grey.xxx, albedo, _Saturation));
            float3 color = RealmsLit(albedo, n, input.positionWS, _Rim);
            // A light along the outline whatever the paint (the battle sets it, so a dark figure
            // stands out from the ground).
            float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
            color += _RimLight.rgb * pow(saturate(1 - dot(n, view)), 3);
            return half4(color, 1);
        }

        half4 Depth(Varyings input) : SV_Target
        {
            Cutaway(input.cut, input.positionCS);
            return 0;
        }

        half4 DepthNormal(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Cutaway(input.cut, input.positionCS);
            float3 n = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0);
            #else
                return half4(n, 0);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            ZTest [_ZTest]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Forward
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthNormal
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack Off
}
