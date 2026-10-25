// The story's strip (CrystalWorld.cs): the ground along the whole route (a
// place as often as the route passes it), each vertex with its (s, t), drawn
// over the world, which goes on under it: up to where the ribbon peels off
// the road a little ahead it is not drawn (the world itself is there); past
// it, it is laid on the ribbon in the sky (Ribbon.hlsl, RibbonBend.cs), and
// goes from the waking world into the dream along it, by the ribbon's own
// metres (_RibbonDream):
// - the world: at first it is the road there and its verges, as the world
//   draws them (the grass by the place's x, z; the road's earth with its
//   ragged edge over it: BandLit's RoadCover; lit, shaded, fogged the same),
//   so that where it lies on the world it is the world; as it lifts off, its
//   edges tear;
// - a memory: its tones go over to the film's warm palette, dithered on
//   pixels that grow coarser step by step; lights kindle on it and twinkle;
//   it leaves the fog;
// - the dream: a strip of film, each frame a picture of its stretch of the
//   story (FilmPicture), its rules and sprocket holes.
// It casts no shadow.
Shader "FinishTheGame/Strip Lit"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _MainTex ("Grass (white: the vertex colour alone)", 2D) = "white" {}
        _RoadTex ("Road", 2D) = "white" {}
        _RoadTint ("Road's tint", Color) = (1, 1, 1, 1)
        _RoadEdge ("Road: half width, its wander, the fade, m; on", Vector) = (0, 0, 0, 0)
        _RoadTile ("Road's tile along it, m", Float) = 5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        // Coiled in the sky, the ribbon shows both its faces.
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Ribbon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _MainTex_ST;
            half4 _RoadTint;
            float4 _RoadEdge;
            float _RoadTile;
        CBUFFER_END
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_RoadTex);
        SAMPLER(sampler_RoadTex);

        // The film: x the strip's half width, m; y a frame's length along the story, m.
        float4 _RibbonFilm;
        // Along the ribbon, m: the memory from, to; the film from, to.
        float4 _RibbonDream;
        // The frames are pictures of their stretch of the story (CrystalWorld.cs
        // takes them): an atlas of columns × rows, the frames there; each
        // picture's left is its stretch's far end.
        TEXTURE2D(_FilmAtlas);
        SAMPLER(sampler_FilmAtlas);
        float4 _FilmGrid;   // columns, rows, frames, 0
        half4 _Tint = half4(1, 1, 1, 1);   // a branch's: dim, or burning away
        float _StoryFrom;   // drawn from this s on (a branch: its own road past where it parts)

        // The film's palette: six warm tones, dusk to cream.
        static const half3 Palette[6] =
        {
            half3(0.11, 0.07, 0.12), half3(0.29, 0.15, 0.20), half3(0.55, 0.27, 0.22),
            half3(0.80, 0.50, 0.28), half3(0.94, 0.76, 0.46), half3(0.99, 0.94, 0.80)
        };

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float ValueNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
        }

        // The road's cover at a place, d metres off its middle: 1 on it, 0 off (as BandLit's).
        half RoadCover(float2 place, float d)
        {
            float wander = ValueNoise(place / 30.0) - 0.5;
            float bites = ValueNoise(place / 5.0 + 17.0) - 0.5;
            float crumbs = ValueNoise(place / 1.2 + 41.0) - 0.5;
            float edge = _RoadEdge.x + _RoadEdge.y * (1.2 * wander + 1.0 * bites + 0.45 * crumbs);
            return 1.0 - smoothstep(edge - _RoadEdge.z, edge, abs(d));
        }

        // A 4×4 Bayer threshold, 0–1.
        float Bayer(int2 cell)
        {
            static const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            int2 c = cell & 3;
            return (m[c.y * 4 + c.x] + 0.5) / 16.0;
        }

        // How far into the memory and into the film the ribbon is c metres along it.
        float Memory(float c) { return smoothstep(_RibbonDream.x, _RibbonDream.y, c); }
        float FilmIn(float c) { return smoothstep(_RibbonDream.z, _RibbonDream.w, c); }

        // The memory's pixels: their size, m, coarser step by step.
        float Pixel(float memory) { return 0.3 * exp2(floor(memory * 3.999)); }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
            float4 story : TEXCOORD1;   // (s, t, 1, heading) on the story's curve
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Laid
        {
            float4 positionCS;
            float3 positionWS;
            float3 normalWS;
            float3 placeWS;
            float4 laid;   // its distance along the story from the focus; the ribbon's metres there, its width's share; t
        };

        Laid Lay(Attributes v)
        {
            Laid l;
            l.placeWS = l.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            l.normalWS = TransformObjectToWorldNormal(v.normalOS);
            float ds = RibbonDistance(v.story);
            bool on = ds > _RibbonParams.z;
            bool kept = !on || RibbonAt(ds - _RibbonParams.z, v.story, l.positionWS, l.normalWS);
            float2 measure = on ? RibbonMeasure(ds - _RibbonParams.z) : float2(0, 1);
            l.laid = float4(ds, measure, v.story.y);
            l.positionCS = kept ? TransformWorldToHClip(l.positionWS) : Gone();
            return l;
        }

        // Only the way on past where the ribbon peels off the road; a branch
        // only past its own start. Its edges torn as it lifts off the world,
        // and crumbling in pixels in the memory; clean on the film.
        void Keep(float3 placeWS, float4 laid)
        {
            float ds = laid.x, c = laid.y, share = laid.z, t = laid.w;
            clip(ds > _RibbonParams.z && ds + _RibbonParams.x >= _StoryFrom ? 1 : -1);
            float film = FilmIn(c), memory = Memory(c);
            float torn = (1.0 - film) * smoothstep(0.0, 40.0, c);
            if (torn <= 0.0) return;
            float side = t >= 0.0 ? 1.0 : -1.0;
            float edge = (_RibbonFilm.x - abs(t)) * share;   // m in from its edge
            float ragged = 2.2 * (0.25 + 0.75 * ValueNoise(float2(c / 6.0, side * 13.0))) + 0.6 * ValueNoise(float2(c / 1.3, side * 29.0));
            float pixel = Pixel(memory);
            float crumbs = memory * 3.0 * Hash(floor(float2(c, abs(t) * share) / pixel) + side * 7.0);
            clip(edge - torn * (ragged + crumbs));
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 placeWS : TEXCOORD2;
                float4 laid : TEXCOORD3;
                half4 color : COLOR;
                half fog : TEXCOORD4;
                float2 st : TEXCOORD5;
                float2 uv : TEXCOORD6;
            };

            Varyings vert(Attributes v)
            {
                Laid l = Lay(v);
                Varyings o;
                o.positionCS = l.positionCS;
                o.positionWS = l.positionWS;
                o.normalWS = l.normalWS;
                o.placeWS = l.placeWS;
                o.laid = l.laid;
                o.color = v.color * _BaseColor;
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.st = v.story.xy;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // The frame's picture, a little sepia, its rules and holes.
            half3 FilmPicture(float2 st)
            {
                float across = abs(st.y) / _RibbonFilm.x;          // 0 middle, 1 edge
                float frame = floor(st.x / _RibbonFilm.y);
                float along = st.x / _RibbonFilm.y - frame;         // 0–1 a frame
                if (across > 0.8)
                {
                    // Sprocket holes: six a frame.
                    float hole = frac(along * 6.0);
                    return abs(hole - 0.5) < 0.2 && abs(across - 0.9) < 0.045 ? Palette[5] : Palette[0];
                }
                if (across > 0.76 || min(along, 1.0 - along) < 0.012 || frame < 0.0 || frame >= _FilmGrid.z) return Palette[0];
                float2 inside = float2(1.0 - along, 0.5 + st.y / (2.0 * 0.76 * _RibbonFilm.x));
                float2 cell = float2(fmod(frame, _FilmGrid.x), floor(frame / _FilmGrid.x));
                half3 picture = SAMPLE_TEXTURE2D(_FilmAtlas, sampler_FilmAtlas, (cell + inside) / _FilmGrid.xy).rgb;
                half grey = dot(picture, half3(0.3, 0.55, 0.15));
                return lerp(picture, Palette[clamp((int)floor(grey * 5.0 + 0.5), 0, 5)], 0.3);
            }

            // The world remembered: warm tones, then the palette dithered on
            // ever coarser pixels; lights kindling on it, twinkling.
            half3 Remembered(half3 colour, float c, float across, float memory)
            {
                half grey = dot(colour, half3(0.3, 0.55, 0.15));
                // Sepia, as bright as it was.
                colour = lerp(colour, grey * half3(1.3, 0.98, 0.72), 0.75 * smoothstep(0.0, 0.4, memory));
                float pixel = Pixel(memory);
                int2 cell = int2(floor(float2(c, across) / pixel));
                float level = saturate(grey * 1.1 + 0.04) * 5.0 + (Bayer(cell) - 0.5) * 0.9;
                colour = lerp(colour, Palette[clamp((int)floor(level + 0.5), 0, 5)], smoothstep(0.25, 0.85, memory));
                // The lights: a few of the cells of a 3.5 m grid, each a round
                // glow somewhere in its cell, twinkling.
                float2 spot = float2(c, across) / 3.5;
                float2 home = floor(spot);
                float h = Hash(home + 3.7);
                if (h > 0.82)
                {
                    float2 middle = 0.3 + 0.4 * float2(Hash(home + 11.1), Hash(home + 23.3));
                    float glow = saturate(1.0 - 2.4 * length(frac(spot) - middle));
                    float twinkle = 0.25 + 0.75 * pow(0.5 + 0.5 * sin(_Time.y * (1.2 + 2.5 * h) + h * 61.0), 3.0);
                    colour += Palette[5] * (3.0 * glow * glow * twinkle * sin(PI * memory));
                }
                return colour;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                Keep(i.placeWS, i.laid);
                float c = i.laid.y, share = i.laid.z;
                // The world there: the grass, the road's earth over it.
                half3 albedo = i.color.rgb * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                if (_RoadEdge.w > 0.5)
                {
                    half cover = RoadCover(i.placeWS.xz, i.st.y);
                    half3 road = _RoadTint.rgb * SAMPLE_TEXTURE2D(_RoadTex, sampler_RoadTex, float2(0.5 + i.st.y / 5.0, i.st.x / _RoadTile)).rgb;
                    albedo = lerp(albedo, road, cover);
                }
                float3 n = normalize(i.normalWS) * (front ? 1 : -1);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half occlusion = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    occlusion = ao.indirectAmbientOcclusion;
                #endif
                half diffuse = saturate(dot(n, light.direction)) * light.shadowAttenuation;
                half3 colour = albedo * (SampleSH(n) * occlusion + light.color * diffuse);

                float memory = Memory(c), film = FilmIn(c);
                if (memory > 0.0) colour = Remembered(colour, c, i.st.y * share, memory);
                if (film > 0.0) colour = lerp(colour, FilmPicture(i.st), film);
                // The dream is outside the world: its own colours, no fog.
                return half4(lerp(MixFog(colour, i.fog), colour, memory) * _Tint.rgb, 1);
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

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 placeWS : TEXCOORD0;
                float4 laid : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Laid l = Lay(v);
                Varyings o;
                o.positionCS = l.positionCS;
                o.placeWS = l.placeWS;
                o.laid = l.laid;
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                Keep(i.placeWS, i.laid);
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

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 placeWS : TEXCOORD1;
                float4 laid : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Laid l = Lay(v);
                Varyings o;
                o.positionCS = l.positionCS;
                o.normalWS = l.normalWS;
                o.placeWS = l.placeWS;
                o.laid = l.laid;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                Keep(i.placeWS, i.laid);
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
