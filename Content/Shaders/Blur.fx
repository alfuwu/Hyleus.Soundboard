#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;

// higher values = blurrier image
float BlurStrength = 1.0;
float2 TextureSize;

sampler2D SpriteTextureSampler = sampler_state{
    Texture = <SpriteTexture>;
};

struct VertexShaderOutput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TextureCoordinates * TextureSize;

    // convert blur strength into UV offset
    float2 texelOffset = float2(BlurStrength, BlurStrength) * 0.001;

    // 1d gaussian weights (sigma ≈ 1.0), normalized
    float weights[5] = {
        0.06136,
        0.24477,
        0.38774,
        0.24477,
        0.06136
    };

    float4 color = 0.0;

    // 5x5 gaussian blur
    for (int x = -2; x <= 2; x++)
    {
        for (int y = -2; y <= 2; y++)
        {
            float2 offset = float2(x, y) * texelOffset;
            float w = weights[x + 2] * weights[y + 2];
            color += tex2D(SpriteTextureSampler, (uv + offset) / TextureSize) * w;
        }
    }

    return color * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
