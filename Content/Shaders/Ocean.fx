//======================================================
// Shader Model
//======================================================
#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

//======================================================
// Resources
//======================================================
Texture2D    Channel3 : register(t0);   // heightmap
TextureCube  Channel2 : register(t1);   // environment map
SamplerState LinearSampler : register(s0);

//======================================================
// Constants (Shadertoy-style)
//======================================================
cbuffer Globals : register(b0)
{
    float3 Resolution;
    float  Time;
    float4 Mouse;
};

static const int   numWaves = 60;
static const int   STEPS = 200;
static const int   STEPS_GROUND = 50;
static const float PI = 3.141592653589;

static const float3 ld = normalize(float3(-1.0, -1.0, -2.0));

static const float oceanHeight = 0.2;
static const float waveBaseHeight = 0.5;
static const float waveMaxAmplitude = 0.35;

static const float3 waterCol = float3(0.15, 0.5, 0.75);
static const float  waterAbsorp = 0.7;
static const float3 subsurfCol = waterCol * float3(1.3, 1.5, 1.1);

float causticNoiseBlur;

//======================================================
// Materials
//======================================================
#define MAT_GROUND 0
#define MAT_OCEAN  1

//======================================================
// Vertex Output
//======================================================
struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

//======================================================
// External Functions
//======================================================
float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float SingleWaveHeight(float2 uv, float2 dir, float speed, float ampl, float time)
{
    float d = dot(uv, dir);
    float ph = d * 10.0 + time * speed;

    float h = sin(ph) * 0.5 + 0.5;
    h = pow(h, 2.0);
    h = h * 2.0 - 1.0;

    return h * ampl;
}

float WaveHeight(float2 uv, float time, int num)
{
    uv *= 1.6;

    float h = 0.0;
    float w = 1.0;
    float tw = 0.0;
    float s = 1.0;

    const float phBase = 0.2;

    for (int i = 0; i < num; i++)
    {
        float rand = hash11((float)i) * 2.0 - 1.0;

        float dirMaxDiffer = (float)i / (float)(numWaves - 1);
        dirMaxDiffer = pow(dirMaxDiffer, 1.0) * 2.0 * PI;

        float ph = phBase + rand * 0.75 * PI;
        float2 dir = float2(sin(ph), cos(ph));

        h += SingleWaveHeight(uv, dir, 1.0 + s * 0.05, w, time);
        tw += w;

        const float scale = 1.0812;
        w /= scale;
        uv *= scale;
        s *= scale;
    }

    h /= tw;
    h = waveBaseHeight + waveMaxAmplitude * h;

    return h;
}

void RORD(float2 uv, out float3 ro, out float3 rd, float time, float4 Mouse)
{
    float rotPh;
    float y;

    if (Mouse.z > 0.0)
    {
        rotPh = -Mouse.x * 0.01;
        y = 4.0 - Mouse.y * 0.005;
    }
    else
    {
        rotPh = 2.5 + time * 0.05;
        y = 1.3;
    }

    float rad = 1.6;

    ro = float3(sin(rotPh), y, cos(rotPh)) * rad;

    float3 lookAt = float3(0.0, 0.0, 0.0);

    float3 cf = normalize(lookAt - ro);
    float3 cr = normalize(cross(cf, float3(0.0, 1.0, 0.0)));
    float3 cu = normalize(cross(cr, cf));

    const float fl = 1.0;
    rd = normalize(uv.x * cr + uv.y * cu + fl * cf);
}

float4 mod289(float4 x)
{
    return x - floor(x / 289.0) * 289.0;
}

float4 permute(float4 x)
{
    return mod289((x * 34.0 + 1.0) * x);
}

float4 snoise(float3 v)
{
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);

    float3 i = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);

    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - 0.5;

    float4 p =
        permute(
            permute(
                permute(i.z + float4(0.0, i1.z, i2.z, 1.0))
                + i.y + float4(0.0, i1.y, i2.y, 1.0))
            + i.x + float4(0.0, i1.x, i2.x, 1.0));

    float4 j = p - 49.0 * floor(p / 49.0);

    float4 x_ = floor(j / 7.0);
    float4 y_ = floor(j - 7.0 * x_);

    float4 x = (x_ * 2.0 + 0.5) / 7.0 - 1.0;
    float4 y = (y_ * 2.0 + 0.5) / 7.0 - 1.0;

    float4 h = 1.0 - abs(x) - abs(y);

    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(h, 0.0);

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    float3 g0 = float3(a0.xy, h.x);
    float3 g1 = float3(a0.zw, h.y);
    float3 g2 = float3(a1.xy, h.z);
    float3 g3 = float3(a1.zw, h.w);

    float4 m = max(0.6 - float4(
        dot(x0, x0),
        dot(x1, x1),
        dot(x2, x2),
        dot(x3, x3)), 0.0);

    float4 m2 = m * m;
    float4 m3 = m2 * m;
    float4 m4 = m2 * m2;

    float3 grad =
        -6.0 * m3.x * x0 * dot(x0, g0) + m4.x * g0 +
        -6.0 * m3.y * x1 * dot(x1, g1) + m4.y * g1 +
        -6.0 * m3.z * x2 * dot(x2, g2) + m4.z * g2 +
        -6.0 * m3.w * x3 * dot(x3, g3) + m4.w * g3;

    float4 px = float4(
        dot(x0, g0),
        dot(x1, g1),
        dot(x2, g2),
        dot(x3, g3));

    return lerp(42.0, 0.0, causticNoiseBlur) * float4(grad, dot(m4, px));
}

