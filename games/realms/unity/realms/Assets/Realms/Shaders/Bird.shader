// Birds over the map (Birds; the visual plan's 6, allowed by the players on 2026-11-12). Every
// bird is two wing triangles on four corners: TEXCOORD0 holds the corner (x: 0 the breast, 1 the
// tail, 2 the left wing's tip, 3 the right's), the kind (y: 0 a bird of the flock, 1 a gull), the
// bird's own random (z) and its place in the flock (w, 0 the leader). A flock's bird keeps its
// place in the wedge (TEXCOORD1 x back, y to the left) behind the leader, whose place, heading
// and size Birds sets each frame (_RealmsBirdFlock, _RealmsBirdHeading). A gull circles its pier
// or fishing hut: TEXCOORD1 the circle's middle x, z, its radius and height, TEXCOORD2 the turn a
// second (signed) and where on the circle it started. The wings beat three times a second with
// pauses to glide; gulls mostly glide and lean into the turn. Dark, muted birds and light grey
// gulls (the vertex colour; alpha 0 hides a gull without a place), faint over the fog of war,
// shaded by the drifting clouds, and gone under the task lines and the day's events, which are
// text over the map.
Shader "Realms/Bird"
{
    Properties
    {
        _Span ("Half the wingspan: a flock's bird, a gull", Vector) = (0.1, 0.13, 0, 0)
        _Opacity ("Opacity", Range(0, 1)) = 0.9
    }
    SubShader
    {
        // After the water and the smoke below them, before the fog and the drifting clouds above.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" "IgnoreProjector"="True" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Realms/Shaders/RealmsCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Span;
                float _Opacity;
            CBUFFER_END

            // Birds sets these each frame.
            float4 _RealmsBirdFlock;   // the leader's x, height, z; how much the flock shows
            float4 _RealmsBirdHeading; // its heading on the map (x, z, a unit); how many birds fly; the wedge's spread
            float4 _RealmsBirdShow;    // x how much the gulls show; y the flock's climb, 0 just risen (tight) to 1 cruising

            #define REALMS_TAU 6.2831853

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 bird : TEXCOORD0;
                float4 place : TEXCOORD1;
                float4 turn : TEXCOORD2;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float corner = input.bird.x;
                float own = input.bird.z;
                float t = _Time.y;
                float3 body;
                float2 forward;
                float bank, span, show, beat;
                if (input.bird.y > 0.5)
                {
                    // A gull on its circle, leaning into the turn.
                    float turns = input.turn.x;
                    float way = turns >= 0 ? 1.0 : -1.0;
                    float s, c;
                    sincos(t * turns + input.turn.y, s, c);
                    body = float3(input.place.x + c * input.place.z, input.place.w + sin(t * 0.37 + own * 11.0) * 0.04,
                        input.place.y + s * input.place.z);
                    forward = float2(-s, c) * way;
                    bank = 0.3 * way;
                    span = _Span.y * (0.92 + 0.16 * own);
                    show = _RealmsBirdShow.x * input.color.a;
                    // Mostly gliding: about a second of beats in every five, each gull at its own moment.
                    float cycle = frac(t / 5.3 + own);
                    float beating = smoothstep(0.0, 0.04, cycle) * (1.0 - smoothstep(0.16, 0.22, cycle));
                    beat = 0.16 + beating * 0.5 * sin(t * 2.6 * REALMS_TAU + own * REALMS_TAU);
                }
                else
                {
                    // A bird of the flock in its place of the wedge, drifting a little in it.
                    float2 heading = _RealmsBirdHeading.xy;
                    float2 left = float2(-heading.y, heading.x);
                    float spread = _RealmsBirdHeading.w * lerp(0.55, 1.0, _RealmsBirdShow.y);
                    float back = input.place.x * spread + sin(t * 0.7 + own * 23.0) * 0.03;
                    float across = input.place.y * spread + sin(t * 0.53 + own * 37.0) * 0.03;
                    float2 at = _RealmsBirdFlock.xz - heading * back + left * across;
                    body = float3(at.x, _RealmsBirdFlock.y + sin(t * 0.9 + own * 17.0) * 0.03, at.y);
                    forward = heading;
                    bank = sin(t * 0.4 + own * 9.0) * 0.08;
                    span = _Span.x * (0.9 + 0.2 * own);
                    // Only as many birds as the flock has.
                    show = _RealmsBirdFlock.w * step(input.bird.w + 0.5, _RealmsBirdHeading.z);
                    // Three beats a second, then a glide; each bird at its own moment.
                    float cycle = frac(t / 2.6 + own * 0.71);
                    float beating = smoothstep(0.0, 0.06, cycle) * (1.0 - smoothstep(0.55, 0.65, cycle));
                    beat = lerp(0.14, 0.55 * sin(t * 3.0 * REALMS_TAU + own * REALMS_TAU), beating);
                }
                // Faint over the fog of war: a flock comes out of the clouds where the seen land begins.
                show *= smoothstep(0.3, 0.8, RealmsSeen(body.xz).x);

                float3 ahead = float3(forward.x, 0, forward.y);
                float3 left3 = float3(-forward.y, 0, forward.x);
                float3 position = body;
                if (show > 0.001)
                {
                    // A short body and wings swept back past it, so that the bird reads as a thin
                    // V from above: with a long body (breast to tail about the span) the wings filled
                    // into a grey arrowhead in the captures of 2026-11-12.
                    if (corner < 0.5)
                        position += ahead * span * 0.3;
                    else if (corner < 1.5)
                        position -= ahead * span * 0.05;
                    else
                    {
                        // A wing's tip, swept back, raised by the beat and lowered on the turn's inner side.
                        float side = corner < 2.5 ? 1.0 : -1.0;
                        float sa, ca;
                        sincos(beat - side * bank, sa, ca);
                        position += left3 * (side * span * ca) + float3(0, span * sa, 0) - ahead * span * 0.5;
                    }
                }
                // A hidden bird folds into a point and draws nothing.
                output.positionWS = position;
                output.positionCS = TransformWorldToHClip(position);
                output.color = half4(input.color.rgb, show);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Not over the task lines and the day's events: birds there crossed the words.
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 soft = 30 / _ScreenParams.xy;
                float under = max(RealmsHudInside(_RealmsHudMask, uv, soft), RealmsHudInside(_RealmsHudMask2, uv, soft));
                float alpha = input.color.a * _Opacity * (1 - under);
                clip(alpha - 0.01);
                return half4(input.color.rgb * RealmsClouds(input.positionWS), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
