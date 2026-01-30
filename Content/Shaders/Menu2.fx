#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Inputs
float3 Color1;       // Primary color
float3 Color2;       // Secondary color
float3 Color3;       // Accent color
float Time;          // Animation time
float AspectRatio;   // Screen width / height

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

// Vertex shader: pass through
VertexShaderOutput MainVS(float4 pos : POSITION, float2 tex : TEXCOORD0)
{
    VertexShaderOutput output;
    output.Position = pos;
    output.TexCoord = tex;
    return output;
}

// Procedural geometric background
float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TexCoord;
    uv.x *= AspectRatio / 2; // Correct for non-square screens

    // Create a moving grid pattern
    float grid = abs(sin((uv.x + Time * 0.1) * 10.0) * sin((uv.y + Time * 0.1) * 10.0));

    // Create a rotating radial pattern
    float angle = atan2(uv.y - 0.5, uv.x - 0.5);
    float radius = length(uv - 0.5);
    float radial = 0.5 + 0.5 * sin(radius * 20.0 - Time * 0.5 + angle * 3.0);

    // Mix the patterns
    float pattern = lerp(grid, radial, 0.5);

    // Mix the three input colors
    float3 colorMix = lerp(Color1, Color2, pattern);
    colorMix = lerp(colorMix, Color3, pattern * 0.5);

    return float4(colorMix, 1.0);
}

technique Technique0
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
