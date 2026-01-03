#if defined(VERTEX) || __VERSION__ > 100 || defined(GL_FRAGMENT_PRECISION_HIGH)
    #define MY_HIGHP_OR_MEDIUMP highp
#else
    #define MY_HIGHP_OR_MEDIUMP mediump
#endif

extern MY_HIGHP_OR_MEDIUMP vec2 misprint;
extern MY_HIGHP_OR_MEDIUMP number dissolve;
extern MY_HIGHP_OR_MEDIUMP number time;
extern MY_HIGHP_OR_MEDIUMP vec4 texture_details;
extern MY_HIGHP_OR_MEDIUMP vec2 image_details;
extern bool shadow;
extern MY_HIGHP_OR_MEDIUMP vec4 burn_colour_1;
extern MY_HIGHP_OR_MEDIUMP vec4 burn_colour_2;
extern MY_HIGHP_OR_MEDIUMP vec2 mouse_screen_pos;
extern MY_HIGHP_OR_MEDIUMP float hovering;
extern MY_HIGHP_OR_MEDIUMP float screen_scale;

// ============ 颜色转换函数 ============
number hue(number s, number t, number h)
{
    number hs = mod(h, 1.0) * 6.0;
    if (hs < 1.0) return (t - s) * hs + s;
    if (hs < 3.0) return t;
    if (hs < 4.0) return (t - s) * (4.0 - hs) + s;
    return s;
}

vec4 RGB(vec4 c)
{
    if (c.y < 0.0001)
        return vec4(vec3(c.z), c.a);

    number t = (c.z < 0.5) ? c.y * c.z + c.z : -c.y * c.z + (c.y + c.z);
    number s = 2.0 * c.z - t;
    return vec4(hue(s, t, c.x + 1.0/3.0), hue(s, t, c.x), hue(s, t, c.x - 1.0/3.0), c.w);
}

vec4 HSL(vec4 c)
{
    number low = min(c.r, min(c.g, c.b));
    number high = max(c.r, max(c.g, c.b));
    number delta = high - low;
    number sum = high + low;

    vec4 hsl = vec4(0.0, 0.0, 0.5 * sum, c.a);
    if (delta == 0.0)
        return hsl;

    hsl.y = (hsl.z < 0.5) ? delta / sum : delta / (2.0 - sum);

    if (high == c.r)
        hsl.x = (c.g - c.b) / delta;
    else if (high == c.g)
        hsl.x = (c.b - c.r) / delta + 2.0;
    else
        hsl.x = (c.r - c.g) / delta + 4.0;

    hsl.x = mod(hsl.x / 6.0, 1.0);
    return hsl;
}

// ============ 噪声函数 ============
vec3 mod289(vec3 x) { 
    return x - floor(x * (1.0 / 289.0)) * 289.0; 
}

vec2 mod289(vec2 x) { 
    return x - floor(x * (1.0 / 289.0)) * 289.0; 
}

vec3 permute(vec3 x) { 
    return mod289(((x * 34.0) + 1.0) * x); 
}

float snoise(vec2 v)
{
    const vec4 C = vec4(0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439);
    
    vec2 i = floor(v + dot(v, C.yy));
    vec2 x0 = v - i + dot(i, C.xx);
    
    vec2 i1 = (x0.x > x0.y) ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
    vec4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;

    i = mod289(i);
    vec3 p = permute(permute(i.y + vec3(0.0, i1.y, 1.0)) + i.x + vec3(0.0, i1.x, 1.0));

    vec3 m = max(0.5 - vec3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
    m = m * m;
    m = m * m;

    vec3 x = 2.0 * fract(p * C.www) - 1.0;
    vec3 h = abs(x) - 0.5;
    vec3 ox = floor(x + 0.5);
    vec3 a0 = x - ox;

    m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);

    vec3 g;
    g.x = a0.x * x0.x + h.x * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;
    
    return 130.0 * dot(m, g);
}

