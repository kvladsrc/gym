// A ground of tiled images blended by masks, as the level's Blender material
// paints it (LevelBuilder.Ember): a bake of 300 x 500 m at 12 cm a pixel
// makes the images' stones, twigs and leaves one colour; here each image
// keeps its own scale. The masks (baked by tools/export_level.py from the
// ground's colour attributes): A = trail, wet, pine; B = autumn, rock, moss.
// Far off, the baked atlas, so the tiles' repeat does not show.
Shader "Soulslike/GroundLayers"
{
    Properties
    {
        _Atlas ("Baked atlas (far)", 2D) = "grey" {}
        _MaskA ("Mask: trail, wet, pine", 2D) = "black" {}
        _MaskB ("Mask: autumn, rock, moss", 2D) = "black" {}
        _Litter ("Forest litter (base)", 2D) = "grey" {}
        _Needles ("Pine needles", 2D) = "grey" {}
        _Leaves ("Fallen leaves", 2D) = "grey" {}
        _Damp ("Damp soil", 2D) = "grey" {}
        _Gravel1 ("Gravel (trails)", 2D) = "grey" {}
        _Gravel2 ("Gravel (rock)", 2D) = "grey" {}
        _Tiles ("Tile sizes, m: litter, needles, leaves, damp", Vector) = (3, 2.4, 2.8, 3.2)
        _Tiles2 ("Tile sizes, m: gravel 1, gravel 2", Vector) = (2.2, 2.6, 0, 0)
        _Fade ("Near, far: the images give way to the atlas, m", Vector) = (25, 70, 0, 0)
        _AtlasTint ("How much of the atlas's colour stays near", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Atlas); SAMPLER(sampler_Atlas);
            TEXTURE2D(_MaskA); SAMPLER(sampler_MaskA);
            TEXTURE2D(_MaskB);
            TEXTURE2D(_Litter); SAMPLER(sampler_Litter);
            TEXTURE2D(_Needles);
            TEXTURE2D(_Leaves);
            TEXTURE2D(_Damp);
            TEXTURE2D(_Gravel1);
            TEXTURE2D(_Gravel2);

            CBUFFER_START(UnityPerMaterial)
                float4 _Atlas_ST;
                float4 _Tiles;
                float4 _Tiles2;
                float4 _Fade;
                float _AtlasTint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), u.x), u.y);
            }

            // A layer's coordinates: the world's ground plane at the layer's
            // tile size, turned by its own angle and bent a little by noise,
            // so neither the tiles nor two layers line up.
            float2 Coordinates(float2 world, float size, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                float2 p = float2(c * world.x - s * world.y, s * world.x + c * world.y) / size;
                return p + 0.35 * float2(Noise(world * 0.11 + angle), Noise(world * 0.11 - angle));
            }

            half4 Layer(TEXTURE2D_PARAM(map, samplerMap), float2 world, float size, float angle)
            {
                return SAMPLE_TEXTURE2D(map, samplerMap, Coordinates(world, size, angle));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 world = input.positionWS.xz;
                half3 maskA = SAMPLE_TEXTURE2D(_MaskA, sampler_MaskA, input.uv).rgb; // trail, wet, pine
                half3 maskB = SAMPLE_TEXTURE2D(_MaskB, sampler_MaskA, input.uv).rgb; // autumn, rock, moss
                half3 atlas = SAMPLE_TEXTURE2D(_Atlas, sampler_Atlas, TRANSFORM_TEX(input.uv, _Atlas)).rgb;
                float patches = Noise(world * 0.15);

                // In the order of the Blender material: the moss litter, then
                // each tileable image where its mask (and, for
                // needles and leaves, a slow noise) says.
                half3 color = Layer(TEXTURE2D_ARGS(_Litter, sampler_Litter), world, _Tiles.x, 0.3).rgb;
                half4 needles = Layer(TEXTURE2D_ARGS(_Needles, sampler_Litter), world, _Tiles.y, 1.1);
                color = lerp(color, needles.rgb, saturate(maskA.b * 1.6 * (0.6 + patches)) * needles.a);
                half4 leaves = Layer(TEXTURE2D_ARGS(_Leaves, sampler_Litter), world, _Tiles.z, 2.3);
                color = lerp(color, leaves.rgb, saturate(maskB.r * 1.6 * (1.4 - patches)) * leaves.a);
                half4 damp = Layer(TEXTURE2D_ARGS(_Damp, sampler_Litter), world, _Tiles.w, 0.7);
                color = lerp(color, damp.rgb, saturate(maskA.g * 1.5) * damp.a);
                half4 trail = Layer(TEXTURE2D_ARGS(_Gravel1, sampler_Litter), world, _Tiles2.x, 1.9);
                color = lerp(color, trail.rgb, saturate(maskA.r * 1.8) * trail.a);
                half4 rock = Layer(TEXTURE2D_ARGS(_Gravel2, sampler_Litter), world, _Tiles2.y, 2.9);
                color = lerp(color, rock.rgb, saturate(maskB.g * 1.5) * rock.a);
                color = lerp(color, color * half3(0.6, 0.72, 0.42), saturate(maskB.b) * 0.6); // moss

                // The painted colour of the whole (the atlas) tints the images
                // near, and is all there is far off.
                half3 near = lerp(color, color * atlas / max(Luminance(atlas), 0.05) * Luminance(color), _AtlasTint);
                float distance = length(input.positionWS - GetCameraPositionWS());
                half3 albedo = lerp(near, atlas, saturate((distance - _Fade.x) / max(_Fade.y - _Fade.x, 1.0)));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.smoothness = 0.05;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 lit = UniversalFragmentPBR(inputData, surface);
                lit.rgb = MixFog(lit.rgb, inputData.fogCoord);
                return lit;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
}
