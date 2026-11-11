// The kingdoms' flags on the map: cloth from FlagCloth, painted from the global texture array
// _RealmsFlags (a slice per kingdom, FlagArt) and waving in the wind. TEXCOORD0 holds the distance
// from the pole (x, 0 at the pole), the flag's v (y), the kingdom's slice (z) and the wave's
// height in object units (w); the tangent is the direction away from the pole with the flag's
// length in w. Both sides are drawn, the back one mirrored as on a real flag.
Shader "Realms/Flag"
{
    Properties
    {
        _Wave ("Wave", Range(0, 2)) = 1
        _WaveSpeed ("Wave speed", Range(0, 8)) = 3.2
        _Rim ("Rim", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Realms/Shaders/RealmsCommon.hlsl"

        TEXTURE2D_ARRAY(_RealmsFlags);
        SAMPLER(sampler_RealmsFlags);
        CBUFFER_START(UnityPerMaterial)
            float _Wave, _WaveSpeed, _Rim;
        CBUFFER_END
        float3 _LightDirection, _LightPosition;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float4 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 uv : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        // Waves running from the pole to the free end, growing away from the pole; the normal
        // follows the slope of the wave along the flag.
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float u = input.uv.x;
            float3 rest = TransformObjectToWorld(input.positionOS.xyz);
            float travel = 6.5;
            float phase = _Time.y * _WaveSpeed - u * travel + dot(rest.xz, float2(1.7, 2.3));
            float s = sin(phase) + 0.35 * sin(phase * 1.9 + 1.3);
            float ds = cos(phase) + 0.35 * 1.9 * cos(phase * 1.9 + 1.3);
            float amp = input.uv.w * _Wave;
            float slope = amp * (s - travel * u * ds) / max(input.tangentOS.w, 1e-4);
            float3 normalOS = normalize(input.normalOS - input.tangentOS.xyz * slope);
            float3 positionOS = input.positionOS.xyz + input.normalOS * (amp * u * s);
            output.positionWS = TransformObjectToWorld(positionOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(normalOS);
            output.uv = input.uv.xyz;
            return output;
        }

        Varyings ShadowVert(Attributes input)
        {
            Varyings output = Vert(input);
            float3 lightDirection = _LightDirection;
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                lightDirection = normalize(_LightPosition - output.positionWS);
            #endif
            output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, lightDirection)));
            return output;
        }

        half4 Forward(Varyings input, bool front : SV_IsFrontFace) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float3 n = normalize(input.normalWS) * (front ? 1 : -1);
            float3 albedo = SAMPLE_TEXTURE2D_ARRAY(_RealmsFlags, sampler_RealmsFlags, input.uv.xy, round(input.uv.z)).rgb;
            float3 color = RealmsLit(albedo, n, input.positionWS, _Rim);
            return half4(color, 1);
        }

        half4 Depth(Varyings input) : SV_Target { return 0; }

        half4 DepthNormal(Varyings input, bool front : SV_IsFrontFace) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            float3 n = normalize(input.normalWS) * (front ? 1 : -1);
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
