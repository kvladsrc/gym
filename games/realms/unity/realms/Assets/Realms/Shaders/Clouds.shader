// The drifting clouds (RealmsCloudAt in RealmsCommon.hlsl): small clusters of round puffs riding
// the wind over the map at MapView's cloud height, their tops lit by the sun, the side away from
// it in a cool shade and the waists between puffs a little darker. Their shadows on the land and
// the water come from the same clouds (RealmsClouds). One transparent plane over the map, drawn
// after the fog of war; MapView fades the clouds out as the camera comes down to the land
// (_RealmsCloudShow), so they never stand between the player and a close look. They show only
// where the land behind them, as the camera sees it, has been seen (RealmsCloudSeen), never over
// the fog of war, and keep a cell away from towns and roads (RealmsCloudRoom); clearly lighter
// than the fog and dense enough to read as white clouds over the sea, not as grey smudges.
Shader "Realms/Clouds"
{
    Properties
    {
        _Top ("Lit top", Color) = (0.9, 0.89, 0.86, 1)
        _Shade ("Shade", Color) = (0.66, 0.69, 0.76, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+60" "IgnoreProjector"="True" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Realms/Shaders/RealmsCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Shade;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(_RealmsCloudShow - 0.001);
                float px = max(length(fwidth(input.positionWS.xz)), 1e-4);
                float3 n;
                float crease;
                float2 middle;
                float cover = RealmsCloudAt(input.positionWS.xz, 0.08 + px, n, crease, middle);
                clip(cover - 0.001);
                // By the land behind the cloud along the view, where the camera sees it stand: by
                // the land right under it, clouds near the far edge of the seen land stood over the
                // fog of war. The whole cloud thins away by the land behind its middle near a town
                // or a road.
                float3 at = input.positionWS, eye = _WorldSpaceCameraPos;
                float behind = at.y / max(eye.y - at.y, 0.1);
                cover *= RealmsCloudSeen(at.xz + (at.xz - eye.xz) * behind);
                cover *= RealmsCloudRoom(middle + (middle - eye.xz) * behind);
                clip(cover - 0.001);
                float lit = smoothstep(-0.35, 0.75, dot(n, _MainLightPosition.xyz));
                float3 color = lerp(_Shade.rgb, _Top.rgb, lit) * (1 - 0.12 * crease);
                return half4(color, cover * _Opacity * _RealmsCloudShow);
            }
            ENDHLSL
        }
    }
}
