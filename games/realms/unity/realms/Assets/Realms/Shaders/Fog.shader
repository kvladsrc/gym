// The fog of war: a bank of clouds lying on the land the players have not seen. One transparent
// plane at sea level, drawn last and over everything (no depth test). Each pixel reads the depth
// texture, finds the point of the world it shows (on the sea's surface where the water hides the
// floor) and follows the ray from the camera to it through the clouds' height field: their top
// stands over the unseen cells as high as MapView asks for the cell (above the ground and the
// trees; MapView hides the towns, monsters and mountain peaks of unseen cells), falls in rounded
// shoulders at the edge and rolls in lumps, so the bank hides whatever is under it and has the
// same shape at every zoom. The cells' visibility and cloud height (_RealmsFog, read by
// RealmsSeen in RealmsCommon.hlsl) ease from cell centre to cell centre, and across the edge between
// seen and unseen a billowing noise frays the bank into round heads; the visibility fades in as
// MapView opens a cell. The clouds are a dark blue-grey, darker than the land so the explored
// island stays the brightest part of the picture, and leaning towards the far sea's colour (a
// quarter darker and greyer, the bank read as a grey slab beside the land, review of
// 2026-11-10); their edge frays into many small heads: lit by the sun on the slope of their top (the side
// towards it lighter, the far side in shade), only a little darker at their feet than at their
// tops (a dark foot reads as a cliff round the seen land, and a low camera sees mostly the sides),
// thinning out to mist at the foot and casting a faint shadow on the open land beside them.
// Broad swells of light and shade drift over the top (_Relief), so it does not lie flat as a
// slab. The bank rises from its foot in a long, low shoulder and is only as solid as it is thick
// over the ground a ray would reach, so its edge dissolves into mist over about half a cell
// instead of ending in a cut, on the side towards the camera too.
Shader "Realms/Fog"
{
    Properties
    {
        _CloudTop ("Cloud, lit top", Color) = (0.29, 0.33, 0.4, 1)
        _CloudShade ("Cloud, shade", Color) = (0.15, 0.18, 0.23, 1)
        _Relief ("Broad swells of light and shade, ± share", Range(0, 0.2)) = 0.1
        _GroundShadow ("Shadow on the land beside the clouds", Range(0, 1)) = 0.11
        _PuffSize ("Puff size, world units", Float) = 1.2
        _Billow ("Puffs along the edge", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+50" "IgnoreProjector"="True" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Realms/Shaders/RealmsCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CloudTop, _CloudShade;
                float _GroundShadow, _PuffSize, _Billow, _Relief;
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

            float3 SceneAt(float2 uv)
            {
                #if UNITY_REVERSED_Z
                    float raw = SampleSceneDepth(uv);
                #else
                    float raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                #endif
                return ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
            }

            #define CLOUD_CEILING 1.4

            // How much of the smallest swells to draw: they fade where a pixel spans more of them
            // than shows.
            static float fine;

            // Billows, about -0.4..0.5: the noise's magnitude, so it swells in round heads with
            // creases between them like a cumulus (rounded at the bottom, so they do not cut
            // notches into the edge), octaves turned against each other so no direction lines
            // them up.
            float Billows(float2 p, int octaves)
            {
                const float2x2 turn = float2x2(0.8, -0.6, 0.6, 0.8);
                float sum = 0, amplitude = 0.5;
                for (int k = 0; k < octaves; k++)
                {
                    float n = RealmsPerlin(p);
                    sum += amplitude * (sqrt(n * n + 0.005) * 2.5 - 0.6);
                    p = mul(turn, p) * 2.07 + 17.3;
                    amplitude *= 0.5;
                }
                return sum;
            }

            // The point's place in the drifting puffs.
            float2 PuffAt(float2 p)
            {
                return p / _PuffSize + float2(_Time.y * 0.012, -_Time.y * 0.008);
            }

            // The cloud's thickness from the point's fog (1 deep in the unseen land, 0 on the seen)
            // and the billows there: across the edge they push it up and down, never far inside a
            // cell (an unseen cell stays covered, a seen one open). height is the bank's there.
            float Thickness(float2 p, out float height)
            {
                float2 seen = RealmsSeen(p);
                height = seen.y;
                float fog = 1 - seen.x;
                if (fog < 0.02)
                    return 0;
                float edge = saturate(4 * fog * (1 - fog));
                return fog + Billows(PuffAt(p), 4) * lerp(0.15, 0.9, edge) * _Billow;
            }

            // The height of the clouds' top over a point, negative where there are none: the bank's
            // full height where the cloud is thick, falling to the ground in a long, low shoulder
            // where it thins (it rises gently from its foot: a steep one stood round the seen land
            // like a cut), with big rolling lumps and smaller swells on top.
            float Top(float2 p, out float height)
            {
                float thick = Thickness(p, height);
                float body = saturate((thick - 0.2) / 0.8);
                if (body <= 0)
                    return thick - 0.6;
                float2 q = PuffAt(p);
                float lumps = 1 + 0.25 * Billows(q * 0.5 + 11.3, 3) + 0.05 * fine * Billows(q * 2.1 + 31.7, 2);
                return height * body * body * (3 - 2 * body) * lumps;
            }

            float Top(float2 p)
            {
                float height;
                return Top(p, height);
            }

            // The cloud's colour at a point of it: on the top lit by the slope towards the sun and
            // in shade on the far one; down the sides, where the slope turns with every puff of
            // the edge, only by height, from the lighter top to a slightly darker foot, so the
            // sides read round instead of streaked.
            float3 CloudColor(float3 at, float3 sun)
            {
                const float e = 0.12;
                float keep = fine;
                fine = 0;
                float height;
                float h = Top(at.xz, height);
                float3 n = normalize(float3(h - Top(at.xz + float2(e, 0)), e, h - Top(at.xz + float2(0, e))));
                fine = keep;
                float flat = smoothstep(0.35, 0.8, n.y);
                float lit = saturate(0.86 + 0.45 * (dot(n, sun) - sun.y) * flat);
                float up = smoothstep(0.02, 0.9, at.y / max(height, 0.5));
                float3 color = lerp(_CloudShade.rgb, _CloudTop.rgb, lit * lerp(0.78, 1, up));
                // Broad swells of light and shade a few cells across, drifting with the puffs
                // (the noise is about -0.7..0.7).
                return color * (1 + _Relief / 0.7 * RealmsPerlin(PuffAt(at.xz) * 0.3 + 5.1));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 scene = SceneAt(uv);
                // Below sea level the water's surface is what shows, where this plane lies.
                float3 s = scene.y < 0 ? input.positionWS : scene;
                fine = 1 - smoothstep(0.03, 0.08, length(fwidth(s.xz)));
                Light sun = GetMainLight();
                float3 toSun = sun.direction;

                // Down the ray from where it enters the clouds' layer to the point, until it
                // passes under their top.
                float3 eye = _WorldSpaceCameraPos;
                const float ceiling = CLOUD_CEILING;
                bool hit = false;
                float3 at = s;
                float gap = 10;
                float3 near = s;
                [branch]
                if (s.y < ceiling && eye.y > s.y)
                {
                    float3 from = lerp(eye, s, saturate((eye.y - ceiling) / (eye.y - s.y)));
                    float3 prev = from;
                    float prevGap = from.y - Top(from.xz);
                    const int steps = 16;
                    [loop]
                    for (int k = 1; k <= steps; k++)
                    {
                        float3 here = lerp(from, s, k / (float)steps);
                        float d = here.y - Top(here.xz);
                        if (d <= 0)
                        {
                            // Halve the step a few times to find where the ray enters, then
                            // take the straight line between.
                            [unroll]
                            for (int b = 0; b < 4; b++)
                            {
                                float3 mid = (prev + here) * 0.5;
                                float dm = mid.y - Top(mid.xz);
                                if (dm > 0)
                                {
                                    prev = mid;
                                    prevGap = dm;
                                }
                                else
                                {
                                    here = mid;
                                    d = dm;
                                }
                            }
                            at = lerp(prev, here, prevGap / max(prevGap - d, 1e-5));
                            hit = true;
                            break;
                        }
                        if (d < gap)
                        {
                            gap = d;
                            near = here;
                        }
                        prev = here;
                        prevGap = d;
                    }
                }

                // At its foot, where the ray crosses little of it, the cloud thins to mist, and it
                // is only as solid as the bank is thick over the ground the ray would reach. A ray
                // that grazes the top of the bank's side towards the camera comes down near its
                // edge, so that side fades out over the land under it. (Solid by the thickness
                // where the ray enters, it ended in a cut: from above, that shoulder lies nearly
                // along the rays and showed as a line a few pixels high.)
                if (hit)
                {
                    float h;
                    float solid = smoothstep(0.3, 0.9, Thickness(s.xz, h));
                    return half4(CloudColor(at, toSun), smoothstep(0, 0.08, at.y - max(s.y, 0)) * solid);
                }

                // Missed: the edge of a lump the ray just grazed, softened (only as solid as the
                // cloud over the ground the ray reaches, like a hit: a ray skimming the foot of
                // the bank's side towards the camera drew a thin dark line along the faded edge),
                // or the open land in the clouds' shadow.
                float graze = 1 - smoothstep(0, 0.05, gap);
                [branch]
                if (graze > 0)
                {
                    float h;
                    graze *= smoothstep(0.3, 0.9, Thickness(s.xz, h));
                }
                float shadow = 0;
                const float shadowHeight = 0.6;
                [branch]
                if (s.y < shadowHeight)
                {
                    float3 b = s + toSun * ((shadowHeight - s.y) / max(toSun.y, 0.3));
                    shadow = smoothstep(-0.05, 0.2, Top(b.xz) - b.y) * _GroundShadow;
                }
                float alpha = max(graze, shadow);
                if (alpha < 0.002)
                    return 0;
                float3 color = _CloudShade.rgb * 0.25;
                [branch]
                if (graze > 0)
                    color = lerp(color, CloudColor(near, toSun), graze / alpha);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
