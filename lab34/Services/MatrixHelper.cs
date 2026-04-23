using System.Numerics;

namespace lab34.Services
{
    public static class MatrixHelper
    {
        public static Matrix4x4 CreateModelMatrix(Vector3 modelCenter, float rotationX, float rotationY, float offsetX, float offsetY)
        {
            Matrix4x4 toCenter = Matrix4x4.CreateTranslation(-modelCenter);
            Matrix4x4 rotation = Matrix4x4.CreateRotationY(rotationY) * Matrix4x4.CreateRotationX(rotationX);
            Matrix4x4 translation = Matrix4x4.CreateTranslation(offsetX, offsetY, 0);
            return translation * rotation * toCenter;
        }

        public static Matrix4x4 CreateViewMatrix(Vector3 cameraPos, Vector3 target)
        {
            return Matrix4x4.CreateLookAt(cameraPos, target, Vector3.UnitY);
        }

        public static Matrix4x4 CreateProjectionMatrix(float width, float height, float fov = MathF.PI / 4f, float nearPlane = 0.1f, float farPlane = 100f)
        {
            float aspect = width / height;
            return Matrix4x4.CreatePerspectiveFieldOfView(fov, aspect, nearPlane, farPlane);
        }

        public static Vector4 ProjectToClip(Vector3 vertex, Matrix4x4 mvp)
        {
            return Vector4.Transform(new Vector4(vertex, 1.0f), mvp);
        }
    }
}