#ifndef REALMS_GROUND_INCLUDED
#define REALMS_GROUND_INCLUDED

// The ground textures (Realms.Editor.Setup builds the array from Art/Textures): each layer is
// stored divided by its own mean colour, at half scale, with its brightness as height in alpha.
// Multiplying a palette colour by a layer's pattern keeps the colour and adds the texture's grass
// blades, stones and cracks; far away the mipmaps fade the pattern to 1. Include after
// RealmsCommon.hlsl.

#define GROUND_GRASS 0
#define GROUND_DRYGRASS 1
#define GROUND_FOREST 2
#define GROUND_SAND 3
#define GROUND_SCREE 4
#define GROUND_ROCK 5
#define GROUND_SNOW 6
#define GROUND_DIRT 7
#define GROUND_LAYERS 8
// After the ground's own layers (GROUND_LAYERS, which the terrain's vertices carry shares of): the
// cobbles of the towns' squares and the road strip, a band along the road repeated twice down its
// layer (v 0..0.5 is the road's width once, as long as twice that along u).
#define GROUND_COBBLE 8
#define GROUND_ROAD 9

TEXTURE2D_ARRAY(_Ground);
SAMPLER(sampler_Ground);

// One layer at one projection: pattern (rgb, 1 on average) and height (a, 1 on average). Explicit
// gradients, so it can be sampled under branches.
float4 GroundSample(float2 uv, float2 dx, float2 dy, int layer)
{
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(_Ground, sampler_Ground, uv, layer, dx, dy) * 2;
}

// A layer laid flat on the ground at two scales and angles, taking whichever stands higher where
// a slow noise (mix, 0..1) hands over between them, so the tiles do not repeat in rows.
float4 GroundFlat(float2 p, float2 dx, float2 dy, int layer, float scale, float mix)
{
    const float2x2 turn = float2x2(0.8, -0.6, 0.6, 0.8);
    float inv = 1 / scale, invB = 1 / (scale * 1.37);
    float4 a = GroundSample(p * inv, dx * inv, dy * inv, layer);
    float4 b = GroundSample(mul(turn, p) * invB + 0.37, mul(turn, dx) * invB, mul(turn, dy) * invB, layer);
    float t = saturate((b.a - a.a) * 1.5 + (mix - 0.5) * 6 + 0.5);
    return lerp(a, b, t);
}

// A layer on a solid seen from any side: blended from the three axis projections by the normal.
// Sides use the layer given; tops use the layer for flat ground (scree on top of rock). dx and dy
// are q's screen gradients, taken outside any branch.
float4 GroundTriplanar(float3 q, float3 dx, float3 dy, float3 n, int side, int top)
{
    float3 w = pow(abs(n), 4);
    w /= w.x + w.y + w.z;
    float4 x = GroundSample(q.zy, dx.zy, dy.zy, side);
    float4 z = GroundSample(q.xy, dx.xy, dy.xy, side);
    float4 y = GroundSample(q.xz, dx.xz, dy.xz, top);
    return x * w.x + y * w.y + z * w.z;
}

#endif
