// A soft light added over the picture (the spirit's halo, its flashes, its
// trail): round on a quad (its UVs 0-1), or, _Trail, across a trail's width;
// out of the world's light and fog. Bloom makes it shine.
Shader "FinishTheGame/Glow"
{
    Properties
    {
        [HDR] _Tint ("Light", Color) = (3, 2.4, 1.4, 1)
        _Falloff ("Falloff", Float) = 2.5
        [Toggle] _Trail ("Across a trail", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }
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
                half _Falloff, _Trail;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = _Trail > 0.5 ? abs(i.uv.y * 2.0 - 1.0) : length(i.uv * 2.0 - 1.0);
                half light = pow(saturate(1.0 - r), _Falloff);
                return half4(_Tint.rgb * light * _Tint.a * i.color.rgb * i.color.a, 0);
            }
            ENDHLSL
        }
    }
}
