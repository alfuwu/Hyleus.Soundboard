#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

struct MPState
{
    float b;
    float bb;
    float3 bp;

    float g;
    float g2;
};

float Time;
float2 Resolution = float2(1280, 720);

float2 z, v, e = float2(.000035, -.000035);
float bo(float3 p, float3 r) {
    p = abs(p) - r;
    return max(max(p.x, p.y), p.z);
}
float2x2 r2(float r) {
    return float2x2(cos(r), sin(r), -sin(r), cos(r));
}
// compat with glsl
float mod(float x, float y) {
    return x - y * floor(x / y);
}
float2 fb(float3 p, float m, float bb)
{
    p.y += bb * .05;
    float2 h, t = float2(bo(p, float3(5, 1, 3)), 3);
    t.x = max(t.x, -(length(p) - 2.5));
    t.x = max(abs(t.x) - .2, (p.y - 0.4)); 
    h = float2(bo(p, float3(5, 1, 3)), 6); 
    h.x = max(h.x, -(length(p) - 2.5));
    h.x = max(abs(h.x) - .1, (p.y - 0.5));
    t = t.x < h.x ? t : h;
    h = float2(bo(p + float3(0, 0.4, 0), float3(5.4, 0.4, 3.4)), m);
    h.x = max(h.x, -(length(p) - 2.5));
    t = t.x < h.x ? t : h;
    h = float2(length(p) - 2., m);
    t = t.x < h.x ? t : h;
    t.x *= 0.7;
    return t;
}
float2 mp(float3 p, float tt, inout MPState st)
{
    float3 pp = st.bp = p;
    float2x2 mat = r2(sin(pp.x * .3 - tt * .5) * .4);
    st.bp.yz = mul(p.yz, mat);
    p.yz = mul(mul(p.yz, mat), r2(1.57));
    st.b = sin(pp.x * .2 + tt);
    st.bb = cos(pp.x * .2 + tt);
    p.x = mod(p.x - tt * 2., 10.) - 5.;
    float4 np = float4(p * .4, .4);
    for (int i = 0; i < 4; i++) {
        np.xyz = abs(np.xyz) - float3(1, 1.2, 0);
        np.xyz = 2. * clamp(np.xyz, -float3(0, 0, 0), float3(2, 0., 4.3 + st.bb)) - np.xyz;
        np = np * (1.3) / clamp(dot(np.xyz, np.xyz), 0.1, .92);
    }
    float2 h, t = fb(abs(np.xyz) - float3(2, 0, 0), 5., st.bb);
    t.x /= np.w;
    t.x = max(t.x, bo(p, float3(5, 5, 10)));
    np *= 0.5;
    np.yz = mul(np.yz, r2(.785));
    np.yz += 2.5;
    h = fb(abs(np.xyz) - float3(0, 4.5, 0), 7., st.bb);
    h.x = max(h.x, -bo(p, float3(20, 5, 5)));
    h.x /= np.w * 1.5;
    t = t.x < h.x ? t : h;
    h = float2(bo(np.xyz, float3(0.0, st.b * 20., 0.0)), 6);
    h.x /= np.w * 1.5;
    st.g2 += 0.1 / (0.1 * h.x * h.x * (1000. - st.b * 998.));
    t = t.x < h.x ? t : h;
    h = float2(0.6 * st.bp.y + sin(p.y * 5.) * 0.03, 6);
    t = t.x < h.x ? t : h;
    h = float2(length(cos(st.bp.xyz * .6 + float3(tt, tt, 0))) + 0.003, 6);
    st.g += 0.1 / (0.1 * h.x * h.x * 4000.);
    t = t.x < h.x ? t : h;
    return t;
}
float2 tr(float3 ro, float3 rd, float tt, out MPState st)
{
    float2 h, t = float2(.1, .1);
    for (int i = 0; i < 128; i++) {
        h = mp(ro + rd * t.x, tt, st);
       
        if (h.x < .0001 || t.x>40.) break;
        t.x += h.x; t.y = h.y;
    }
    if (t.x > 40.) t.y = 0.;
    return t;
}
#define a(d, po, no, tt, st) clamp(mp(po+no*d, tt, st).x/d,0.,1.)
#define s(d, po, ld, tt, st) smoothstep(0.,1.,mp(po+ld*d, tt, st).x/d)
float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 fragCoord = input.TexCoord;
    MPState st;
    st.b = 0;
    st.bb = 0;
    st.bp = 0;
    st.g = 0;
    st.g2 = 0;
    float2 uv = fragCoord.xy;
    float tt = sin(Time / 62.8318) * 62.8318; 
    float3 ro = lerp(float3(1, 1, 1), float3(-0.5, 1, -1), ceil(sin(tt * .5))) * float3(10, 2.8 + 0.75 * smoothstep(-1.5, 1.5, 1.5 * cos(tt + 0.2)), cos(tt * 0.3) * 3.1),//Ro=ray origin=camera position
        cw = normalize(float3(0, 0, 0) - ro), cu = normalize(cross(cw, normalize(float3(0, 1, 0)))), cv = normalize(cross(cu, cw)),
        rd = mul(float3x3(cu, cv, cw), normalize(float3(uv, .5))), co, fo;//rd=ray direction (where the camera is pointing), co=final color, fo=fog color
    float3 ld = normalize(float3(.2, .4, -.3));
    co = fo = float3(.1, .2, .3) - length(uv) * .1 - rd.y * .2;
    float2 z = tr(ro, rd, tt, st);
    float t = z.x;
    if (z.y > 0.) {
        float3 po = ro + rd * t;
        float3 no = normalize(e.xyy * mp(po + e.xyy, tt, st).x + e.yyx * mp(po + e.yyx, tt, st).x + e.yxy * mp(po + e.yxy, tt, st).x + e.xxx * mp(po + e.xxx, tt, st).x);
        float3 al = lerp(float3(0.1, 0.2, .4), float3(0.1, 0.4, .7), .5 + 0.5 * sin(st.bp.y * 7.));
        if (z.y < 5.) al = float3(0, 0, 0);
        if (z.y > 5.) al = float3(1, 1, 1);
        if (z.y > 6.) al = lerp(float3(1, .5, 0), float3(.9, .3, .1), .5 + .5 * sin(st.bp.y * 7.));
        float dif = max(0., dot(no, ld)),
            fr = pow(1. + dot(no, rd), 4.),
            sp = pow(max(dot(reflect(-ld, no), -rd), 0.), 40.);
        co = lerp(sp + lerp(float3(.8, .8, .8), float3(1, 1, 1), abs(rd)) * al * (a(.1, po, no, tt, st) * a(.2, po, no, tt, st) + .2) * (dif + s(2., po, ld, tt, st)), fo, min(fr, .2));
        co = lerp(fo, co, exp(-.0003 * t * t * t));
    }
    return float4(pow(co + st.g * .2 + st.g2 * lerp(float3(1., .5, 0), float3(.9, .3, .1), .5 + .5 * sin(st.bp.y * 3.)), float3(.65, .65, .65)), 1);// Naive gamma correction and glow applied at the end
}

technique PanelBackground
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