// ============ Dissolve 遮罩 ============
vec4 dissolve_mask(vec4 tex, vec2 texture_coords, vec2 uv)
{
    if (dissolve < 0.001) {
        return vec4(shadow ? vec3(0.0) : tex.xyz, shadow ? tex.a * 0.3 : tex.a);
    }

    float adjusted_dissolve = (dissolve * dissolve * (3.0 - 2.0 * dissolve)) * 1.02 - 0.01;
    float t = time * 10.0 + 2003.0;
    
    vec2 floored_uv = (floor((uv * texture_details.ba))) / max(texture_details.b, texture_details.a);
    float n = snoise(floored_uv * 200.0 + 25.0) + snoise(floored_uv * 100.0 + t) + snoise(floored_uv * 50.0 + t * 2.0);
    
    float threshold = (adjusted_dissolve) * 1.4 - n * (0.5 + 0.5 * adjusted_dissolve);
    vec4 c = (threshold >= 0.001) ? vec4(0.0) : tex;
    c.a = tex.a;

    float blur_region = (0.1 * texture_details.r / texture_details.b);
    float interval = adjusted_dissolve * 1.4 - n * (0.5 + 0.5 * adjusted_dissolve);

    if (interval < (0.0 + blur_region)) {
        vec4 hsl = HSL(tex);
        hsl.x = HSL(burn_colour_1).x;
        c = hsl.y > 0.1 ? RGB(hsl) : burn_colour_1;
        c = mix(c, burn_colour_1, 0.6);
        c.a = tex.a;
    } else if (interval < (0.0 + blur_region * 2.0)) {
        vec4 hsl = HSL(tex);
        hsl.x = HSL(burn_colour_2).x;
        c = hsl.y > 0.1 ? RGB(hsl) : burn_colour_2;
        c = mix(c, burn_colour_2, 0.6);
        c.a = tex.a;
    }

    if (c.a <= 0.001) 
        c = vec4(0.0);

    return c;
}

// ============ 顶点着色器 ============
#ifdef VERTEX
vec4 position(mat4 transform_projection, vec4 vertex_position)
{
    if (hovering <= 0.0) {
        return transform_projection * vertex_position;
    }
    
    float mid_dist = length(vertex_position.xy - 0.5 * love_ScreenSize.xy) / length(love_ScreenSize.xy);
    vec2 mouse_offset = (vertex_position.xy - mouse_screen_pos.xy) / screen_scale;
    float scale = 0.2 * (-0.03 - 0.3 * max(0.0, 0.3 - mid_dist))
                * hovering * (length(mouse_offset) * length(mouse_offset)) / (2.0 - mid_dist);

    return transform_projection * vertex_position + vec4(0.0, 0.0, 0.0, scale);
}
#endif

// ============ 片段着色器 ============
#ifdef PIXEL
vec4 effect(vec4 color, Image texture, vec2 texture_coords, vec2 screen_coords)
{
    vec2 uv = (((texture_coords * (texture_details.ba)) - texture_details.rg)) / image_details;
    
    if (misprint.x + misprint.y == 0.0) {
        vec4 pixel = Texel(texture, texture_coords);
        return dissolve_mask(pixel, texture_coords, uv);
    }

    // RGB 通道分离效果
    vec2 offset_r = misprint * vec2(0.01, 0.0);
    vec2 offset_g = misprint * vec2(0.0, 0.0);
    vec2 offset_b = misprint * vec2(-0.01, 0.0);

    float r = Texel(texture, texture_coords + offset_r / image_details).r;
    float g = Texel(texture, texture_coords + offset_g / image_details).g;
    float b = Texel(texture, texture_coords + offset_b / image_details).b;
    float a = Texel(texture, texture_coords).a;

    vec4 pixel = vec4(r, g, b, a);

    // 添加噪声和色差效果
    float noise_val = snoise(uv * 100.0 + time) * 0.05;
    pixel.rgb += noise_val;

    return dissolve_mask(pixel, texture_coords, uv);
}
#endif