#ifndef RIBBON_INCLUDED
#define RIBBON_INCLUDED

// The ribbon. Each vertex of the world knows its place on the story's curve
// (its second UVs: s along it, t to its right, 1, the road's heading there;
// 0 in the third: not of the story, drawn where it is). Its distance ds along
// the curve from the camera's focus decides:
// - near (-pastFlat to flat): drawn where it is, the world as it is;
// - ahead: on the ribbon that RibbonBend.cs samples each frame. Its length u
//   beyond the flat part is pressed into the curve's finite length A,
//   a = A (1 - e^(-u/A)), and everything there is scaled by da/du = e^(-u/A)
//   every way, so shapes keep their form (conformal), only smaller; at the
//   flat part's end the scale is 1 and the curve is the road there. Heights
//   are pressed further as it rises (the samples' w), into a relief: a tall
//   thing on a sharp rise would fold over itself;
//   Or, in the sky (_RibbonWindow.w = 1), laid along it as it runs, a = u,
//   unscaled: the curve itself is as long as the story left (A), its
//   samples' run and widths saying how it is stretched;
// - behind: shrunk away into a point under the ground.
// A vertex shrunk to almost nothing either way is gone (Ribbonize returns
// false): the far future's tip and the far past's point are far apart, and a
// triangle across the window's seam would stretch between them.

// The curve comes as a texture one texel high: RIBBON_SAMPLES points, then as
// many tangents, then sides (arrays this long would not fit a vertex
// shader's uniforms; one row, so no API flips it).
#define RIBBON_SAMPLES 1024

float4 _RibbonParams;                    // focus s, curve length A, flat, loop length
float4 _RibbonWindow;                    // the past's reach, its flat part, its shrinking's length, 1: laid as it runs (the sky)
float4 _RibbonPast;                      // the point the past shrinks to
// Row 0: the curve's points (w: the heights' share kept); row 1: its forward,
// unit (w: metres of curve a metre of its length's measure); row 2: its
// right, unit, square to the forward (w: the width's share kept); row 3, in
// the sky: x its own metres from its start, y its width's share.
TEXTURE2D_FLOAT(_RibbonCurveMap);
float4 RibbonSample(int i, int row) { return LOAD_TEXTURE2D_LOD(_RibbonCurveMap, int2(clamp(i, 0, RIBBON_SAMPLES - 1) + row * RIBBON_SAMPLES, 0), 0); }
float _StoryTurn;                        // radians the level is turned where it is shown (its road headings with it)

// The vertex's distance along the story from the camera's focus, round the
// loop into [-reach, loop - reach); 0 for what is not of the story.
float RibbonDistance(float4 story)
{
    float loop = _RibbonParams.w;
    if (story.z < 0.5 || _RibbonParams.y <= 0.0 || loop <= 0.0) return 0.0;   // not of the story, or not set (the editor out of play)
    float ds = story.x - _RibbonParams.x;
    return ds - loop * floor((ds + _RibbonWindow.x) / loop);
}

// On the ribbon ahead: drawn but casting no shadow on the world near.
bool RibbonAhead(float4 story) { return RibbonDistance(story) > _RibbonParams.z; }

// How far along the curve u metres past its start lie, m.
float RibbonArc(float u)
{
    u = max(u, 0.0);
    return _RibbonWindow.w > 0.5 ? min(u, _RibbonParams.y) : _RibbonParams.y * (1.0 - exp(-u / _RibbonParams.y));
}

// How much a thing u metres past the start is scaled down there.
float RibbonShrink(float u) { return _RibbonWindow.w > 0.5 ? (u <= _RibbonParams.y ? 1.0 : 0.0) : exp(-u / _RibbonParams.y); }

// The sky's ribbon's own measures u metres of the story past its start: its
// metres from its start, its width's share.
float2 RibbonMeasure(float u)
{
    float f = saturate(RibbonArc(u) / _RibbonParams.y) * (RIBBON_SAMPLES - 1);
    int i = min((int)f, RIBBON_SAMPLES - 2);
    return lerp(RibbonSample(i, 3).xy, RibbonSample(i + 1, 3).xy, f - i);
}

// A place on the ribbon: u metres past its start along the story, t to the
// right, h up, its normal turned from the road's frame (the road's heading
// there) into the ribbon's. False: shrunk to nothing.
bool RibbonAt(float u, float4 story, inout float3 positionWS, inout float3 normalWS)
{
    float scale = RibbonShrink(u);
    if (scale < 1e-3) return false;
    float f = saturate(RibbonArc(u) / _RibbonParams.y) * (RIBBON_SAMPLES - 1);
    int i = min((int)f, RIBBON_SAMPLES - 2);
    float k = f - i;
    // Through the samples smoothly (Catmull-Rom): no corners where the coils are tight.
    float4 p0 = RibbonSample(i - 1, 0), p1 = RibbonSample(i, 0), p2 = RibbonSample(i + 1, 0), p3 = RibbonSample(i + 2, 0);
    float k2 = k * k, k3 = k2 * k;
    float4 c = 0.5 * (2.0 * p1 + (p2 - p0) * k + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * k2 + (3.0 * p1 - p0 - 3.0 * p2 + p3) * k3);
    float4 along = lerp(RibbonSample(i, 1), RibbonSample(i + 1, 1), k);
    float4 side = lerp(RibbonSample(i, 2), RibbonSample(i + 1, 2), k);
    float3 t = normalize(along.xyz);
    float3 x = normalize(side.xyz - t * dot(side.xyz, t));
    scale *= along.w;   // kept conformal where the curve runs faster than its measure
    float3 up = cross(t, x);

    float3 roadForward = float3(sin(story.w + _StoryTurn), 0, cos(story.w + _StoryTurn));
    float3 roadRight = float3(roadForward.z, 0, -roadForward.x);
    float3 n = float3(dot(normalWS, roadRight), normalWS.y, dot(normalWS, roadForward));
    positionWS = c.xyz + (story.y * side.w * x + positionWS.y * c.w * up) * scale;
    normalWS = normalize(n.x * x + n.y * up + n.z * t);
    return true;
}

bool Ribbonize(inout float3 positionWS, inout float3 normalWS, float4 story)
{
    float ds = RibbonDistance(story);
    float flat = _RibbonParams.z;
    if (ds > flat) return RibbonAt(ds - flat, story, positionWS, normalWS);
    if (ds < -_RibbonWindow.y)
    {
        float k = exp((ds + _RibbonWindow.y) / _RibbonWindow.z);
        if (k < 1e-3) return false;
        positionWS = _RibbonPast.xyz + (positionWS - _RibbonPast.xyz) * k;
    }
    return true;
}

// A clip-space position no triangle is drawn with.
float4 Gone() { return asfloat(0x7fc00000).xxxx; }

#endif