//======================================================
// Height Functions
//======================================================
float WaterHeight(float3 p, int waveCount)
{
    return WaveHeight(p.xz * 0.1, Time, waveCount) + oceanHeight;
}

float GroundHeight(float3 p)
{
    float h = 0.0;
    float tw = 0.0;
    float w = 1.0;

    p *= 0.2;
    p.xz += float2(-1.25, 0.35);

    for (int i = 0; i < 2; i++)
    {
        h += w * sin(p.x) * sin(p.z);
        const float s = 1.173;
        tw += w;
        p *= s;
        p.xz += float2(2.373, 0.977);
        w /= s;
    }

    h /= tw;
    return -0.2 + 1.65 * h;
}

//======================================================
// SDFs
//======================================================
float sdOcean(float3 p)
{
    float dh = p.y - WaterHeight(p, numWaves);
    return dh * 0.75;
}

float sdOcean_Levels(float3 p, int waveCount)
{
    float dh = p.y - WaterHeight(p, waveCount);
    return dh * 0.75;
}

float map(float3 p, bool includeWater, out int material)
{
    float hGround = GroundHeight(p);
    float dGround = (p.y - hGround) * 0.9;
    float d = dGround;

    material = MAT_GROUND;

    if (includeWater)
    {
        float dOcean = sdOcean(p);
        if (dOcean < d) material = MAT_OCEAN;
        d = min(d, dOcean);
    }

    return d;
}

//======================================================
// Raymarching
//======================================================
float RM(float3 ro, float3 rd, out int material)
{
    float t = 0.0;
    float s = 1.0;

    for (int i = 0; i < STEPS; i++)
    {
        float d = map(ro + t * rd, true, material);
        if (d < 0.001) return t;
        t += d * s;
        s *= 1.02;
    }

    return -t;
}

float RM_Ground(float3 ro, float3 rd, out int material)
{
    float t = 0.0;

    for (int i = 0; i < STEPS_GROUND; i++)
    {
        float d = map(ro + t * rd, false, material);
        if (d < 0.001) return t;
        t += d;
    }

    return -t;
}

//======================================================
// Normals
//======================================================
float3 Normal(float3 p, out int material)
{
    const float h = 0.001;
    const float2 k = float2(1, -1);

    return normalize(
        k.xyy * map(p + k.xyy * h, true, material) +
        k.yyx * map(p + k.yyx * h, true, material) +
        k.yxy * map(p + k.yxy * h, true, material) +
        k.xxx * map(p + k.xxx * h, true, material)
    );
}

float3 WaveNormal_Levels(float3 p, int levels)
{
    const float h = 0.001;
    const float2 k = float2(1, -1);

    return normalize(
        k.xyy * sdOcean_Levels(p + k.xyy * h, levels) +
        k.yyx * sdOcean_Levels(p + k.yyx * h, levels) +
        k.yxy * sdOcean_Levels(p + k.yxy * h, levels) +
        k.xxx * sdOcean_Levels(p + k.xxx * h, levels)
    );
}

float3 NormalGround(float3 p, out int material)
{
    const float h = 0.001;
    const float2 k = float2(1, -1);
    const float heightmapScale = 2.0;
    const float heightmapHeight = 0.025;

    float s1 = Channel3.SampleLevel(LinearSampler, (p + k.xyy * h).xz * heightmapScale, 0).x;
    float s2 = Channel3.SampleLevel(LinearSampler, (p + k.yyx * h).xz * heightmapScale, 0).x;
    float s3 = Channel3.SampleLevel(LinearSampler, (p + k.yxy * h).xz * heightmapScale, 0).x;
    float s4 = Channel3.SampleLevel(LinearSampler, (p + k.xxx * h).xz * heightmapScale, 0).x;

    return normalize(
        k.xyy * (map(p + k.xyy * h, true, material) + heightmapHeight * s1) +
        k.yyx * (map(p + k.yyx * h, true, material) + heightmapHeight * s2) +
        k.yxy * (map(p + k.yxy * h, true, material) + heightmapHeight * s3) +
        k.xxx * (map(p + k.xxx * h, true, material) + heightmapHeight * s4)
    );
}

//======================================================
// Utility
//======================================================
float Fresnel(float3 rd, float3 nor)
{
    float f = 1.0 - abs(dot(nor, rd));
    return pow(f, 6.0);
}

