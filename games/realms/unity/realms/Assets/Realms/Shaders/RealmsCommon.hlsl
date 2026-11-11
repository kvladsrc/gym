#ifndef REALMS_COMMON_INCLUDED
#define REALMS_COMMON_INCLUDED

// Shared by the terrain, water, prop, smoke, cloud and fog shaders: the wind, how far the players
// have seen and the drifting clouds, the light model and the map overlay.
// Include after URP's Core.hlsl and Lighting.hlsl.
//
// Colours are meant as they look on screen: the volume uses no tonemapping, so a lit face facing
// the sun shows its albedo exactly and a face turned away shows a cool shade of it.
//
// The overlay reads the cell texture MapView uploads (_RealmsCells, one texel per cell in odd-r
// offset layout: r = owner + 1, g = biome, b = flags, a = road mask) and draws, per pixel, the territory tint,
// the faint grid, the kingdoms' borders (each side in its own colour) and the hover and selection
// rings, and the growing border's marks (BorderMarks). Hexagons are pointy-top with unit radius,
// as in Realms.Core.HexMetrics.

TEXTURE2D(_RealmsCells);
TEXTURE2D(_RealmsFog);           // how far each cell is seen (see RealmsSeen)
float4 _RealmsMapSize;          // width, height, 1/width, 1/height
float4 _RealmsCountryColors[8]; // linear
float4 _RealmsHover;            // axial q, r, enabled
float4 _RealmsSelect;           // axial q, r, enabled
TEXTURE2D(_RealmsMarks);         // the growing border's marks, laid out like _RealmsCells (see RealmsMarkAt)
float4 _RealmsMarkShow;          // x 1 while the other kingdoms' marks show (Alt), y when the players' border last took cells (_Time.y)
float4 _RealmsOverlay;          // line scale, grid visibility
float _RealmsClouds;            // darkening under the drifting clouds' shadows
float _RealmsCloudHeight;       // the drifting clouds' height over the map
float _RealmsCloudCover;        // the share of the clouds' grid squares holding a cloud
float _RealmsCloudShow;         // how much the clouds themselves show: 0 close to the land
float4 _RealmsWind;             // where the wind blows to (x, z), its strength; MapView sets it
float4 _RealmsHudMask;          // the task lines' rectangle on screen (normalized, from the bottom left), Hud sets it
float4 _RealmsCut;              // a screen's subject x, z, how far from it the cut band starts, its half-width (MapCamera)
float4 _RealmsCutEye;           // that screen camera's x, z, how far the cut is in (0 off the screens)

#define REALMS_SQRT3 1.7320508
#define REALMS_INNER 0.8660254

// How far a plant rooted at xz is cut away on a town or hero screen (MapCamera.Hold, review of
// 2026-11-10): in a band from just outside the screen's subject towards its camera, so the near
// wood does not cover the town or fill the lower part of the view. 0 outside it and on the map.
// A plant goes whole: only those rooted on a narrow strip, the middle 15% of the band's soft
// edge, dissolve in part, as plants dissolved pixel by pixel showed as grids of dots that read
// as a rendering fault (review of 2026-11-11). The screen's own fade in and out comes on top.
// The prop shader takes it at each plant's root, the terrain shader for the shade at their feet.
float RealmsCut(float2 xz)
{
    float2 toEye = _RealmsCutEye.xy - _RealmsCut.xy;
    float2 way = toEye * rsqrt(max(dot(toEye, toEye), 1e-6));
    float2 d = xz - _RealmsCut.xy;
    float along = dot(d, way), side = abs(dot(d, float2(-way.y, way.x)));
    float band = smoothstep(_RealmsCut.z, _RealmsCut.z + 0.6, along) * (1 - smoothstep(_RealmsCut.w - 0.6, _RealmsCut.w, side));
    return smoothstep(0.425, 0.575, band) * saturate(_RealmsCutEye.z);
}

static const float2 RealmsNormals[6] =
{
    float2(1, 0), float2(0.5, 0.8660254), float2(-0.5, 0.8660254),
    float2(-1, 0), float2(-0.5, -0.8660254), float2(0.5, -0.8660254)
};
static const int2 RealmsSteps[6] = { int2(1, 0), int2(0, 1), int2(-1, 1), int2(-1, 0), int2(0, -1), int2(1, -1) };

