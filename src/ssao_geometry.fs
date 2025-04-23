#version 330 core
layout (location = 0) out vec4 gPositionDepth;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gAlbedoSpec;

in vec2 TexCoords;
in vec3 Normal;
in vec3 FragPos;

const float NEAR = 0.1;
const float FAR = 50.0;

float LinearizeDepth(float depth)
{
    float z = depth * 2.0 - 1.0; // Back to NDC 
    return (2.0 * NEAR * FAR) / (FAR + NEAR - z * (FAR - NEAR));	
} // 将深度值转换为线性空间的深度值

void main()
{    
    // 储存片段的位置矢量到第一个G缓冲纹理
    gPositionDepth.xyz = FragPos;
    // 储存线性深度到gPositionDepth的alpha分量
    gPositionDepth.a = LinearizeDepth(gl_FragCoord.z); 
    // 储存法线信息到G缓冲
    gNormal = normalize(Normal);
    // 漫反射颜色
    gAlbedoSpec.rgb = vec3(0.95);
}