using System;
using System.Numerics;

namespace lab34.Services
{
    public static class LightingHelper
    {
        public const float Ka = 0.15f;
        public const float Kd = 0.8f;
        public const float Ks = 0.5f;
        public const float Shininess = 32f;

        public static int ComputePhongColor(Vector3 normal, Vector3 fragPos, Vector3 lightPos, Vector3 lightColor, Vector3 objectColor, Vector3 cameraPos)
        {
            Vector3 ambient = Ka * lightColor;

            Vector3 lightDir = Vector3.Normalize(lightPos - fragPos);
            float diff = MathF.Max(Vector3.Dot(normal, lightDir), 0f);
            Vector3 diffuse = Kd * diff * lightColor;

            Vector3 viewDir = Vector3.Normalize(cameraPos - fragPos);
            Vector3 reflectDir = Vector3.Reflect(-lightDir, normal);
            float spec = MathF.Pow(MathF.Max(Vector3.Dot(viewDir, reflectDir), 0f), Shininess);
            Vector3 specular = Ks * spec * lightColor;

            Vector3 result = (ambient + diffuse + specular) * objectColor;

            int r = (int)(Math.Clamp(result.X, 0f, 1f) * 255f);
            int g = (int)(Math.Clamp(result.Y, 0f, 1f) * 255f);
            int b = (int)(Math.Clamp(result.Z, 0f, 1f) * 255f);

            return unchecked((int)(0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b));
        }
    }
}