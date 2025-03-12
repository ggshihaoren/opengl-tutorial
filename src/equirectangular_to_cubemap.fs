#version 330 core

out vec4 FragColor;
in vec3 WorldPos;

uniform sampler2D equirectangularMap;

const vec2 invAtan = vec2(0.1591, 0.3183); // 1/pi, 2/pi

vec2 SampleSpherical(vec3 v)
{
    vec2 uv = vec2(atan(v.z, v.x), asin(v.y)); // 球坐标转换为uv坐标, atan(z,x)计算的原点到点(x,z)与x轴的夹角
    uv *= invAtan; // 映射到[-0.5, 0.5]
    uv += 0.5; // 映射到[0, 1]
    return uv;
}

void main()
{
    vec2 uv = SampleSpherical(normalize(WorldPos));
    vec3 color = texture(equirectangularMap, uv).rgb;
    FragColor = vec4(color, 1.0);
}