// Grass blades (GrassField: clumps drawn instanced around the camera).
// Where it grows comes from the level's ground masks at the clump's root
// (A = trail, wet, pine; B = autumn, rock, moss) and a slow noise for
// clearings: elsewhere the clump shrinks to nothing. Its colour is the
// ground's baked colour there, darker at the root, greener to the tip, so
// it grows out of the ground it stands on. The ground's atlas UV is planar
// in world X/Z: _GroundUV = (u per x, u at 0, v per z, v at 0).
Shader "Soulslike/Grass"
{
    Properties
    {
        _Atlas ("Ground's baked colour", 2D) = "grey" {}
        _MaskA ("Mask: trail, wet, pine", 2D) = "black" {}
        _MaskB ("Mask: autumn, rock, moss", 2D) = "black" {}
        _GroundUV ("World X/Z to the ground's UV", Vector) = (0.0033, 0.5, 0.002, 0.5)
        _Tip ("Tip colour", Color) = (0.42, 0.48, 0.2, 1)
        _Radius ("Drawn this far, m", Float) = 40
        _Wind ("Sway, m", Float) = 0.07
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_Atlas); SAMPLER(sampler_Atlas);
        TEXTURE2D(_MaskA); SAMPLER(sampler_MaskA);
        TEXTURE2D(_MaskB);

        CBUFFER_START(UnityPerMaterial)
            float4 _Atlas_ST;
            float4 _GroundUV;
            float4 _Tip;
            float _Radius;
            float _Wind;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 color : TEXCOORD1;
            half fogFactor : TEXCOORD2;
        };

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

        float Noise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), u.x), u.y);
        }

        Varyings Vertex(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings output = (Varyings)0;
            float3 root = TransformObjectToWorld(float3(0, 0, 0));
            float2 ground = float2(_GroundUV.x * root.x + _GroundUV.y, _GroundUV.z * root.z + _GroundUV.w);
            half3 a = SAMPLE_TEXTURE2D_LOD(_MaskA, sampler_MaskA, ground, 0).rgb;
            half3 b = SAMPLE_TEXTURE2D_LOD(_MaskB, sampler_MaskA, ground, 0).rgb;

            // None on the trails, rocks and wet ground; thinner on needles and leaves.
            float grows = saturate(1 - a.r * 2.2) * saturate(1 - b.g * 1.8) * saturate(1 - a.g * 1.4)
                * lerp(1, 0.4, saturate(max(a.b, b.r) * 1.5));
            grows *= smoothstep(0.3, 0.6, Noise(root.xz * 0.09) + 0.15); // clearings
            float fade = saturate((_Radius - distance(root, GetCameraPositionWS())) / 8.0);
            float size = grows > 0.2 ? saturate(grows * 1.3) * fade : 0;

            float t = input.uv.x;
            float3 world = TransformObjectToWorld(input.positionOS.xyz * size);
            float gust = sin(_Time.y * 1.6 + root.x * 0.35 + root.z * 0.22) + 0.5 * sin(_Time.y * 2.7 + root.z * 0.5);
            world.xz += float2(0.8, 0.6) * gust * _Wind * t * t * size;

            half3 soil = SAMPLE_TEXTURE2D_LOD(_Atlas, sampler_Atlas, ground, 0).rgb;
            half3 tip = lerp(soil * 1.25, _Tip.rgb, 0.45) * (0.85 + 0.3 * input.uv.y);
            output.color = lerp(soil * 0.6, tip, t);
            output.positionWS = world;
            output.positionCS = TransformWorldToHClip(world);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 Fragment(Varyings input) : SV_Target
            {
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = float3(0, 1, 0);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = input.color;
                surface.smoothness = 0.05;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 lit = UniversalFragmentPBR(inputData, surface);
                lit.rgb = MixFog(lit.rgb, inputData.fogCoord);
                return lit;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Depth
            #pragma multi_compile_instancing
            half Depth(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
