using System;
using System.Numerics;

namespace lab34.Services
{
    public static class LightingHelper
    {
        public static int ComputePhongColor(Vector3 normal, Vector3 fragPos, Vector3 lightPos, 
            Vector3 lightColor, Vector3 objectColor, float ks, Vector3 cameraPos)
        {
            const float ka = 0.15f;
            const float kd = 0.8f;
            const float shininess = 32f;

            Vector3 ambient = ka * lightColor;
            Vector3 lightDir = Vector3.Normalize(lightPos - fragPos);
            float diff = MathF.Max(Vector3.Dot(normal, lightDir), 0f);
            Vector3 diffuse = kd * diff * lightColor;

            Vector3 viewDir = Vector3.Normalize(cameraPos - fragPos);
            Vector3 reflectDir = Vector3.Reflect(-lightDir, normal);
            float spec = MathF.Pow(MathF.Max(Vector3.Dot(viewDir, reflectDir), 0f), shininess);
            Vector3 specular = ks * spec * lightColor;

            Vector3 result = (ambient + diffuse + specular) * objectColor;

            int r = (int)(Math.Clamp(result.X, 0f, 1f) * 255f);
            int g = (int)(Math.Clamp(result.Y, 0f, 1f) * 255f);
            int b = (int)(Math.Clamp(result.Z, 0f, 1f) * 255f);

            return unchecked((int)(0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b));
        }
    }
}