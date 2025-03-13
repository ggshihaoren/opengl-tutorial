#version 330 core

out vec4 FragColor;
in vec3 WorldPos;

uniform samplerCube environmentMap;
uniform float roughness;

const float PI = 3.14159265359;

// 这一步是将渲染方程拆成的两部分中第一部分sum{L_i dot dw_i},被称为预滤波环境贴图，是预先计算的环境卷积贴图

float DistributionGGX(vec3 N, vec3 H, float roughness)
{ 
    // 法线分布函数Trowbridge-Reitz GGX
    // NDF = alpha^2 / (pi * (dot(N, H)^2 * (alpha^2 - 1) + 1)^2)
    // h表示半角向量，N表示法线，alpha表示粗糙度，
    float a = roughness * roughness; // 法线分布函数中采用粗糙度的平方会让光照看起来更加自然
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}

// Hammersley 序列是基于 Van Der Corput 序列，该序列是把十进制数字的二进制表示镜像翻转到小数点右边而得到的
float RadicalInverse_VdC(uint bits) 
{
     bits = (bits << 16u) | (bits >> 16u);
     bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
     bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
     bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
     bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
     return float(bits) * 2.3283064365386963e-10; // / 0x100000000
}

// 总样本数为 N，样本索引为 i
vec2 Hammersley(uint i, uint N)
{
    return vec2(float(i)/float(N), RadicalInverse_VdC(i));
}

vec3 ImportanceSampleGGX(vec2 Xi, vec3 N, float roughness)
{
	float a = roughness*roughness;
	
	float phi = 2.0 * PI * Xi.x;
	float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a*a - 1.0) * Xi.y));
	float sinTheta = sqrt(1.0 - cosTheta*cosTheta);
	
	// from spherical coordinates to cartesian coordinates - halfway vector
	vec3 H;
	H.x = cos(phi) * sinTheta;
	H.y = sin(phi) * sinTheta;
	H.z = cosTheta;
	
	// from tangent-space H vector to world-space sample vector
	vec3 up          = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
	vec3 tangent   = normalize(cross(up, N));
	vec3 bitangent = cross(N, tangent);
	
	vec3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
	return normalize(sampleVec);
}

void main()
{
    vec3 N = normalize(WorldPos);

    vec3 R = N;
    vec3 V = R; // 简化使得光线方向等于视线方向

    const uint SAMPLE_COUNT = 1024u;
    vec3 prefilteredColor = vec3(0.0);
    float totalWeight = 0.0;

    for(uint i = 0u; i < SAMPLE_COUNT; ++i) {
        vec2 Xi = Hammersley(i, SAMPLE_COUNT);
        vec3 H = ImportanceSampleGGX(Xi, N, roughness);
        vec3 L  = normalize(2.0 * dot(V, H) * H - V);

        float NdotL = max(dot(N, L), 0.0);
        if(NdotL > 0.0)
        {
            float D   = DistributionGGX(N, H, roughness);
            float NdotH = max(dot(N, H), 0.0);
            float HdotV = max(dot(H, V), 0.0);
            float pdf = D * NdotH / (4.0 * HdotV) + 0.0001;  // pdf和L的分布成正比

            float resolution = 512.0; 
            float saTexel  = 4.0 * PI / (6.0 * resolution * resolution); // 6个面，每个面resolution * resolution个texel, 计算每个texel的面积
            float saSample = 1.0 / (float(SAMPLE_COUNT) * pdf + 0.0001); // 每个样本的面积

            float mipLevel = roughness == 0.0 ? 0.0 : 0.5 * log2(saSample / saTexel);  // 计算mipLevel， 如果一个样本有多个texel，那么这个样本的mipLevel就会更低
            
            // 在预过滤卷积时，不直接采样环境贴图，而是基于积分的 PDF 和粗糙度采样环境贴图的 mipmap ，以减少伪像
            prefilteredColor += textureLod(environmentMap, L, mipLevel).rgb * NdotL; // 根据mipLevel采样
            totalWeight      += NdotL;
        }
    }
    prefilteredColor = prefilteredColor / totalWeight;
    FragColor = vec4(prefilteredColor, 1.0);
}