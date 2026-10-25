// The horizon's ribbon (HorizonRibbon.cs): the route unrolled into a picture
// (its length along u, across it along v), already lit when it was made, so
// drawn as it is, without the fog (it is far, and it is the way ahead to be
// seen); faded in and out by the vertices' alpha, and blurred where the
// vertices say (their second UVs' x: the mip level), far off.
Shader "FinishTheGame/Horizon"
{
    Properties
    {
        _MainTex ("The unrolled route", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Horizon"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 blur : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float blur : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.blur = v.blur.x;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 picture = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, i.uv, i.blur);
                return half4(picture.rgb * i.color.rgb, picture.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
