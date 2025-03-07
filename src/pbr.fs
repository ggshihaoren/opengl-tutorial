#version 330 core

out vec4 FragColor;
in vec2 TexCoords;
in vec3 WorldPos;
in vec3 Normal;

uniform sampler2D albedoMap;
uniform sampler2D normalMap;
uniform sampler2D metallicMap;
uniform sampler2D roughnessMap;
uniform sampler2D aoMap;

uniform vec3 lightPositions[4];
uniform vec3 lightColors[4];

uniform vec3 camPos;

const float PI = 3.14159265359;

// fragment shader需要纹理坐标，点世界坐标，法线，光照位置，光照颜色，相机位置，以及材质属性（贴图）

vec3 getNormalFromMap()
{ // 通过法线，坐标点，纹理坐标计算TNB,将切线空间的法线转到世界空间
    vec3 tangentNormal = texture(normalMap, TexCoords).xyz * 2.0 - 1.0;

    vec3 Q1  = dFdx(WorldPos);
    vec3 Q2  = dFdy(WorldPos);
    vec2 st1 = dFdx(TexCoords);
    vec2 st2 = dFdy(TexCoords);

    vec3 N   = normalize(Normal);
    vec3 T  = normalize(Q1*st2.t - Q2*st1.t);
    vec3 B  = -normalize(cross(N, T));
    mat3 TBN = mat3(T, B, N);

    return normalize(TBN * tangentNormal);
}

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

float GeometrySchlickGGX(float NdotV, float roughness)
{
    // 几何函数Schlick-GGX,从统计学上近似的求得了微平面间相互遮蔽的比率，这种相互遮蔽会损耗光线的能量。
    // GGX = dot(N, V) / (dot(N, V) *(1 - k) + k)
    // k是对alpha的重映射，k_{direct} = (alpha + 1) ^ 2 / 8, k_{IBL} = alpha ^ 2 / 2

    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;

    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    // 需要将观察方向（几何遮蔽(Geometry Obstruction)）和光线方向向量（几何阴影(Geometry Shadowing)）都考虑进去，最后需要的即二者的乘积
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);

    return ggx1 * ggx2;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    // 菲涅尔方程描述的是被反射的光线对比光线被折射的部分所占的比率，这个比率会随着我们观察的角度不同而不同
    // 用Fresnel-Schlick近似法求得近似解
    // F = F0 + (1 - F0) * (1 - cosTheta)^5)
    // cosTheta表示入射角和法线的夹角，F0表示表面的基础反射率
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

void main()
{
    vec3 albedo = pow(texture(albedoMap, TexCoords).rgb, vec3(2.2)); // gamma矫正
    float metallic = texture(metallicMap, TexCoords).r;
    float roughness = texture(roughnessMap, TexCoords).r;
    float ao = texture(aoMap, TexCoords).r;

    vec3 N = getNormalFromMap();
    vec3 V = normalize(camPos - WorldPos);

    // calculate reflectance at normal incidence; if dia-electric (like plastic) use F0 
    // of 0.04 and if it's a metal, use the albedo color as F0 (metallic workflow)   
    // 计算平面的基础反射率， 大多数电介质表面而言使用0.04作为基础反射率已经足够,金属表面使用albedo作为基础反射率
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic);

    // 反射方程
    vec3 Lo = vec3(0.0);
    for (int i = 0; i < 4; i++) {
        // 计算每一条光线的radiance
        vec3 L = normalize(lightPositions[i] - WorldPos);
        vec3 H = normalize(V + L);
        float distance = length(lightPositions[i] - WorldPos);
        float attenuation = 1.0 / (distance * distance); // 衰减
        vec3 radiance = lightColors[i] * attenuation; // 光照到该点的颜色
    
        // Cook-Torrance BRDF
        // 有漫反射和镜面反射两部分
        // fr = kd * f_{lamb} + ks * f_{spec}
        // kd是漫反射率，f_{lamb}是漫反射函数，ks是镜面反射率，f_{spec}是镜面反射函数
        // f_{lamb} = albedo / PI
        // f_{spec} = DFG / (4 * dot(N, V) * dot(N, L))
        float NDF = DistributionGGX(N, H, roughness);
        float G   = GeometrySmith(N, V, L, roughness);
        vec3 F    = fresnelSchlick(clamp(dot(H, V), 0.0, 1.0), F0);
        vec3 DFG = NDF * G * F;
        float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001;
        vec3 f_spec = DFG / denominator;

        vec3 KS = F; // 镜面反射率
        vec3 KD = vec3(1.0) - KS; // 漫反射率
        KD *= 1.0 - metallic; // 金属表面不会有漫反射

        // 渲染方程为Lo = sum(fr * radiance * dot(N, L))

        vec3 fr = KS * f_spec + KD * (albedo / PI);
        float NdotL = max(dot(N, L), 0.0);
        Lo += fr * radiance * NdotL;
    }

    // ambient lighting

    vec3 ambient = vec3(0.03) * albedo * ao;
    vec3 color = ambient + Lo;

    // HDR tonemapping
    color = color / (color + vec3(1.0));

    // gamma correct
    color = pow(color, vec3(1.0/2.2)); 

    FragColor = vec4(color, 1.0);
}