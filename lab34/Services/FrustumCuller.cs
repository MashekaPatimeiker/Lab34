using System.Numerics;

namespace lab34.Services
{
    public sealed class FrustumCuller
    {
        public bool IsTriangleInFrustum(Vector4 clip0, Vector4 clip1, Vector4 clip2)
        {
            if (AreAllVerticesOutsideLeft(clip0, clip1, clip2)) return false;
            if (AreAllVerticesOutsideRight(clip0, clip1, clip2)) return false;
            if (AreAllVerticesOutsideBottom(clip0, clip1, clip2)) return false;
            if (AreAllVerticesOutsideTop(clip0, clip1, clip2)) return false;
            if (AreAllVerticesOutsideNear(clip0, clip1, clip2)) return false;
            if (AreAllVerticesOutsideFar(clip0, clip1, clip2)) return false;
            return true;
        }

        private bool AreAllVerticesOutsideLeft(Vector4 v0, Vector4 v1, Vector4 v2)
            => v0.X < -v0.W && v1.X < -v1.W && v2.X < -v2.W;

        private bool AreAllVerticesOutsideRight(Vector4 v0, Vector4 v1, Vector4 v2)
            => v0.X > v0.W && v1.X > v1.W && v2.X > v2.W;

        private bool AreAllVerticesOutsideBottom(Vector4 v0, Vector4 v1, Vector4 v2)
            => v0.Y < -v0.W && v1.Y < -v1.W && v2.Y < -v2.W;

        private bool AreAllVerticesOutsideTop(Vector4 v0, Vector4 v1, Vector4 v2)
            => v0.Y > v0.W && v1.Y > v1.W && v2.Y > v2.W;

        private bool AreAllVerticesOutsideNear(Vector4 v0, Vector4 v1, Vector4 v2)
            => v0.Z < 0 && v1.Z < 0 && v2.Z < 0;

        private bool AreAllVerticesOutsideFar(Vector4 v0, Vector4 v1, Vector4 v2)
            => v0.Z > v0.W && v1.Z > v1.W && v2.Z > v2.W;
    }
}