// The axial coordinates of the hexagon containing a point of the ground plane (x, z).
int2 RealmsHexAt(float2 p)
{
    float q = REALMS_SQRT3 / 3.0 * p.x - p.y / 3.0;
    float r = 2.0 / 3.0 * p.y;
    float s = -q - r;
    float rq = round(q), rr = round(r), rs = round(s);
    float dq = abs(rq - q), dr = abs(rr - r), ds = abs(rs - s);
    if (dq > dr && dq > ds)
        rq = -rr - rs;
    else if (dr > ds)
        rr = -rq - rs;
    return int2(rq, rr);
}

float2 RealmsCenter(int2 h)
{
    return float2(REALMS_SQRT3 * (h.x + 0.5 * h.y), 1.5 * h.y);
}

// Cell data for a hexagon: x owner (-1 for none), y biome, z flags. Off the map: open sea.
int4 RealmsCellAt(int2 h)
{
    int row = h.y;
    int col = h.x + ((row - (row & 1)) >> 1);
    if (col < 0 || row < 0 || col >= (int)_RealmsMapSize.x || row >= (int)_RealmsMapSize.y)
        return int4(-1, 0, 8, 0);
    float4 v = LOAD_TEXTURE2D(_RealmsCells, int2(col, row));
    int4 c = int4(round(v * 255.0));
    c.x -= 1;
    return c;
}

// The growing border's marks on a hexagon (BorderMarks): x the players' kingdom for their marks,
// y what is drawn (1 the cell a town of the players takes next, 2 the cell another kingdom's town
// takes next, 4 a wild cell the players' chosen town reaches, 8 a cell the players' border has just
// taken, 16 a cell the guiding lines point at), z the other kingdom for its mark; -1 for no kingdom.
// Off the map: nothing.
int3 RealmsMarkAt(int2 h)
{
    int row = h.y;
    int col = h.x + ((row - (row & 1)) >> 1);
    if (col < 0 || row < 0 || col >= (int)_RealmsMapSize.x || row >= (int)_RealmsMapSize.y)
        return int3(-1, 0, -1);
    int3 m = int3(round(LOAD_TEXTURE2D(_RealmsMarks, int2(col, row)).rgb * 255.0)) - int3(1, 0, 1);
    // Kingdoms past the colours' eight are no marks (and neither is a texture never uploaded).
    if (m.x > 7 || m.z > 7)
        return int3(-1, 0, -1);
    return m;
}

float RealmsHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Four hashes of a square of an integer grid (id holds whole numbers), each 0..1, a different set
// for each salt. An integer hash (pcg4d): exact however far from the origin the square lies, where
// RealmsHash's float fraction runs out on fine grids.
float4 RealmsHash4(float2 id, uint salt)
{
    uint4 v = uint4(uint2(int2(id)), salt, 0x9E3779B9u);
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.w; v.y += v.z * v.x; v.z += v.x * v.y; v.w += v.y * v.z;
    v ^= v >> 16u;
    v.x += v.y * v.w; v.y += v.z * v.x; v.z += v.x * v.y; v.w += v.y * v.z;
    return float4(v >> 8u) * (1.0 / 16777216.0);
}

float RealmsNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3 - 2 * f);
    return lerp(lerp(RealmsHash(i), RealmsHash(i + float2(1, 0)), u.x),
                lerp(RealmsHash(i + float2(0, 1)), RealmsHash(i + float2(1, 1)), u.x), u.y);
}

// Gradient noise, about -0.7..0.7: round features, with no square grid in them as in
// RealmsNoise.
float2 RealmsGradient(float2 i)
{
    float a = RealmsHash(i) * 6.2831853;
    return float2(cos(a), sin(a));
}

