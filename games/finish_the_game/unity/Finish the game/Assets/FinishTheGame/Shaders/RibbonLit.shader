// Plain lit colour (the mesh's vertex colour times _BaseColor), the main
// light with its shadows, ambient and fog; every pass bends the world onto
// the ribbon (Ribbon.hlsl) the same way, the shadows' and the depth's too;
// the ribbon ahead casts no shadow (it is not where the world near is).
Shader "FinishTheGame/Ribbon Lit"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Ribbon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float4 story : TEXCOORD1;   // (s, t, 1, heading) on the story's curve: Ribbon.hlsl
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        /// False: the vertex is gone (Ribbon.hlsl), its clip position Gone().
        bool Bent(Attributes v, out float3 positionWS, out float3 normalWS)
        {
            positionWS = TransformObjectToWorld(v.positionOS.xyz);
            normalWS = TransformObjectToWorldNormal(v.normalOS);
            return Ribbonize(positionWS, normalWS, v.story);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

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
            };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                bool kept = Bent(v, o.positionWS, o.normalWS);
                o.positionCS = kept ? TransformWorldToHClip(o.positionWS) : Gone();
                o.color = v.color * _BaseColor;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half occlusion = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    occlusion = ao.indirectAmbientOcclusion;
                #endif
                half diffuse = saturate(dot(n, light.direction)) * light.shadowAttenuation;
                half3 colour = i.color.rgb * (SampleSH(n) * occlusion + light.color * diffuse);
                return half4(MixFog(colour, i.fog), 1);
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

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS, normalWS;
                if (!Bent(v, positionWS, normalWS) || RibbonAhead(v.story)) return Gone();
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 toLight = normalize(_LightPosition - positionWS);
                #else
                    float3 toLight = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, toLight));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 frag() : SV_Target { return 0; }
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

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS, normalWS;
                if (!Bent(v, positionWS, normalWS)) return Gone();
                return TransformWorldToHClip(positionWS);
            }

            half frag() : SV_Target { return 0; }
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
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 positionWS;
                bool kept = Bent(v, positionWS, o.normalWS);
                o.positionCS = kept ? TransformWorldToHClip(positionWS) : Gone();
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
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
