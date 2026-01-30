#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float3 Color1;
float3 Color2;
float3 Color3;

float Time;
float AspectRatio;

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float2 Rotate(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(
        c * p.x - s * p.y,
        s * p.x + c * p.y
    );
}

// stable hash per cell
float Hash21(float2 p)
{
    p = frac(p * float2(127.1, 311.7));
    p += dot(p, p + 34.5);
    return frac(p.x * p.y);
}

// signed distance to a rectangle with pointed ends
float PanelShape(float2 p, float2 size, float tip)
{
    // core rectangle
    float2 d = abs(p) - size;
    float rect = max(d.x, d.y);

    // rointy ends (triangular caps)
    float tipShape = abs(p.x) + p.y * 0.5 - tip;

    return min(rect, tipShape);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TexCoord;
    uv.x *= AspectRatio;

    // center, rotate ~45°, uncenter
    uv -= 0.5;
    uv = Rotate(uv, 0.785398); // 45°
    uv += 0.5;

    // slow diagonal drift
    uv += float2(Time * 0.01, Time * 0.005);

    // large sparse grid
    float2 cellUV = uv * float2(4.0, 6.0);
    float2 cell = floor(cellUV);
    float2 local = frac(cellUV) - 0.5;

    // shape variation per panel
    float h = Hash21(cell);
    float width = lerp(0.15, 0.25, h);
    float height = lerp(0.05, 0.08, h);
    float tip = lerp(0.18, 0.28, h);

    float d = PanelShape(local, float2(width, height), tip);

    // outside shape = background (transparent black)
    if (d > 0.0)
        discard;

    // hard color choice
    float r = Hash21(cell + 17.3);
    float3 color =
        (r < 0.33) ? Color1 :
        (r < 0.66) ? Color2 :
                     Color3;

    return float4(color, 1.0);
}

technique PanelBackground
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