float RealmsPerlin(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * f * (f * (f * 6 - 15) + 10);
    float a = dot(RealmsGradient(i), f);
    float b = dot(RealmsGradient(i + float2(1, 0)), f - float2(1, 0));
    float c = dot(RealmsGradient(i + float2(0, 1)), f - float2(0, 1));
    float d = dot(RealmsGradient(i + 1), f - 1);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Three octaves of gradient noise, each turned against the last so no direction lines them up;
// mostly within -0.3..0.3.
float RealmsFbm(float2 p)
{
    const float2x2 turn = float2x2(0.8, -0.6, 0.6, 0.8);
    float sum = 0, amplitude = 0.6;
    [unroll]
    for (int k = 0; k < 3; k++)
    {
        sum += amplitude * RealmsPerlin(p);
        p = mul(turn, p) * 2.03 + 11.1;
        amplitude *= 0.5;
    }
    return sum;
}

// ----- Wind -----

// A point of the ground in the wind's frame: x along the wind (growing downwind), y across it.
float2 RealmsWindFrame(float2 p)
{
    float2 toward = _RealmsWind.xy;
    return float2(dot(p, toward), dot(p, float2(-toward.y, toward.x)));
}

// Gusts sweeping over the land with the wind, a few cells across and wider across the wind than
// along it, changing shape as they go (their finer half drifts slower): 0 in a lull, 1 in a gust.
// Round gradient noise, so no square grid shows in them as they move.
float RealmsGust(float2 p)
{
    float2 w = RealmsWindFrame(p);
    float t = _Time.y;
    float n = RealmsPerlin(float2((w.x - t * 0.55) * 0.4, w.y * 0.24))
        + 0.45 * RealmsPerlin(float2((w.x - t * 0.4) * 0.9 + 5.3, w.y * 0.54 + 2.9));
    return smoothstep(-0.05, 0.3, n);
}

// How far a vertex of a plant moves in the wind. sway.x is how far it bends at most, in world
// units (0 where it stands still, growing towards a plant's top), sway.y the plant's own phase so
// neighbours do not move in step. The plant leans a little with the wind, further in a gust, and
// rocks to and fro and flutters sideways about that lean: the rocking is what the eye sees from
// above, so it carries most of the movement. Its top dips as it leans.
float3 RealmsSway(float3 positionWS, float2 sway)
{
    if (sway.x <= 0)
        return 0;
    float t = _Time.y;
    float2 toward = _RealmsWind.xy, side = float2(-toward.y, toward.x);
    float gust = RealmsGust(positionWS.xz);
    float phase = sway.y * 6.2831853;
    float rock = sin(t * 1.7 + phase + dot(positionWS.xz, toward) * 3) * 0.6 + sin(t * 2.9 + phase * 1.6) * 0.4;
    float lean = 0.15 + 0.55 * gust + rock * (0.35 + 0.35 * gust);
    float flutter = sin(t * 3.7 + phase * 2.3) * (0.2 + 0.25 * gust);
    float2 offset = (toward * lean + side * flutter) * (_RealmsWind.z * sway.x);
    return float3(offset.x, -0.3 * length(offset), offset.y);
}

// The wind in the grass and the crops at a point of the ground: xy, how far the tips lean along
// the ground, in blade heights; z, how much of the grass is bent over and shows its lighter side,
// 0..1. The gusts are RealmsGust's, so the grass bends with the trees; inside a gust waves run
// downwind, twice as fast as the gust itself, in bands across the wind some three quarters of a
// cell apart that curve and break up as they go: the waves the eye follows over a meadow or a
// field of wheat. Each band is about a third of the way to the next: the grass bends as the
// front reaches it and rises again slowly behind it, and between the bands it stands, so in a
// still picture the waves read as waves rather than as a patch of paler grass (review of
// 2026-11-10; they were broad and soft, and a whole gust lightened). The front takes a twelfth
// of the way at least: at a twenty-fifth it ran as a hard bright line, which the players saw as
// white streaks. Nor is it ever sharper than about three pixels (px, the pixel's footprint on
// the ground), so it does not crawl; further out, where the bands are a few pixels wide, the
// waves are weaker.
float3 RealmsGrassWind(float2 p, float px)
{
    float2 w = RealmsWindFrame(p);
    float t = _Time.y;
    float gust = RealmsGust(p);
    // In periods: 0.8 a world unit along the wind.
    float phase = (w.x - t * 1.1) * 0.8 + RealmsPerlin(float2(w.y * 0.4, w.x * 0.1)) * 0.64;
    float f = frac(phase);
    float front = clamp(px * 2.4, 0.08, 0.16);
    float band = smoothstep(0.64, 1 - front, f) * (1 - smoothstep(1 - front, 1, f));
    float wave = gust * (0.12 + 0.88 * band) * _RealmsWind.z * lerp(1, 0.6, smoothstep(0.03, 0.07, px));
    float lean = 0.2 * _RealmsWind.z + 0.8 * wave;
    return float3(_RealmsWind.xy * lean, wave);
}

// ----- Fog of war -----

// How far the players have seen: MapView's _RealmsFog, one texel per cell in odd-r offset layout
// like _RealmsCells (red 0 unseen, 1 seen; green the fog's cloud height in units of
// REALMS_FOG_HEIGHT_SCALE, MapView.CloudHeightScale). Off the map nothing is seen and the fog
// stands as over the sea.
#define REALMS_FOG_HEIGHT_SCALE 2.5

float2 RealmsSeenAt(int2 h)
{
    int row = h.y;
    int col = h.x + ((row - (row & 1)) >> 1);
    if (col < 0 || row < 0 || col >= (int)_RealmsMapSize.x || row >= (int)_RealmsMapSize.y)
        return float2(0, 0.85);
    return LOAD_TEXTURE2D(_RealmsFog, int2(col, row)).rg * float2(1, REALMS_FOG_HEIGHT_SCALE);
}

// The three cell centres around a point of the ground (the triangle of centres it lies in) and
// how much each counts there, together 1: a cell's value eased this way is its own at its
// centre and halfway at an edge, with no seam where three cells meet.
void RealmsCorners(float2 p, out int2 h0, out int2 h1, out int2 h2, out float3 w)
{
    float2 a = float2(REALMS_SQRT3 / 3.0 * p.x - p.y / 3.0, 2.0 / 3.0 * p.y);
    float2 i = floor(a);
    float2 f = a - i;
    int2 h = (int2)i;
    h0 = h;
    h1 = h + int2(1, 0);
    h2 = h + int2(0, 1);
    float3 b = float3(1 - f.x - f.y, f.x, f.y);
    if (b.x < 0)
    {
        h0 = h + int2(1, 1);
        b = float3(-b.x, 1 - f.y, 1 - f.x);
    }
    w = b * b * (3 - 2 * b);
    w /= w.x + w.y + w.z;
}

// How far a point of the ground is seen, 0..1, and the fog's height over it, eased between the
// three cell centres around it (RealmsCorners).
float2 RealmsSeen(float2 p)
{
    int2 h0, h1, h2;
    float3 w;
    RealmsCorners(p, h0, h1, h2, w);
    float2 seen = RealmsSeenAt(h0) * w.x + RealmsSeenAt(h1) * w.y + RealmsSeenAt(h2) * w.z;
    return float2(saturate(seen.x), seen.y);
}

// ----- Clouds -----

// The drifting cloud over a point of the clouds' layer (x, z): its cover, 0..1, soft over soft
// world units at the outline; for its top, the normal of the puffs there, eased between them (world
// space), and how deep the point lies in a crease between puffs (0 on a crown, 1 in a waist). The clouds ride
// downwind (0.22 world units a second at strength 1) on a grid of squares 6 world units along
// the wind and 5 across; a share of the squares (_RealmsCloudCover) holds one cloud each, which
// with its jitter stays inside its square, so a point looks at its own square only. A
// cloud is a cluster of round puffs: a big crown in the middle, smaller ones along the wind and at
// its sides, the last sometimes left out. Each has its own size, turn (up to 15 degrees off the
// wind) and side, and a slow noise frays the outline, so the clouds change as they go. Also the
// middle of that cloud's crown (x, z), where RealmsCloudRoom asks whether the cloud may show.
float RealmsCloudAt(float2 p, float soft, out float3 normal, out float crease, out float2 middle)
{
    normal = float3(0, 1, 0);
    crease = 0;
    middle = p;
    float2 toward = _RealmsWind.xy, side = float2(-toward.y, toward.x);
    float drift = _Time.y * 0.22 * _RealmsWind.z;
    float2 w = float2(dot(p, toward) - drift, dot(p, side));
    // Small clouds, about three cells long: bigger ones lay over the sea and the borders like blots.
    const float2 size = float2(6, 5);
    float2 id = floor(w / size);
    if (RealmsHash(id * 0.731 + 5.3) >= _RealmsCloudCover)
        return 0;
    float h1 = RealmsHash(id * 1.37 + 2.1), h2 = RealmsHash(id * 0.53 + 9.4);
    float h3 = RealmsHash(id * 2.11 + 4.7), h4 = RealmsHash(id * 0.29 + 1.9);
    float scale = lerp(0.35, 0.58, h1);
    float turn = (h2 - 0.5) * 0.52;
    float flip = h3 < 0.5 ? -1 : 1;
    float2 crown = (id + 0.5) * size + (float2(h4, h2) - 0.5) * float2(1.4, 1.2);
    middle = toward * (crown.x + drift) + side * crown.y;
    float2 local = w - crown;
    float c = cos(turn), s = sin(turn);
    // The point in the cloud's own frame: x along it, y across, in the cloud's units.
    float2 q = float2(c * local.x + s * local.y, (c * local.y - s * local.x) * flip) / scale;
    bool tail = h3 > 0.35;
    // The puffs: centre, radius and the height of its base. A left-out tail hides in the crown.
    float4 puffs[6] =
    {
        float4(0, 0, 1.05 + 0.25 * h1, 0.2),
        float4(-1.15 - 0.2 * h2, 0.15, 0.8 + 0.15 * h3, 0.05),
        float4(1.1 + 0.2 * h4, -0.1, 0.75 + 0.15 * h2, 0),
        float4(-0.35, 0.75 + 0.15 * h4, 0.6 + 0.1 * h1, 0),
        float4(0.45, -0.75 - 0.1 * h3, 0.55 + 0.12 * h4, -0.05),
        float4(tail ? float2(2, 0.25) : float2(0, 0), tail ? 0.45 : 0.01, -0.1)
    };
    float d = 10, best = -10, second = -10;
    float heights[6];
    float3 slopes[6];
    [unroll]
    for (int k = 0; k < 6; k++)
    {
        float2 o = q - puffs[k].xy;
        float r = puffs[k].z, len = length(o);
        // A smooth union: neighbouring puffs melt together at their waists.
        float dk = len - r;
        float m = saturate(0.5 + 0.5 * (dk - d) / 0.3);
        d = lerp(dk, d, m) - 0.3 * m * (1 - m);
        float rise = sqrt(max(r * r - len * len, 0));
        heights[k] = puffs[k].w + rise - max(dk, 0);
        slopes[k] = float3(o, max(rise, 0.14 * r));
        second = max(second, min(best, heights[k]));
        best = max(best, heights[k]);
    }
    d += RealmsPerlin(q * 1.4 + id * 3.7 + _Time.y * 0.04) * 0.3;
    float cover = 1 - smoothstep(-soft, soft, d * scale);
    if (cover <= 0)
        return 0;
    crease = 1 - smoothstep(0, 0.5, best - second);
    // The top's normal: every puff's slope, as much as the puff nearly reaches the highest one
    // there, so the light passes from puff to puff without a seam (the highest's slope alone cut
    // the clouds into flat plates with hard edges, 2026-11-10); then from the cloud's frame back to
    // the world's.
    float3 top = 0;
    [unroll]
    for (int j = 0; j < 6; j++)
        top += normalize(slopes[j]) * exp((heights[j] - best) / 0.15);
    float2 v = float2(top.x, top.y * flip);
    v = float2(c * v.x - s * v.y, s * v.x + c * v.y);
    float2 slope = v.x * toward + v.y * side;
    normal = normalize(float3(slope.x, top.z, slope.y));
    return cover;
}

// ----- Light -----

float RealmsShadow(float3 positionWS)
{
    float4 shadowCoord;
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
        shadowCoord = ComputeScreenPos(TransformWorldToHClip(positionWS));
    #else
        shadowCoord = TransformWorldToShadowCoord(positionWS);
    #endif
    return GetMainLight(shadowCoord).shadowAttenuation;
}

// Drifting clouds keep to the land the players have seen: over the fog of war one read as a
// piece of it. How much of a cloud there is over a point of the clouds' layer.
float RealmsCloudSeen(float2 p)
{
    return smoothstep(0.35, 0.8, RealmsSeen(p).x);
}

// Whether a drifting cloud may stand over a point of the ground, 0..1: not within a cell of a
// town or a road (MapView marks those cells, the flag 128), where the players look most. Asked
// at a cloud's middle, so a whole cloud thins away as it drifts there and comes back after,
// rather than being cut along the cells.
float RealmsCloudRoomAt(int2 h)
{
    return (RealmsCellAt(h).z & 128) != 0 ? 0.0 : 1.0;
}

float RealmsCloudRoom(float2 p)
{
    int2 h0, h1, h2;
    float3 w;
    RealmsCorners(p, h0, h1, h2, w);
    float room = RealmsCloudRoomAt(h0) * w.x + RealmsCloudRoomAt(h1) * w.y + RealmsCloudRoomAt(h2) * w.z;
    return smoothstep(0.55, 0.95, room);
}

// The drifting clouds' shadows (1 in the clear, less under a cloud): the cloud over the point
// where the ray from here towards the sun crosses the clouds' height, softened.
float RealmsClouds(float3 positionWS)
{
    float3 toSun = _MainLightPosition.xyz;
    float2 p = positionWS.xz + toSun.xz / max(toSun.y, 0.2) * (_RealmsCloudHeight - positionWS.y);
    float3 normal;
    float crease;
    float2 middle;
    float cover = RealmsCloudAt(p, 0.3, normal, crease, middle);
    return cover > 0 ? 1 - _RealmsClouds * cover * RealmsCloudSeen(p) : 1;
}

// The albedo where the sun reaches, a cool shade of it where it does not; a soft rim brightens
// silhouettes so props read against the ground.
float3 RealmsLit(float3 albedo, float3 normalWS, float3 positionWS, float rim)
{
    Light sun = GetMainLight();
    float ndl = dot(normalWS, sun.direction);
    float shadow = lerp(1, RealmsShadow(positionWS), 0.85);
    float lit = smoothstep(-0.05, 0.5, ndl) * shadow;
    float3 shade = albedo * float3(0.58, 0.6, 0.72);
    float3 color = lerp(shade, albedo, lit);
    float3 view = GetWorldSpaceNormalizeViewDir(positionWS);
    float edge = pow(saturate(1 - dot(normalWS, view)), 3);
    color += rim * edge * albedo * (0.4 + 0.6 * lit);
    return color * RealmsClouds(positionWS);
}

// ----- Overlay -----

// The kingdom's colour mixed into the ground of its land and waters (before the light).
float3 RealmsTint(float3 albedo, float2 p, float amount)
{
    int owner = RealmsCellAt(RealmsHexAt(p)).x;
    if (owner < 0)
        return albedo;
    float3 c = _RealmsCountryColors[owner].rgb;
    return lerp(albedo, albedo * 0.5 + c * 0.5, amount);
}

// How much of the grid and the borders shows at a pixel (positionCS from SV_POSITION): a quarter
// under the task lines, which are text over the map without a window, so a border running through
// them crossed out their words (review of 2026-11-10); the change is soft over 14 pixels.
float RealmsHudShow(float4 positionCS)
{
    if (_RealmsHudMask.z <= _RealmsHudMask.x)
        return 1;
    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    float2 soft = 14 / _ScreenParams.xy;
    float2 inside = min(uv - _RealmsHudMask.xy, _RealmsHudMask.zw - uv) / soft;
    return lerp(1, 0.25, saturate(min(inside.x, inside.y) + 0.5));
}

// Grid, borders and rings over the lit colour, gridShare of the grid's strength (the plain open
// sea shows it more than the patterned land, so it fades out there), lineShow of the grid's and the
// borders' (see RealmsHudShow; the rings always show whole). Adds glow for the bloom to the emission
// and raises coverage (0..1) where a line is drawn, so the transparent water can make its lines opaque.
float3 RealmsLines(float3 color, float2 p, float mask, float gridShare, float lineShow, inout float3 emission, inout float coverage)
{
    float px = max(length(fwidth(p)), 1e-4);
    int2 h = RealmsHexAt(p);
    float2 local = p - RealmsCenter(h);
    int4 cell = RealmsCellAt(h);
    int owner = cell.x;
    float scale = max(_RealmsOverlay.x, 0.5);

    float nearest = 10;
    float border = 10;
    int side = 0;
    [unroll]
    for (int d = 0; d < 6; d++)
    {
        float e = REALMS_INNER - dot(local, RealmsNormals[d]);
        if (e < nearest)
        {
            nearest = e;
            side = d;
        }
        int other = RealmsCellAt(h + RealmsSteps[d]).x;
        if (owner >= 0 && other != owner)
            border = min(border, e);
    }

    // A faint grid that fades as the camera pulls back.
    float gridWidth = 0.012 * scale;
    float grid = 1 - smoothstep(gridWidth - px, gridWidth + px, nearest);
    mask *= lineShow;
    color = lerp(color, color * 0.6, grid * 0.32 * _RealmsOverlay.y * mask * gridShare);

    if (owner >= 0 && border < 1)
    {
        // Inside a kingdom along its border: a light core line on the edge, a solid band in the
        // kingdom's colour and a glow fading inwards. Over water the band is narrower and the
        // glow shorter and fainter (a red one turned the sea purple, and the plain sea showed the
        // band stronger than the selection), and a dark line runs along the band's inner side, so
        // a blue border still reads on the blue sea.
        bool water = (cell.z & 8) != 0;
        float3 c = _RealmsCountryColors[owner].rgb;
        float core = 0.03 * scale, band = (water ? 0.075 : 0.105) * scale, reach = (water ? 0.3 : 0.45) * scale;
        float fade = 1 - smoothstep(band, reach, border);
        color = lerp(color, c, fade * fade * (water ? 0.11 : 0.5) * mask);
        if (water)
        {
            float outer = band + 0.03 * scale;
            float outline = smoothstep(band - px, band + px, border) * (1 - smoothstep(outer - px, outer + px, border));
            color = lerp(color, float3(0.015, 0.018, 0.025), outline * 0.85 * mask);
            coverage = max(coverage, outline * mask);
        }
        float inBand = 1 - smoothstep(band - px, band + px, border);
        color = lerp(color, c, inBand * mask);
        float inCore = 1 - smoothstep(core - px, core + px, border);
        color = lerp(color, lerp(c, float3(1, 1, 1), 0.7), inCore * mask);
        emission += c * inBand * (water ? 0.04 : 0.08) * mask;
        coverage = max(coverage, inBand * mask);
    }

    // The growing border's marks (BorderMarks, the territory plan of 2026-11-11). The cell a
    // town takes next is outlined with dashes in the kingdom's colour, edged dark so it reads on
    // any ground: a shape, not only a colour. A cell just taken shows them brightening and
    // fading as its border appears. A wild cell the chosen town reaches gets a dot in the middle.
    int3 mark = RealmsMarkAt(h);
    if (mark.y != 0)
    {
        float seen = RealmsSeenAt(h).x * mask;
        float flash = (mark.y & 8) != 0 ? saturate(1 - (_Time.y - _RealmsMarkShow.y) / 1.6) : 0;
        bool own = (mark.y & 1) != 0 || flash > 0;
        float dashes = own ? 1 : (mark.y & 2) != 0 ? _RealmsMarkShow.x * 0.85 : 0;
        float3 c = _RealmsCountryColors[max(own ? mark.x : mark.z, 0)].rgb;
        if ((mark.y & 8) != 0 && (mark.y & 1) == 0)
            dashes = flash;
        dashes *= seen;
        if (dashes > 0)
        {
            // Along the nearest edge of the inset outline, 0..1 from corner to corner: three
            // dashes an edge, the middle one centred and one bent round each corner.
            float inset = 0.1 * scale, width = 0.06 * scale, edge = 0.018 * scale;
            float span = 1 - 2 * inset * 0.57735;
            float2 n = RealmsNormals[side];
            float s = dot(local, float2(-n.y, n.x)) / span + 0.5;
            float along = abs(frac(s * 3) - 0.5) - 0.2, perUnit = 3 / span;
            float off = abs(nearest - inset);
            float body = smoothstep(-px * perUnit, px * perUnit, along) * (1 - smoothstep(width * 0.5 - px, width * 0.5 + px, off));
            float rim = smoothstep(-px * perUnit, px * perUnit, along + edge * perUnit)
                * (1 - smoothstep(width * 0.5 + edge - px, width * 0.5 + edge + px, off));
            float3 dash = lerp(c, float3(1, 1, 1), 0.2 + 0.6 * flash);
            color = lerp(color, float3(0.02, 0.025, 0.03), rim * 0.65 * dashes);
            color = lerp(color, dash, body * dashes);
            emission += c * body * dashes * (0.08 + 0.5 * flash);
            coverage = max(coverage, rim * dashes);
        }
        if (flash > 0)
        {
            float glow = flash * flash * seen;
            color = lerp(color, lerp(c, float3(1, 1, 1), 0.3), 0.4 * glow * (1 - smoothstep(0.2, 0.9, 1 - nearest)));
            emission += c * 0.35 * glow;
        }
        if ((mark.y & 4) != 0)
        {
            float r = 0.065 * scale, edgeR = r + 0.02 * scale, at = length(local);
            float spot = 1 - smoothstep(r - px, r + px, at);
            float ring = 1 - smoothstep(edgeR - px, edgeR + px, at);
            float3 reach = _RealmsCountryColors[max(mark.x, 0)].rgb;
            color = lerp(color, float3(0.02, 0.025, 0.03), ring * 0.6 * seen);
            color = lerp(color, lerp(reach, float3(1, 1, 1), 0.35), spot * seen);
            coverage = max(coverage, ring * seen);
        }
        if ((mark.y & 16) != 0)
        {
            // Where the guiding lines point (the interface plan's 3.3): gold like the selection
            // ring, but set in from the edge and breathing slowly, edged dark so it reads on any
            // ground; whole under the task lines, as the selection's.
            float shown = RealmsSeenAt(h).x;
            float3 gold = float3(1.0, 0.62, 0.04);
            float breath = 0.6 + 0.4 * sin(_Time.y * 2.6);
            float inset = 0.2 * scale, w = 0.045 * scale, e = 0.02 * scale;
            float off = abs(nearest - inset);
            float body = 1 - smoothstep(w - px, w + px, off);
            float rim = 1 - smoothstep(w + e - px, w + e + px, off);
            color = lerp(color, float3(0.02, 0.025, 0.03), rim * 0.55 * shown);
            color = lerp(color, lerp(gold, float3(1, 1, 1), 0.25 * breath), body * shown);
            emission += gold * body * (0.25 + 0.45 * breath) * shown;
            coverage = max(coverage, rim * shown);
        }
    }

    if (_RealmsHover.z > 0.5 && all(h == int2(_RealmsHover.xy)))
    {
        float w = 0.05 * scale;
        float ring = 1 - smoothstep(w - px, w + px, nearest);
        color = lerp(color, float3(1, 1, 1), 0.14 * (1 - smoothstep(0, 0.7, nearest)));
        color = lerp(color, float3(1, 1, 1), ring * 0.95);
        coverage = max(coverage, ring);
    }
    if (_RealmsSelect.z > 0.5 && all(h == int2(_RealmsSelect.xy)))
    {
        float3 gold = float3(1.0, 0.62, 0.04);
        float pulse = 0.7 + 0.3 * sin(_Time.y * 4.0);
        float w = 0.08 * scale;
        float ring = 1 - smoothstep(w - px, w + px, nearest);
        color = lerp(color, gold, 0.16 * pulse * (1 - smoothstep(0, 0.6, nearest)));
        color = lerp(color, gold, ring);
        emission += gold * ring * 0.7 * pulse;
        coverage = max(coverage, ring);
    }
    return color;
}

#endif
