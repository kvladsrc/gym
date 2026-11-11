// The map's ground: vertex colours from TerrainField, lit by the display-colour model, with the
// territory tint before the light and the grid, borders and rings after it. The colours carry the
// pattern of the ground textures (grass, dry grass, forest floor, sand, scree, rock, dirt) in the
// shares TerrainField gives each vertex, rock on steep slopes, stronger in some stretches than in
// others. Drawn per pixel: broad warm and cool drifts, patches of lush and dry grass and bare
// earth under the pattern; on top of it clumps and smaller things on the ground (flowers, tufts,
// leaves, pebbles) gathered in clusters, ploughed fields on wheat cells (flag 1 in the cell
// texture), the towns' trodden squares and lanes (paved squares from level 2) and the dirt roads
// of the road mask (alpha). Each ground layer has its own tile size, so grass blades, stones and
// snow crust come out at believable sizes next to each other. Grass stands on the green ground,
// also drawn per pixel (Grass): fine blades close up, coarser ones further out, its mean colour
// far away; the wind runs over it and over the wheat in lighter waves (RealmsGrassWind), as the
// drifting clouds and their shadows show it overhead (Clouds.shader). Under the sea the overlay
// fades out: the water draws its own on the surface. The battle field's ground is drawn with it
// too, off the map, where the cells read as open sea: its small things follow _RealmsFieldBiome.
Shader "Realms/Terrain"
{
    Properties
    {
        _Tint ("Territory tint", Range(0, 1)) = 0.24
        _FieldA ("Field", Color) = (0.83, 0.74, 0.47, 1)
        _FieldB ("Field, second crop", Color) = (0.73, 0.61, 0.38, 1)
        _FieldGreen ("Field, green crop", Color) = (0.59, 0.7, 0.43, 1)
        _Road ("Road", Color) = (0.58, 0.52, 0.37, 1)
        _RoadEdge ("Road edge", Color) = (0.5, 0.44, 0.31, 1)
        _Paving ("Paving", Color) = (0.8, 0.77, 0.71, 1)
        _Details ("Small things on the ground", Range(0, 1)) = 1
        _Ground ("Ground layers", 2DArray) = "" {}
        _GroundDetail ("Ground texture", Range(0, 1)) = 0.75
        _GroundScale ("Ground texture tile, world units", Float) = 1.3
        _LayerTilesA ("Tile per layer: grass, dry grass, forest, sand", Vector) = (0.6, 0.6, 1, 0.8)
        _LayerTilesB ("Tile per layer: scree, rock, snow, dirt", Vector) = (0.7, 1, 1.3, 1)
        _CobbleTile ("Cobble tile, world units", Float) = 0.32
        _SandStrength ("Sand pattern strength", Range(0, 1)) = 0.7
        _Grass ("Grass", Range(0, 1)) = 1
        _GrassHeight ("Grass height, world units", Float) = 0.055
        _GrassBlades ("Blade spacing close up, world units", Float) = 0.016
        _GrassCoarse ("Blade spacing further out, world units", Float) = 0.032
        _ContactShade ("Shade at the feet of houses, walls and trunks", Range(0, 1)) = 0.23
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Realms/Shaders/RealmsCommon.hlsl"
        #include "Assets/Realms/Shaders/RealmsGround.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Tint;
            half4 _FieldA, _FieldB, _FieldGreen, _Road, _RoadEdge, _Paving;
            float _Details, _GroundDetail, _GroundScale, _CobbleTile, _SandStrength;
            float4 _LayerTilesA, _LayerTilesB;
            float _Grass, _GrassHeight, _GrassBlades, _GrassCoarse, _ContactShade;
        CBUFFER_END
        float3 _LightDirection, _LightPosition;
        // The battle field's biome (BattleView sets it while a fight is shown, 0 otherwise).
        float _RealmsFieldBiome;
        // The shade round the feet of what stands on the land (ContactShade): red for houses,
        // walls and towers, green for tree trunks, which fades where a screen cuts the plants
        // away. _RealmsContactRect: the texture's x and z origin and 1 / its size in world
        // units; z is 0 while there is no map. Off the texture (the battle field) there is none.
        TEXTURE2D(_RealmsContact);
        SAMPLER(sampler_RealmsContact);
        float4 _RealmsContactRect;

        float Contact(float2 p)
        {
            float2 uv = (p - _RealmsContactRect.xy) * _RealmsContactRect.zw;
            float2 shade = SAMPLE_TEXTURE2D(_RealmsContact, sampler_RealmsContact, uv).rg;
            float inside = _RealmsContactRect.z > 0 && all(uv == saturate(uv)) ? 1 : 0;
            return max(shade.r, shade.g * (1 - RealmsCut(p))) * inside;
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float4 layersA : TEXCOORD1; // shares of ground layers 0-3
            float4 layersB : TEXCOORD2; // and 4-7
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float4 color : TEXCOORD2;
            float4 layersA : TEXCOORD3;
            float4 layersB : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.color = input.color;
            output.layersA = input.layersA;
            output.layersB = input.layersB;
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

        // The ground textures' pattern in the shares of the vertex weights, rock taking over steep
        // slopes. Where layers meet the taller pattern wins (grass blades over sand, stones over
        // grass), so the edges are ragged rather than smeared. Each layer has its own tile
        // (_LayerTilesA/B times _GroundScale). The sand's ripples are the texture's strongest
        // pattern: they show at _SandStrength, less in calm patches and far away.
        float3 GroundPattern(float2 p, float2 dx, float2 dy, float4 sharesA, float4 sharesB, float3 n)
        {
            float shares[GROUND_LAYERS] = { sharesA.x, sharesA.y, sharesA.z, sharesA.w, sharesB.x, sharesB.y, sharesB.z, sharesB.w };
            float tiles[GROUND_LAYERS] = { _LayerTilesA.x, _LayerTilesA.y, _LayerTilesA.z, _LayerTilesA.w,
                _LayerTilesB.x, _LayerTilesB.y, _LayerTilesB.z, _LayerTilesB.w };
            float steep = smoothstep(0.86, 0.7, n.y);
            float mix = RealmsNoise(p * 0.45 + 17.3);
            float4 layer[GROUND_LAYERS];
            float score[GROUND_LAYERS];
            float best = -10;
            [unroll]
            for (int k = 0; k < GROUND_LAYERS; k++)
            {
                float share = k == GROUND_ROCK ? lerp(shares[k], 1, steep) : shares[k] * (1 - steep);
                layer[k] = 1;
                score[k] = -10;
                [branch]
                if (share > 0.02)
                {
                    layer[k] = GroundFlat(p, dx, dy, k, _GroundScale * tiles[k], mix);
                    if (k == GROUND_SAND)
                    {
                        // The ripples come and go in patches and turn and grow with them (the
                        // layer laid again, larger and turned, where a slow noise says), and fade
                        // sooner than the mipmaps far away: at one size and strength everywhere
                        // they repeated as the same scales up to the horizon (review of 2026-11-10).
                        const float2x2 turn = float2x2(0.28, -0.96, 0.96, 0.28);
                        float4 wide = GroundFlat(mul(turn, p) + 23.1, mul(turn, dx), mul(turn, dy), k,
                            _GroundScale * tiles[k] * 1.6, 1 - mix);
                        layer[k] = lerp(layer[k], wide, smoothstep(0.4, 0.6, RealmsNoise(p * 0.23 + 41.7)));
                        float calm = lerp(0.45, 1, smoothstep(0.3, 0.7, RealmsNoise(p * 0.11 + 7.9)));
                        float far = smoothstep(0.015, 0.05, max(length(dx), length(dy)));
                        layer[k].rgb = lerp(1, layer[k].rgb, _SandStrength * calm * lerp(1, 0.4, far));
                    }
                    score[k] = share + layer[k].a * 0.25;
                    best = max(best, score[k]);
                }
            }
            float3 pattern = 0;
            float total = 0;
            [unroll]
            for (int j = 0; j < GROUND_LAYERS; j++)
            {
                float b = max(0, score[j] - best + 0.2);
                pattern += layer[j].rgb * b;
                total += b;
            }
            return pattern / max(total, 1e-4);
        }

        // Two patches of crops inside a wheat hexagon, split by a path, each sown in its own
        // direction. The rows are furrows: the flank facing the sun is lit, the far one shaded,
        // the soil shows in the trough between the rows, and each row breaks into tufts. Every
        // few rows a strip ripens a little differently, which still reads when the rows are too
        // fine to draw (they fade into their mean below a few pixels, px being the pixel's size
        // on the ground). The light on the flanks is a smooth wave and holds down to a few pixels
        // a row; the soil in the troughs and the tufts change sharply from row to row and go
        // earlier, from about ten pixels a row, since thinner they crawl and flicker as the view
        // moves (px, fwidth's length, is 1.6-2.2 real pixels). The crop thins out raggedly over
        // its last stretch to the hexagon's edge and to the path, so the field grows out of the
        // grass instead of lying on it. The wind's waves (wave, RealmsGrassWind's z) run over the
        // crop as lighter bands, where the stalks bend and show more of their sunlit length.
        float3 Fields(float3 albedo, float2 p, float px, float wave)
        {
            int2 h = RealmsHexAt(p);
            int4 cell = RealmsCellAt(h);
            if ((cell.z & 1) == 0)
                return albedo;
            float2 local = p - RealmsCenter(h);
            float seed = RealmsHash(float2(h) * 0.37 + 0.11);
            float angle = seed * 6.2831;
            float2 axis = float2(cos(angle), sin(angle));
            float2 across = float2(-axis.y, axis.x);
            float side = dot(local, across);
            // dir crosses the rows, row runs along them.
            float2 dir = side > 0 ? axis : across;
            float2 row = float2(-dir.y, dir.x);
            const float rows = 14;  // per world unit: a cell is about 1.7 across
            float t = dot(local, dir) * rows + seed * 3;
            float k = floor(t), f = frac(t);
            float rowPx = 1 / (rows * px);
            float show = smoothstep(4, 7, rowPx);
            float showLit = smoothstep(2, 3.5, rowPx);
            float tuft = lerp(0.5, RealmsNoise(float2(dot(local, row) * 9, k * 3.7 + seed * 17)), show);
            // A ridge's cross-section is a raised cosine, so its slope across the row goes with
            // sin; the flank turned to the sun gets more of it.
            float2 sun = normalize(_MainLightPosition.xz + 1e-4);
            float lit = sin(f * 6.2831) * dot(dir, sun) * showLit;
            float soil = smoothstep(0.3, 0.5, f) * smoothstep(0.7, 0.5, f) * lerp(0.4, 1, 1 - tuft) * show;
            float3 crop = side > 0 ? _FieldA.rgb : (seed > 0.6 ? _FieldGreen.rgb : _FieldB.rgb);
            float strip = RealmsHash(float2(floor(t / 4), seed * 31 + (side > 0 ? 7.3 : 0)));
            crop *= (0.92 + 0.1 * strip) * (0.95 + 0.1 * RealmsNoise(p * 2.3 + seed * 5));
            float3 field = crop * (1 + 0.16 * lit + 0.14 * (tuft - 0.5)) * (1 + 0.3 * wave);
            field = lerp(field, _RoadEdge.rgb * 0.9, soil * 0.6 + 0.05 * (1 - show));
            // The hexagon's edge, a little wavy, and the path between the patches.
            float edge = 10;
            [unroll]
            for (int d = 0; d < 6; d++)
                edge = min(edge, REALMS_INNER - dot(local, RealmsNormals[d]));
            edge += (RealmsNoise(p * 7) - 0.5) * 0.08;
            float path = abs(side) + (RealmsNoise(p * 9 + 3.3) - 0.5) * 0.02;
            float inside = smoothstep(0.04, 0.2, edge) * smoothstep(0.015, 0.07, path);
            float cover = saturate((inside - (1 - tuft) * 0.5) * 2);
            return lerp(albedo, field, cover);
        }

        // The ground's own variety across a few cells, which the hexagons' colours cannot give:
        // broad drifts of warmer and cooler, lighter and darker ground, inside them softer patches
        // of lush and of dry grass, and here and there a spot of bare earth, all on round noise
        // warped so no grid or row shows. Green ground (green, its layers' share) changes its hue,
        // sand and stone mostly their lightness. bare (0..1) is where the earth shows, for the
        // grass texture to calm there; lush and dry (0..1) are the patches of lush and dry grass.
        // The dry patches and the bare spots have ragged edges and end within a few tenths of a
        // unit: with soft round edges a dry patch read as a brownish haze and a bare spot as a
        // puddle or a rusty stain (review of 2026-11-10). On the battle field there are fewer
        // bare spots, as they lay among the figures like stains.
        float3 Variety(float3 albedo, float2 p, float green, float land, out float bare, out float lush, out float dry)
        {
            float2 warp = float2(RealmsPerlin(p * 0.23 + 3.1), RealmsPerlin(p * 0.23 + 8.7));
            float broad = RealmsFbm(p * 0.15 + warp * 0.6);
            float patch = RealmsFbm(p * 0.6 + warp * 1.8 + 13.7);
            // The warm drift and the dry patch together took a third of the blue off, and a dry
            // patch in a warm drift read as a rusty blot (review 52); now straw rather than rust.
            float3 hue = lerp(1, broad > 0 ? float3(1.06, 1.03, 0.85) : float3(0.8, 0.93, 0.9), saturate(abs(broad) * 2.6));
            float3 tint = lerp(1 + broad * 0.22, hue * (1 + broad * 0.08), green);
            lush = smoothstep(0.04, 0.24, patch) * green;
            dry = smoothstep(-0.1, -0.2, patch + (RealmsNoise(p * 4.3 + 5.5) - 0.5) * 0.16) * green;
            tint *= lerp(1, float3(0.74, 0.86, 0.68), lush * 0.9);
            tint *= lerp(1, float3(1.08, 1.05, 0.85), dry * 0.7);
            float spot = RealmsFbm(p * 1.4 + warp + 41.3) + dry * 0.15 + (RealmsNoise(p * 7.1 + 2.2) - 0.5) * 0.1;
            bare = smoothstep(0.36, 0.46, spot) * green * land * (_RealmsFieldBiome > 0.5 ? 0.4 : 1);
            float3 color = albedo * lerp(1, tint, land);
            float3 earth = SRGBToLinear(float3(0.72, 0.64, 0.5)) * (0.92 + 0.16 * patch);
            return lerp(color, earth, bare * 0.6);
        }

        // How many of the small things grow at a point, 0.1..1.9 times the biome's density: they
        // gather in clusters with bare stretches between rather than spreading evenly.
        float Gathered(float cluster, float low, float high)
        {
            return lerp(low, high, smoothstep(-0.2, 0.3, cluster));
        }

        // Clumps a few pixels wide on a coarser grid, flat below and lit on top: grass tufts on
        // the steppe and meadows, flower clusters, ferns and fallen leaves in forests, stones on
        // hills and mountains, shells on beaches. Big enough to survive the pixel view. The flat
        // tufts and ferns give way where standing grass shows (grassShow, 0..1). In a dry patch
        // on green ground (dry, 0..1) there are more of them and most are straw tufts, which is
        // what makes the patch read as dry grass where the blades are too fine to draw.
        float3 Clumps(float3 albedo, float2 p, float px, int biome, float land, float cluster, float grassShow, float dry)
        {
            float density = biome == 4 ? 0.5 : biome == 3 ? 0.42 : biome == 5 ? 0.34 : biome == 6 ? 0.38
                : biome == 2 ? 0.12 : biome == 7 ? 0.14 : 0;
            bool green = biome >= 3 && biome <= 6;
            density *= Gathered(cluster, 0.1, 1.8) * (green ? 1 + 1.2 * dry : 1);
            const float g = 0.2;
            float2 id = floor(p / g);
            float2 f = p / g - id;
            float r1 = RealmsHash(id + 4.13), r2 = RealmsHash(id * 1.3 + 8.7), r3 = RealmsHash(id * 0.77 + 2.9);
            if (r1 > density * _Details * lerp(1, 0.4, _GroundDetail) || land < 0.5)
                return albedo;
            float2 v = (f - (0.25 + 0.5 * float2(r2, r3))) * g;
            float radius = g * (0.17 + 0.1 * r2);
            float pick = frac(r1 * 5.17 + r3 * 2.3);
            float3 dark, light;
            float fade = 1;
            if (green && frac(r2 * 7.7 + r3 * 3.1) < dry * 0.85)
            {
                fade = 1 - grassShow;
                v.x *= 0.8;
                dark = albedo * float3(0.78, 0.74, 0.6);
                light = albedo * float3(1.2, 1.12, 0.8);
            }
            else if (biome == 4)
            {
                fade = 1 - grassShow;
                v.x *= 0.8;
                dark = albedo * float3(0.74, 0.68, 0.44);
                light = albedo * float3(1.1, 1.07, 0.84);
            }
            else if (biome == 3 && pick < 0.2)
            {
                float3 flower = pick < 0.07 ? float3(0.9, 0.89, 0.84) : pick < 0.13 ? float3(0.86, 0.75, 0.4)
                    : pick < 0.17 ? float3(0.8, 0.56, 0.6) : float3(0.62, 0.57, 0.78);
                light = SRGBToLinear(flower);
                dark = light * 0.78;
                radius *= 0.8;
            }
            else if (biome == 3)
            {
                fade = 1 - grassShow;
                dark = albedo * float3(0.7, 0.84, 0.6);
                light = albedo * float3(1.1, 1.1, 0.82);
            }
            else if (biome == 5 && pick < 0.3)
            {
                light = lerp(albedo, SRGBToLinear(float3(0.74, 0.56, 0.34)), 0.6);
                dark = light * 0.8;
                v.y *= 1.6;
            }
            else if (biome == 5)
            {
                fade = 1 - grassShow;
                dark = albedo * float3(0.68, 0.8, 0.66);
                light = albedo * float3(1.12, 1.14, 0.86);
            }
            else if (biome == 2)
            {
                dark = SRGBToLinear(float3(0.82, 0.72, 0.68));
                light = SRGBToLinear(float3(0.92, 0.9, 0.86));
                radius *= 0.6;
            }
            else
            {
                dark = lerp(SRGBToLinear(float3(0.52, 0.5, 0.47)), albedo, 0.3);
                light = SRGBToLinear(float3(0.8, 0.78, 0.74));
                v.y *= 1.3;
            }
            float d = length(v);
            float cover = 1 - smoothstep(radius - px * 0.5, radius + px * 0.5, d);
            // Flat underneath, like a tuft or a stone sitting on the ground.
            cover *= smoothstep(-radius * 0.4 - px * 0.5, -radius * 0.4 + px * 0.5, v.y);
            cover *= smoothstep(0.3, 0.75, radius / px) * fade;
            float top = smoothstep(-px * 0.5, px * 0.5, v.y - radius * 0.2);
            return lerp(albedo, lerp(dark, light, top), cover * land);
        }

        // Small things scattered on the ground, one chance per square of a jittered grid: flowers
        // on meadows, dry tufts on the steppe, fallen leaves in forests, pebbles on beaches, hills
        // and mountains; and a fine grain of light and shade on grass. They gather in clusters
        // (flowers in tighter ones) and fade out as they shrink below a pixel, so far views stay
        // calm. The meadows' flowers stand over the grass and everything else lies under it, so
        // the ground takes two calls, before the grass and after it (flowers). Where standing
        // grass shows (grassShow, 0..1) the flat tufts and the forest's dots give way to it; dry
        // is Variety's dry patch, for Clumps.
        float3 Details(float3 albedo, float2 p, float px, float land, float grassShow, bool flowers, float dry)
        {
            int biome = RealmsCellAt(RealmsHexAt(p)).y;
            if (biome == 0)
                biome = (int)_RealmsFieldBiome;
            if (flowers && biome != 3)
                return albedo;
            float cluster = RealmsFbm(p * 0.8 + 23.9);
            if (!flowers)
            {
                bool green = biome == 3 || biome == 4 || biome == 5 || biome == 6;
                float grain = RealmsNoise(p * float2(23, 8)) - 0.5;
                float grainFade = (1 - smoothstep(0.02, 0.05, px)) * (1 - _GroundDetail);
                albedo *= 1 + (green ? 0.12 : 0.06) * grain * grainFade * land;
                albedo = Clumps(albedo, p, px, biome, land, cluster, grassShow, dry);
                if (biome == 3)
                    return albedo;
            }

            float density = biome == 3 ? 0.22 : biome == 4 ? 0.26 : biome == 5 ? 0.3 : biome == 6 ? 0.18
                : biome == 2 ? 0.14 : biome == 7 ? 0.12 : 0;
            density *= biome == 3 ? Gathered(RealmsFbm(p * 1.3 + 61.7) + cluster * 0.5, 0, 2.4) : Gathered(cluster, 0.15, 1.8);
            const float g = 0.07;
            float2 id = floor(p / g);
            float2 f = p / g - id;
            float r1 = RealmsHash(id + 0.31), r2 = RealmsHash(id * 1.7 + 3.1), r3 = RealmsHash(id * 0.61 + 7.7);
            if (r1 > density * _Details || land < 0.5)
                return albedo;
            float2 c = 0.25 + 0.5 * float2(r2, r3);
            float2 v = (f - c) * g;
            float3 color;
            float radius = g * (0.13 + 0.07 * r3);
            float pick = frac(r1 * 7.31 + r2 * 3.7);
            float fade = biome == 4 || biome == 5 ? 1 - grassShow : 1;
            if (biome == 3)
            {
                // Flowers in soft colours, mostly white and pale yellow, a few pink and violet.
                color = pick < 0.4 ? float3(0.9, 0.89, 0.84) : pick < 0.72 ? float3(0.86, 0.75, 0.4)
                    : pick < 0.88 ? float3(0.8, 0.56, 0.6) : float3(0.62, 0.57, 0.78);
                color = SRGBToLinear(color);
            }
            else if (biome == 4)
            {
                // Dry tufts: soft, a little longer than wide, each turned its own way so they do
                // not line up in strokes.
                float turn = frac(r1 * 13.7 + r3 * 5.1) * 3.1416;
                float2 axis = float2(cos(turn), sin(turn));
                v = float2(dot(v, axis), dot(v, float2(-axis.y, axis.x)));
                v.x /= 1.35;
                color = albedo * (pick < 0.5 ? float3(1.12, 1.05, 0.88) : float3(0.84, 0.8, 0.68));
            }
            else if (biome == 5)
                color = albedo * (pick < 0.5 ? float3(0.7, 0.78, 0.6) : float3(1.25, 1.2, 0.7));
            else
            {
                // Pebbles: grey or light, a little squashed.
                v.y *= 1.4;
                color = SRGBToLinear(pick < 0.5 ? float3(0.64, 0.62, 0.58) : float3(0.86, 0.84, 0.8));
                color = lerp(color, albedo, biome == 7 ? 0.5 : 0.2);
            }
            float d = length(v);
            float cover = 1 - smoothstep(radius - px * 0.5, radius + px * 0.5, d);
            cover *= smoothstep(0.25, 0.7, radius / px) * fade;
            return lerp(albedo, color, cover * land);
        }

        // Grass standing on the green ground, drawn per pixel with no blades built: the view ray
        // is traced down through the grass in thin layers, front to back, and each layer asks
        // which blade, if any, the ray passes through at its height, so the blades stand up and
        // the nearer ones hide those behind. The blades stand on a jittered grid, at most one in
        // a square, each a slim cone whose tip leans with the wind (RealmsGrassWind).

        // One layer of the grass at height h: how much of the pixel's ray there (q) the blades of
        // a grid g world units apart cover, 0..1, and of the blade covering most of it how far up
        // it the layer lies (up, 0 at the root, 1 at the tip), its own shade (seed, 0..1) and the
        // side of it the ray meets, towards the sun or away (side, -1..1). A blade stands up to
        // tall high, width times g in radius at the root; rooted away from its square's edges
        // and never reaching far into the next squares, so the four squares round q's nearest
        // grid corner hold every blade that can cover q. aa softens the edges, in world units.
        float GrassLayer(float2 q, float g, float h, float tall, float width, float density, uint salt,
            float2 sun, float aa, out float up, out float seed, out float side)
        {
            float2 base = floor(q / g - 0.5);
            float best = 0;
            up = 0;
            seed = 0;
            side = 0;
            [unroll]
            for (int k = 0; k < 4; k++)
            {
                float2 id = base + float2(k & 1, k >> 1);
                float4 r = RealmsHash4(id, salt);
                float rel = h / (tall * (0.55 + 0.45 * r.w));
                if (r.x > density || rel >= 1)
                    continue;
                float2 o = q - (id + 0.15 + 0.7 * r.yz) * g;
                float d = length(o);
                float radius = width * g * sqrt(1 - rel);
                float cover = 1 - smoothstep(radius - aa, radius + aa, d);
                if (cover > best)
                {
                    best = cover;
                    up = rel;
                    seed = r.w;
                    side = dot(o, sun) / max(d, 1e-6);
                }
            }
            return best;
        }

        // The grass of one grid over a pixel: the view ray (p where it meets the ground, ray how
        // far it runs along the ground per unit of height towards the camera) traced from the
        // grass's top down in the given number of layers, the tips leaning by lean (blade
        // heights), each blade coloured from root to tip and lighter on its sunny side; under
        // where the ray reaches the ground. A blade shows whole only while the ray moves less
        // than about its width from one layer to the next; with too few layers it breaks up
        // into stacked dashes where the camera looks at the grass from low down.
        float3 GrassLevel(float3 under, float3 root, float3 tip, float2 p, float2 ray, float2 lean,
            float g, float width, float tall, float density, uint salt, float2 sun, float aa, int layers)
        {
            float3 color = 0;
            float alpha = 0;
            [loop]
            for (int i = layers - 1; i >= 0; i--)
            {
                float h = (i + 0.5) / layers * tall;
                float2 q = p + ray * h - lean * (h * h / tall);
                float up, seed, side;
                float cover = GrassLayer(q, g, h, tall, width, density, salt, sun, aa, up, seed, side);
                float3 blade = lerp(root, tip, smoothstep(0, 1, up)) * (0.9 + 0.2 * seed) * (1 + 0.15 * side);
                color += (1 - alpha) * cover * blade;
                alpha += (1 - alpha) * cover;
                if (alpha > 0.97)
                    break;
            }
            return color + (1 - alpha) * under;
        }

        // The blades' width at the root, in grid squares: the fine blades close up and the coarse
        // ones further out, which stand in for several of the fine ones.
        #define GRASS_FINE_WIDTH 0.32
        #define GRASS_COARSE_WIDTH 0.26

        // How much the blades of a grid g world units apart show at a pixel px wide (fwidth's
        // length, about 1.75 pixels): they come in as their roots grow from 1.2 to 2 pixels wide.
        // Thinner, they would crawl and flicker as the wind moves them.
        float GrassShow(float g, float width, float px)
        {
            return smoothstep(1.2, 2, 2 * width * g * 1.75 / px);
        }

        // The grass over a pixel of the ground. amount (0..1) is how tall and dense it grows
        // there, dry (0..1) how much of it is straw-coloured, wind RealmsGrassWind there. The
        // blades take the ground's colour (ground), lighter towards the tips, and the wind's
        // waves turn them only a little lighter and greyer as the bent grass shows its paler side
        // and hides more of the ground: never towards straw, so a wave is not taken for a dry
        // patch (review of 2026-11-10: they lightened by half towards pale straw), and keeping
        // their green, as at a quarter lighter and a third of the way to a cool grey the players
        // saw them as white streaks running over the meadows; between them
        // the ground with its small things (under) shows, in the grass's shade. Close up the fine
        // blades show (showFine), further out the coarse ones (showCoarse), over the colour of
        // the fine ones they leave out; further still the grass is its mean colour, with the
        // waves running over it. All three keep the same brightness on average, so no seam
        // shows where one gives way to the next.
        float3 Grass(float3 ground, float3 under, float2 p, float px, float3 view, float amount, float dry,
            float3 wind, float showFine, float showCoarse)
        {
            float tall = _GrassHeight * amount;
            float density = saturate(amount * 1.4);
            float3 tip = ground * lerp(float3(1.22, 1.2, 1.03), float3(1.32, 1.22, 0.9), dry);
            tip = lerp(tip, dot(tip, float3(0.2126, 0.7152, 0.0722)), 0.1 * wind.z) * (1 + 0.12 * wind.z);
            float3 root = ground * 0.82;
            float3 shaded = under * lerp(1, 0.8, density);
            // A blade's colour on average as seen from above, and the share of the ground the
            // blades hide (more where the wind bends them over).
            float3 blade = lerp(root, tip, 0.45);
            float3 mean = lerp(shaded, blade, saturate(0.55 * density * (1 + 0.2 * wind.z)));
            float3 color = mean;
            float2 ray = view.xz / max(view.y, 0.35);
            float2 sun = normalize(_MainLightPosition.xz + 1e-4);
            float aa = px * 0.35;
            [branch]
            if (showCoarse > 0 && showFine < 1)
                color = lerp(mean, GrassLevel(lerp(shaded, blade, 0.35 * density), root, tip, p, ray, wind.xy,
                    _GrassCoarse, GRASS_COARSE_WIDTH, tall, density, 1u, sun, aa, 8), showCoarse);
            // The fine blades are traced in twice as many layers, as the town and hero screens
            // look at them from low down. More of their light upper parts shows then than of the
            // coarse blades', so they are a little darker, to keep the same brightness on average.
            [branch]
            if (showFine > 0)
                color = lerp(color, GrassLevel(shaded, root * 0.926, tip * 0.926, p, ray, wind.xy,
                    _GrassBlades, GRASS_FINE_WIDTH, tall, density, 2u, sun, aa, 16), showFine);
            return color;
        }

        // The square's paving out to radius: cobbles (the cobble layer, a tile of _CobbleTile world
        // units, so a square holds a dozen stones or so across), the outline broken where the
        // stones stand proud of the earth between them. dx and dy are p's screen gradients.
        float3 Paving(float3 albedo, float2 p, float2 local, float radius, float px, float2 dx, float2 dy)
        {
            float r = length(local) + (RealmsNoise(p * 9) - 0.5) * 0.05;
            if (r > radius + 0.04 + px)
                return albedo;
            float inv = 1 / _CobbleTile;
            float4 cobble = GroundSample(p * inv, dx * inv, dy * inv, GROUND_COBBLE);
            float inside = 1 - smoothstep(radius - px, radius + px, r - (cobble.a - 1) * 0.02);
            // Nearly the texture's full contrast, and the joints between the stones darker still,
            // so the paving reads as stones and not as a pale disc.
            float3 stones = lerp(1, cobble.rgb, min(1, 1.2 * _GroundDetail)) * lerp(0.84, 1, smoothstep(0.5, 1, cobble.a));
            return lerp(albedo, _Paving.rgb * stones, inside);
        }

        // A town's ground (flag 2): trodden earth round the middle and lanes out to the town's
        // places, the hexagon's corners (all six in a quarter, all but the front one in the centre,
        // flag 16, where the square is), with trampled grass along them; the earth carries the dirt
        // layer's pattern. From level 2 (the level less one is in bits 5-6) the centre's square is
        // paved, and it grows with the town. dx and dy are p's screen gradients.
        float3 TownGround(float3 albedo, float2 p, float px, float2 dx, float2 dy)
        {
            float2 q = p + (float2(RealmsNoise(p * 3.1), RealmsNoise(p * 3.1 + 7.7)) - 0.5) * 0.05;
            int2 h = RealmsHexAt(q);
            int flags = RealmsCellAt(h).z;
            if ((flags & 2) == 0)
                return albedo;
            bool centre = (flags & 16) != 0;
            float level = ((flags >> 5) & 3) + 1;
            float2 local = q - RealmsCenter(h);
            float r = length(local);
            float square = centre ? 0.22 + 0.035 * level : 0.17;
            float d = r - square;
            [unroll]
            for (int k = 0; k < 6; k++)
            {
                if (centre && k == 4)
                    continue;
                float a = radians(30 + 60 * k);
                float2 dir = float2(cos(a), sin(a));
                float t = clamp(dot(local, dir), 0, 0.5);
                d = min(d, length(local - dir * t) - lerp(0.03, 0.02, t * 2));
            }
            float earth = 1 - smoothstep(-px * 0.5, 0.01 + px * 0.5, d);
            float trodden = 1 - smoothstep(0, 0.035 + px, d);
            float3 soil = GroundFlat(p, dx, dy, GROUND_DIRT, _GroundScale * _LayerTilesB.w, RealmsNoise(p * 0.45 + 17.3)).rgb;
            float3 dirt = lerp(_RoadEdge.rgb, _Road.rgb, 0.55) * lerp(1, soil, _GroundDetail);
            float3 color = lerp(albedo, lerp(albedo, _RoadEdge.rgb, 0.35), trodden * 0.6);
            color = lerp(color, dirt, earth);
            if (centre && level >= 2)
                color = Paving(color, p, local, square - 0.045, px, dx, dy);
            return color;
        }

        // The road strip's period along a road: a third of the way from a cell's centre to an
        // edge, so a whole number of periods lies between the middles of the two edges a road
        // joins in a cell, straight or bent, and the strip meets itself at every edge.
        #define ROAD_PERIOD (REALMS_INNER / 3)

        // The road strip u along a road and v from its middle line (world units), tangent the
        // road's way there and span its full width. Both halves of the strip are the same half
        // (v's sign does not count), so it meets itself at an edge however the cells on either
        // side measure along and across. dx and dy are the ground's screen gradients.
        float4 RoadStrip(float u, float v, float2 tangent, float span, float2 dx, float2 dy)
        {
            float2 across = float2(-tangent.y, tangent.x);
            float2 uv = float2(u / ROAD_PERIOD, (abs(v) + span * 0.5) / (2 * span));
            float2 duvx = float2(dot(dx, tangent) / ROAD_PERIOD, dot(dx, across) / (2 * span));
            float2 duvy = float2(dot(dy, tangent) / ROAD_PERIOD, dot(dy, across) / (2 * span));
            return GroundSample(uv, duvx, duvy, GROUND_ROAD);
        }

        // A road passing through a cell with nothing in its middle, in at the middle of edge a and
        // out at the middle of edge b (a < b, not opposite): square to each edge, it turns between
        // them in an arc round the inside of the turn, the whole way across for a gentle turn
        // (edges two apart), and for a sharp one (edges side by side) a tighter arc nearer the
        // middle with straight arms out to the edges, where the bridges cross the rivers.
        // RoadShape.cs follows the same line for the props. d is the distance from the road's
        // middle line, u the way along it from a's edge, scaled to a whole number of strip
        // periods at b's, tangent its way there.
        void RoadBend(float2 local, int a, int b, out float d, out float u, out float2 tangent)
        {
            float2 ea = RealmsNormals[a], eb = RealmsNormals[b];
            bool sharp = dot(ea, eb) > 0;
            // With half the angle between the arms 30 degrees (sharp) or 60 (gentle): the arc's
            // radius and angle, its centre's distance from the cell's (radius / sin) and its
            // ends' (where the arms end, radius / tan).
            float radius = sharp ? 0.35 : 1.5, angle = sharp ? 2.0943951 : 1.0471976;
            float2 f = normalize(ea + eb) * (sharp ? 0.7 : 1.7320508);
            float inner = sharp ? 0.6062178 : REALMS_INNER;
            float arm = REALMS_INNER - inner, total = 2 * arm + radius * angle;
            float scale = max(1, round(total / ROAD_PERIOD)) * ROAD_PERIOD / total;
            float ta = clamp(dot(local, ea), inner, REALMS_INNER), tb = clamp(dot(local, eb), inner, REALMS_INNER);
            float da = distance(local, ea * ta), db = distance(local, eb * tb);
            float2 w = local - f, wa = ea * inner - f, wb = eb * inner - f;
            float r = max(length(w), 1e-5);
            float turn = wa.x * wb.y - wa.y * wb.x;
            bool onArc = (wa.x * w.y - wa.y * w.x) * turn >= 0 && (w.x * wb.y - w.y * wb.x) * turn >= 0;
            float dArc = onArc ? abs(r - radius) : 10;
            if (dArc <= min(da, db))
            {
                d = dArc;
                u = (arm + radius * acos(clamp(dot(wa, w) / (radius * r), -1, 1))) * scale;
                tangent = float2(-w.y, w.x) / r;
            }
            else if (da <= db)
            {
                d = da;
                u = (REALMS_INNER - ta) * scale;
                tangent = ea;
            }
            else
            {
                d = db;
                u = (total - (REALMS_INNER - tb)) * scale;
                tangent = eb;
            }
        }

        // Dirt roads across the middles of the edges in a cell's road mask, square to them. A road
        // passing through bends round in an arc (RoadBend). In a hub (bit 64 of the mask: a town,
        // a resource or a place stands in the middle), at a road's end and where three or more
        // meet, they run straight to the cell's centre; where roads of different lines meet there,
        // the inner corner is rounded off and the two nearest strips cross-fade along the line
        // between them. A neighbour's road meets each at the shared edge going the same way, and a
        // gentle warp of the ground bends both alike, so the roads run on without seams; so does
        // the road strip (ruts, a grassy middle, dusty shoulders), which comes round whole at every
        // edge's middle. The shoulders fade into the ground over the rim, raggedly: the strip's
        // higher parts (stones, tufts) reach further. dx and dy are p's screen gradients.
        float3 Roads(float3 albedo, float2 p, float px, float2 dx, float2 dy)
        {
            float2 q = p + (float2(RealmsNoise(p * 2.1), RealmsNoise(p * 2.1 + 19.7)) - 0.5) * 0.1;
            int2 h = RealmsHexAt(q);
            int mask = RealmsCellAt(h).w;
            int roads = mask & 63;
            if (roads == 0)
                return albedo;
            float2 local = q - RealmsCenter(h);
            // Far away a road keeps about three pixels of width (px is the pixel's footprint
            // along both screen axes, about one and a half pixels across).
            const float rim = 0.027;
            float width = max(0.06, px), span = 2 * (width + rim), outside = width + rim + 0.04 + px;
            // The first two roads, and whether more leave the cell.
            int a = -1, b = -1, more = 0;
            [unroll]
            for (int k = 0; k < 6; k++)
            {
                if (((roads >> k) & 1) == 0)
                    continue;
                if (a < 0)
                    a = k;
                else if (b < 0)
                    b = k;
                else
                    more = 1;
            }
            float d;
            float4 strip = 1;
            [branch]
            if ((mask & 64) == 0 && more == 0 && b >= 0 && b - a != 3)
            {
                float u;
                float2 tangent;
                RoadBend(local, a, b, d, u, tangent);
                if (d > outside)
                    return albedo;
                strip = RoadStrip(u, d, tangent, span, dx, dy);
            }
            else
            {
                // The nearest road and the next nearest, with their lines.
                d = 10;
                float next = 10;
                float2 axis = float2(1, 0), nextAxis = float2(1, 0);
                [unroll]
                for (int j = 0; j < 6; j++)
                {
                    if (((roads >> j) & 1) == 0)
                        continue;
                    float2 dir = RealmsNormals[j];
                    // One axis for both halves of a straight road: RealmsNormals[j + 3] is
                    // -RealmsNormals[j].
                    float2 along = j < 3 ? dir : -dir;
                    float t = clamp(dot(local, dir), 0, REALMS_INNER);
                    float dj = length(local - dir * t);
                    if (dj < d)
                    {
                        next = d;
                        nextAxis = axis;
                        d = dj;
                        axis = along;
                    }
                    else if (dj < next)
                    {
                        next = dj;
                        nextAxis = along;
                    }
                }
                float blend = 0.5 * (1 - smoothstep(0, 0.03, next - d));
                bool bend = any(nextAxis != axis);
                if (bend)
                {
                    // The inner corner where roads of different lines meet is rounded off (a smooth
                    // minimum of their distances), so a road turns rather than kinks.
                    const float fillet = 0.08;
                    float share = saturate(0.5 + 0.5 * (next - d) / fillet);
                    d = lerp(next, d, share) - fillet * share * (1 - share);
                }
                if (d > outside)
                    return albedo;
                strip = RoadStrip(dot(local, axis), dot(local, float2(-axis.y, axis.x)), axis, span, dx, dy);
                [branch]
                if (blend > 0 && bend)
                    strip = lerp(strip, RoadStrip(dot(local, nextAxis), dot(local, float2(-nextAxis.y, nextAxis.x)),
                        nextAxis, span, dx, dy), blend);
            }
            strip.rgb = lerp(1, strip.rgb, _GroundDetail);
            float reach = d - (strip.a - 1) * 0.015;
            float trampled = 1 - smoothstep(width, width + rim + 0.02 + px * 0.5, reach);
            float cover = 1 - smoothstep(width * 0.8 - px * 0.5, width + rim + px * 0.5, reach);
            float3 color = lerp(albedo, lerp(albedo, _RoadEdge.rgb, 0.4), trampled * 0.6);
            return lerp(color, _Road.rgb * strip.rgb, cover);
        }

        half4 Forward(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float3 n = normalize(input.normalWS);
            float2 p = input.positionWS.xz;
            float land = smoothstep(-0.05, 0.02, input.positionWS.y);
            float px = max(length(fwidth(p)), 1e-4);
            float3 albedo = input.color.rgb;
            float bare, lush, dry;
            float green = saturate(input.layersA.x + input.layersA.y + input.layersA.z);
            albedo = Variety(albedo, p, green, land, bare, lush, dry);
            // The textures' pattern, on land and the shallow sea floor: strong in some stretches,
            // faint in others and on bare earth, always strong on stone.
            float calm = lerp(0.3, 1, smoothstep(-0.25, 0.3, RealmsFbm(p * 0.35 + 77.7)));
            calm = lerp(calm, 1, saturate(input.layersB.x + input.layersB.y + smoothstep(0.86, 0.7, n.y)));
            float patterned = _GroundDetail * smoothstep(-0.5, 0, input.positionWS.y) * calm * (1 - 0.7 * bare);
            float2 dpx = ddx(p), dpy = ddy(p);
            [branch]
            if (patterned > 0)
                albedo *= lerp(1, GroundPattern(p, dpx, dpy, input.layersA, input.layersB, n), patterned);
            // The grass: tall and dense on meadows, steppe and hills, low and sparse on the forest
            // floor, none on sand, stone, steep slopes, bare earth or under water; taller in some
            // stretches than in others, in lush patches more than in dry ones. Its blades show on
            // seen land only, as the fog covers the rest; off the map (the battle
            // field) nothing counts as seen, but all is in view.
            float ground = smoothstep(0.03, 0.08, input.positionWS.y);
            float grass = saturate(input.layersA.x + input.layersA.y + 0.35 * input.layersA.z)
                * (1 - bare) * (1 - smoothstep(0.86, 0.7, n.y)) * ground * _Grass;
            grass *= saturate(lerp(0.6, 1.1, smoothstep(-0.25, 0.25, RealmsFbm(p * 0.8 + 31.3))) + 0.15 * lush - 0.25 * dry);
            // On the battle field the waves are half as strong: the fight is what to look at there.
            float3 wind = RealmsGrassWind(p, px) * float3(1, 1, _RealmsFieldBiome > 0.5 ? 0.5 : 1);
            float showFine = 0, showCoarse = 0;
            [branch]
            if (grass > 0.02)
            {
                float seen = RealmsCellAt(RealmsHexAt(p)).y == 0 ? 1 : smoothstep(0.05, 0.5, RealmsSeen(p).x);
                showFine = GrassShow(_GrassBlades, GRASS_FINE_WIDTH, px) * seen;
                showCoarse = GrassShow(_GrassCoarse, GRASS_COARSE_WIDTH, px) * seen;
            }
            float3 bladeGround = albedo;
            albedo = Details(albedo, p, px, ground, max(showFine, showCoarse) * saturate(grass * 3), false, dry);
            [branch]
            if (grass > 0.02)
            {
                float straw = saturate(input.layersA.y / max(green, 1e-3) * 0.8 + dry * 0.6);
                albedo = Grass(bladeGround, albedo, p, px, GetWorldSpaceNormalizeViewDir(input.positionWS), grass,
                    straw, wind, showFine, showCoarse);
            }
            albedo = Details(albedo, p, px, ground, 0, true, dry);
            albedo = Fields(albedo, p, px, wind.z);
            albedo = TownGround(albedo, p, px, dpx, dpy);
            albedo = Roads(albedo, p, px, dpx, dpy);
            albedo = RealmsTint(albedo, p, _Tint * land);
            // Houses, walls and trunks stand in the ground rather than on it: a soft shade round
            // their feet, about a quarter darker at the foot (review of 2026-11-10).
            albedo *= 1 - _ContactShade * Contact(p);
            // Gentle slopes catch the light. The sun stands so high that RealmsLit's ramp is full on
            // all ground tilted less than about twenty degrees away from it, so the land's low rolls
            // showed no light and shade at all and the meadows read as flat and bare (2026-11-10).
            // Slopes facing the sun lighten here, by a tenth at most, and those turned away darken,
            // by a seventh, fading out on steep ground, which the ramp shades already. At half this
            // strength the rolls changed the light by about a twentieth and still read as flat.
            float3 sun = GetMainLight().direction;
            float tilt = dot(n, sun) - sun.y;
            albedo *= 1 + clamp(tilt * 2, -0.14, 0.1) * smoothstep(0.8, 0.95, n.y) * land;
            float3 color = RealmsLit(albedo, n, input.positionWS, 0);
            float3 emission = 0;
            float coverage = 0;
            color = RealmsLines(color, p, land, 1, RealmsHudShow(input.positionCS), emission, coverage);
            return half4(color + emission, 1);
        }

        half4 Depth(Varyings input) : SV_Target { return 0; }

        half4 DepthNormal(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            float3 n = normalize(input.normalWS);
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