float3 Reflection(float3 refl, float fresnel)
{
    float spec = pow(max(0.0, dot(refl, -ld)), 256.0);
    float3 col = spec;
    col += fresnel * Channel2.SampleLevel(LinearSampler, refl, 0).rgb * 0.4;
    return col;
}

void DarkenGround(inout float3 col, float3 groundPos, float oceanH, out float wetness)
{
    wetness = 1.0 - smoothstep(0.05, 0.2, groundPos.y - oceanH - 0.3);
    col = lerp(col, col * float3(0.95, 0.92, 0.85) * 0.8, wetness);
}

//======================================================
// Rendering
//======================================================
float3 Render(float d, float3 ro, float3 rd, int material)
{
    // Not hit -> render background
    if (d < 0.0)
    {
        float3 col = float3(0.35, 0.62, 0.9);
        col = lerp(col, float3(1.0, 1.0, 1.0), max(0.0, (1.0 - rd.y) * 0.3));
        float sunDot = max(0.0, dot(rd, -ld));
        sunDot = pow(sunDot, 6.0);
        sunDot = tanh(sunDot);
        col += sunDot * float3(1.0, 0.8, 0.7);
        return col;
    }

    float3 p = ro + d * rd;
    float3 refl;
    float3 pGround;
    float3 col = float3(0.9, 0.85, 0.7); // ground base color
    float3 transmittance = float3(1.0, 1.0, 1.0);

    if (material == MAT_OCEAN)
    {
        float hGround = GroundHeight(p);
        float dGround = p.y - hGround;
        float nearShoreAlpha = 1.0 - saturate((hGround - oceanHeight - 0.5) / (-0.7)); // matches smoothstep(.5, -.2, ...)

        float3 nor = Normal(p, material);
        nor = normalize(lerp(nor, float3(0, 1, 0), nearShoreAlpha * 0.9));
        refl = reflect(rd, nor);
        float3 refr = refract(rd, nor, 1.0 / 1.2);
        if (all(refr == 0.0)) refr = refl;

        float tGround = RM_Ground(p, refr, material);
        if (tGround < 0.0) tGround = 4.0; // crude fix
        pGround = p + tGround * refr;

        float fresnel = Fresnel(rd, nor);
        float3 norSubsurf = WaveNormal_Levels(p, numWaves / 3);
        float3 ldSubsurf = ld * float3(1.0, -1.0, 1.0);
        float subsurf = max(0.0, max(0.0, dot(rd, -ldSubsurf)) * dot(norSubsurf, ldSubsurf));
        subsurf = pow(subsurf, 2.0) * (1.0 - fresnel) * 0.5;

        float wetness;
        DarkenGround(col, pGround, oceanHeight, wetness);

        float spec = pow(max(0.0, dot(refl, -ld)), 256.0);

        transmittance = exp(-tGround * waterAbsorp / waterCol);
        float waterAlpha = 1.0 - exp(-tGround * waterAbsorp * 0.5);

        // caustics
        float3 causticPos = pGround * 2.0 + float3(0, Time * 0.15, 0);
        float3 o = float3(0.01, 0.0, 0.01) * 2.0;
        float3 caustics;
        caustics.x = lerp(snoise(causticPos + o).x, snoise(causticPos + o + 1.0).x, 0.5);
        caustics.y = lerp(snoise(causticPos + o * 4.0).y, snoise(causticPos + o + 1.0).y, 0.5);
        caustics.z = lerp(snoise(causticPos + o * 6.0).z, snoise(causticPos + o + 1.0).z, 0.5);
        float causticAlpha = 1.0 - saturate(exp(-tGround * 2.0));
        caustics = exp(caustics * 4.0 - 1.0) * causticAlpha;
        col += caustics;

        col *= transmittance;
        col += tGround * exp(-tGround * waterAbsorp) * waterCol * 0.3;
        col += subsurf * subsurfCol;
        col += Reflection(refl, fresnel);
    }
    else if (material == MAT_GROUND)
    {
        pGround = p;
        float wetness;
        DarkenGround(col, pGround, oceanHeight, wetness);

        float3 nor = Normal(p, material);
        refl = reflect(rd, nor);
        float fresnel = Fresnel(rd, nor);
        col += wetness * Reflection(refl, fresnel);
    }

    return col;
}

//======================================================
// Pixel Shader Entry
//======================================================
float4 PS_Main(VertexShaderOutput input) : COLOR
{
    float2 fragCoord = input.TexCoord * Resolution.xy;
    float2 uv = (2.0 * fragCoord - Resolution.xy) / Resolution.y;

    float3 ro, rd;
    RORD(uv, ro, rd, Time, Mouse);

    int material = 0;
    float d = RM(ro, rd, material);
    float3 col = pow(Render(d, ro, rd, material), 1.0 / 2.2);

    return float4(col, 1.0);
}

technique PanelBackground
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL PS_Main();
    }
}
