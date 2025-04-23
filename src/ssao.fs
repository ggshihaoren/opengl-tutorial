#version 330 core
out float FragColor;
in vec2 TexCoords;

uniform sampler2D gPositionDepth;
uniform sampler2D gNormal;
uniform sampler2D texNoise;

uniform vec3 samples[64];

int kernelSize = 64;
float radius = 1.0;

// 由于TexCoords的取值在0.0和1.0之间，texNoise纹理将不会平铺。所以我们将通过屏幕分辨率除以噪声纹理大小的方式计算TexCoords的缩放大小，并在之后提取相关输入向量的时候使用
const vec2 noiseScale = vec2(800.0f / 4.0f, 600.0f / 4.0f);

uniform mat4 projection;

void main()
{
    vec3 fragPos = texture(gPositionDepth, TexCoords).xyz;
    vec3 normal = texture(gNormal, TexCoords).rbg;
    vec3 randomVec = texture(texNoise, TexCoords * noiseScale).xyz;

     // 使用了一个随机向量来构造切线向量, 必要有一个恰好沿着几何体表面的TBN矩阵
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN = mat3(tangent, bitangent, normal);

    float occlusion = 0.0; // 环境遮蔽值
    for (int i = 0; i < kernelSize; i++) {
        vec3 sample = TBN * samples[i]; // 从切线空间转换到世界空间
        sample = fragPos + sample * radius;

        vec4 offset = vec4(sample, 1.0);
        offset = projection * offset; // 从世界空间转换到投影空间
        offset.xyz /= offset.w; // 透视除法
        offset.xyz = offset.xyz * 0.5 + 0.5; // 转换到[0,1]范围

        float sampleDepth = -texture(gPositionDepth, offset.xy).a;

        float rangeCheck = smoothstep(0.0, 1.0, radius / abs(fragPos.z - sampleDepth)); // 用于减少遮蔽的强度
        occlusion += (sampleDepth >= sample.z ? 1.0 : 0.0) * rangeCheck;   
    }
    occlusion = 1.0 - (occlusion / kernelSize); // 用1.0减去了遮蔽因子，以便直接使用遮蔽因子去缩放环境光照分量
    FragColor = occlusion;
}