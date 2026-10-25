// Plain lit colour (the mesh's vertex colour times _BaseColor), the main
// light with its shadows, ambient and fog, seen through the band along the
// story's route (Band.hlsl): out of it the world fades into the fog's colour
// and is gone, its shadows and depth too. A road (_RoadEdge.w): its mesh
// wider than the road, the distance across it in the second UVs; its edge
// ragged by noise (the width wandering slowly, the verge's bites, the crumbs)
// and blended into the ground under it.
Shader "FinishTheGame/Band Lit"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _MainTex ("Texture (white: the vertex colour alone)", 2D) = "white" {}
        _RoadEdge ("Road: half width, its wander, the fade, m; on", Vector) = (0, 0, 0, 0)
        [HideInInspector] _SrcBlend ("", Float) = 1
        [HideInInspector] _DstBlend ("", Float) = 0
        [HideInInspector] _ZWrite ("", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Band.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _MainTex_ST;
            float4 _RoadEdge;
        CBUFFER_END
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float ValueNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
        }
        // The road's cover at a place, d metres off its middle: 1 on it, 0 off.
        half RoadCover(float2 place, float d)
        {
            float wander = ValueNoise(place / 30.0) - 0.5;
            float bites = ValueNoise(place / 5.0 + 17.0) - 0.5;
            float crumbs = ValueNoise(place / 1.2 + 41.0) - 0.5;
            float edge = _RoadEdge.x + _RoadEdge.y * (1.2 * wander + 1.0 * bites + 0.45 * crumbs);
            return 1.0 - smoothstep(edge - _RoadEdge.z, edge, abs(d));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fog : TEXCOORD2;
                float3 placeWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                float across : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.placeWS = o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color * _BaseColor;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.across = v.uv1.x;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half keep = BandKeep(i.placeWS);
                clip(keep - 0.02);
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half occlusion = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    occlusion = ao.indirectAmbientOcclusion;
                #endif
                half diffuse = saturate(dot(n, light.direction)) * light.shadowAttenuation;
                half3 albedo = i.color.rgb * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                half3 colour = albedo * (SampleSH(n) * occlusion + light.color * diffuse);
                colour = MixFog(colour, i.fog);
                half3 edge = unity_FogColor.rgb;
                half cover = _RoadEdge.w > 0.5 ? RoadCover(i.placeWS.xz, i.across) : 1;
                return half4(lerp(edge, colour, keep), cover);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;   // where it is (the band keeps by it)
            };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 shown = o.positionWS;
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 toLight = normalize(_LightPosition - shown);
                #else
                    float3 toLight = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(shown, normalWS, toLight));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(BandKeep(i.positionWS) - 0.5);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 shown = o.positionWS, normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(shown);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                clip(BandKeep(i.positionWS) - 0.02);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                float3 shown = o.positionWS;
                o.positionCS = TransformWorldToHClip(shown);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(BandKeep(i.positionWS) - 0.02);
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octahedral = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
