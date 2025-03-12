#version 330 core

out vec4 FragColor;
in vec3 WorldPos;

uniform samplerCube environmentMap;


const float PI = 3.14159265359;

void main()
{
    vec3 N = normalize(WorldPos);

    vec3 irradiance = vec3(0.0);   

    vec3 up = vec3(0.0, 1.0f, 0.0f);
    vec3 right = normalize(cross(up, N));
    up = normalize(cross(N, right));

    float sampleDelta = 0.025;
    float nrSamples = 0.0;
    for (float phi = 0; phi < 2.0 * PI; phi += sampleDelta) {
        for (float theta = 0; theta < 0.5 * PI; theta += sampleDelta) {
            vec3 tangentSample = vec3(sin(theta) * cos(phi),  sin(theta) * sin(phi), cos(theta)); // 切线空间的采样向量
            vec3 sampleVec = tangentSample.x * right + tangentSample.y * up + tangentSample.z * N; // 转到世界空间

            irradiance += texture(environmentMap, sampleVec).rgb * cos(theta) * sin(theta); // theta越大，半球采样区域越小，用sin(theta)平衡区域贡献度
            nrSamples++;
        }
    }
    irradiance = PI * irradiance * (1.0 / nrSamples); // 乘以PI，然后除以采样次数

    FragColor = vec4(irradiance, 1.0);
}