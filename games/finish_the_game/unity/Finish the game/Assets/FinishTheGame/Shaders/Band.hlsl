#ifndef BAND_INCLUDED
#define BAND_INCLUDED

// The band: the plane is seen only along the stretch of the story's route
// round the camera's focus (PlaneWorld.cs sends it each frame as up to
// BAND_POINTS points, from behind to ahead). A point of the world is kept by
// its distance across the stretch: whole within _BandWidth.x - _BandWidth.y,
// gone at _BandWidth.x; and by how far it lies past the stretch's ends: gone
// over _BandEnds.x behind the first point and _BandEnds.y past the last (where
// the horizon takes over). Another pass of the route through the same place,
// earlier or later in the story, is not in the stretch: there, only what is
// near the stretch shows. With a crystal (_BandDisk.w), only what is within
// its radius, round its middle, too; an open crystal (w = 2) is all the world
// within its radius, band or not.

#define BAND_POINTS 64

float4 _BandPoints[BAND_POINTS];   // xz on the ground (x, z in .xz)
float _BandCount;                  // how many are sent; under 2: no band, all kept
float4 _BandWidth;                 // half width, its fade
float4 _BandEnds;                  // the fades past the first point and the last
float4 _BandDisk;                  // the crystal's middle x, z, radius; w: 1 a crystal on the band, 2 an open one

// 1: kept whole; 0: gone.
float BandKeep(float3 positionWS)
{
    int n = (int)_BandCount;
    if (n < 2) return 1.0;
    float2 p = positionWS.xz;
    if (_BandDisk.w > 1.5) return distance(p, _BandDisk.xy) < _BandDisk.z ? 1.0 : 0.0;
    float nearest = 1e12, past = 0;
    [loop] for (int i = 0; i < n - 1; i++)
    {
        float2 a = _BandPoints[i].xz, b = _BandPoints[i + 1].xz;
        float2 ab = b - a, ap = p - a;
        float length2 = max(dot(ab, ab), 1e-6);
        float h = dot(ap, ab) / length2;
        // The ends' segments run on beyond them; how far past is faded apart.
        float over = 0;
        if (i == 0 && h < 0) over = -h * sqrt(length2) / max(_BandEnds.x, 1e-3);
        if (i == n - 2 && h > 1) over = (h - 1) * sqrt(length2) / max(_BandEnds.y, 1e-3);
        if (!(i == 0 && h < 0) && !(i == n - 2 && h > 1)) h = saturate(h);
        float2 across = ap - ab * h;
        float d2 = dot(across, across);
        if (d2 < nearest)
        {
            nearest = d2;
            past = over;
        }
    }
    float side = 1.0 - smoothstep(_BandWidth.x - _BandWidth.y, _BandWidth.x, sqrt(nearest));
    float keep = side * (1.0 - saturate(past));
    if (_BandDisk.w > 0.5 && distance(p, _BandDisk.xy) >= _BandDisk.z) keep = 0;
    return keep;
}

#endif
