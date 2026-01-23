#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;

float AntialiasThickness = 1.0;
float CornerRadius = 16.0;
float2 TextureSize;

sampler2D SpriteTextureSampler = sampler_state{
    Texture = <SpriteTexture>;
};

struct VertexShaderOutput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

float4 MainPS(VertexShaderOutput input) : COLOR{
    // sample the texture
    float4 color = tex2D(SpriteTextureSampler, input.TextureCoordinates) * input.Color;

    // convert uv to pixel coordinates
    float2 pixelPos = input.TextureCoordinates * TextureSize;

    // distance from nearest edge
    float dx = min(pixelPos.x, TextureSize.x - pixelPos.x);
    float dy = min(pixelPos.y, TextureSize.y - pixelPos.y);

    // compute alpha for corners
    float alpha = 1.0;
    if (dx < CornerRadius && dy < CornerRadius) {
        float cx = CornerRadius - dx;
        float cy = CornerRadius - dy;
        float dist = sqrt(cx * cx + cy * cy);
        alpha = saturate((CornerRadius - dist) / AntialiasThickness);
    }

    // slightly fade edges to better match with corner aa
    if (pixelPos.x <= 0.5 || pixelPos.x >= TextureSize.x - 0.5 ||
        pixelPos.y <= 0.5 || pixelPos.y >= TextureSize.y - 0.5) {
        alpha = min(alpha, 0.5);
    }

    return color * alpha;
}

technique SpriteDrawing{
    pass P0 {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
