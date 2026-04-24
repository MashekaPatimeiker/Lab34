using System;
using System.Numerics;

namespace lab34.Services
{
    public unsafe class Rasterizer
    {
        private int _width, _height;
        private float[] _zBuffer;
        private Vector3 _cameraPos, _lightPos, _lightColor;

        public Rasterizer(int width, int height, float[] zBuffer, Vector3 cameraPos, Vector3 lightPos, Vector3 lightColor)
        {
            UpdateSize(width, height, zBuffer);
            _cameraPos = cameraPos;
            _lightPos = lightPos;
            _lightColor = lightColor;
        }

        public void UpdateSize(int w, int h, float[] zb) { _width = w; _height = h; _zBuffer = zb; }
        public void UpdateCamera(Vector3 cp) => _cameraPos = cp;

        public struct Vertex {
            public Vector2 Screen;
            public float WInv; 
            public Vector2 UVW;
            public Vector3 NormalW;
            public Vector3 PosW;
        }

        public void FillTriangle(int* buffer, int stride, Vertex v1, Vertex v2, Vertex v3, 
            TextureMap? diffMap, TextureMap? normMap, TextureMap? specMap, Matrix4x4 modelMatrix)
        {
            if (v1.Screen.Y > v2.Screen.Y) Swap(ref v1, ref v2);
            if (v1.Screen.Y > v3.Screen.Y) Swap(ref v1, ref v3);
            if (v2.Screen.Y > v3.Screen.Y) Swap(ref v2, ref v3);

            int yStart = (int)Math.Max(0, Math.Ceiling(v1.Screen.Y));
            int yEnd = (int)Math.Min(_height - 1, Math.Floor(v3.Screen.Y));

            for (int y = yStart; y <= yEnd; y++)
            {
                bool bottom = y > v2.Screen.Y || v2.Screen.Y == v1.Screen.Y;
                float hSum = bottom ? v3.Screen.Y - v2.Screen.Y : v2.Screen.Y - v1.Screen.Y;
                float t1 = (y - v1.Screen.Y) / (v3.Screen.Y - v1.Screen.Y);
                float t2 = (y - (bottom ? v2.Screen.Y : v1.Screen.Y)) / hSum;

                Vertex a = Lerp(v1, v3, t1);
                Vertex b = bottom ? Lerp(v2, v3, t2) : Lerp(v1, v2, t2);
                if (a.Screen.X > b.Screen.X) Swap(ref a, ref b);

                int xStart = (int)Math.Max(0, Math.Ceiling(a.Screen.X));
                int xEnd = (int)Math.Min(_width - 1, Math.Floor(b.Screen.X));

                for (int x = xStart; x <= xEnd; x++)
                {
                    float tx = (x - a.Screen.X) / (b.Screen.X - a.Screen.X);
                    if (a.Screen.X == b.Screen.X) tx = 1;

                    float wInv = a.WInv + (b.WInv - a.WInv) * tx;
                    float z = 1.0f / wInv;

                    if (z < _zBuffer[y * _width + x])
                    {
                        _zBuffer[y * _width + x] = z;
                        Vector2 uv = (a.UVW + (b.UVW - a.UVW) * tx) * z;
                        Vector3 pos = (a.PosW + (b.PosW - a.PosW) * tx) * z;
                        Vector3 norm = Vector3.Normalize((a.NormalW + (b.NormalW - a.NormalW) * tx) * z);

                        Vector3 color = diffMap?.Sample(uv) ?? new Vector3(0, 0, 1);
                        if (normMap != null) {
                            Vector3 sampled = normMap.Sample(uv);
                            Vector3 modelNorm = Vector3.Normalize(sampled * 2.0f - Vector3.One);
                            norm = Vector3.Normalize(Vector3.TransformNormal(modelNorm, modelMatrix));
                        }
                        float ks = specMap?.Sample(uv).X ?? 0.5f;

                        buffer[y * stride + x] = LightingHelper.ComputePhongColor(norm, pos, _lightPos, _lightColor, color, ks, _cameraPos);
                    }
                }
            }
        }
        public void DrawLine(int* buffer, int x1, int y1, int x2, int y2, int color, int stride)
        {
            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                if (x1 >= 0 && x1 < _width && y1 >= 0 && y1 < _height)
                {
                    buffer[y1 * stride + x1] = color;
                }
                if (x1 == x2 && y1 == y2) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x1 += sx; }
                if (e2 < dx) { err += dx; y1 += sy; }
            }
        }
        private Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex {
            Screen = Vector2.Lerp(a.Screen, b.Screen, t),
            WInv = a.WInv + (b.WInv - a.WInv) * t,
            UVW = Vector2.Lerp(a.UVW, b.UVW, t),
            NormalW = Vector3.Lerp(a.NormalW, b.NormalW, t),
            PosW = Vector3.Lerp(a.PosW, b.PosW, t)
        };

        private void Swap<T>(ref T a, ref T b) { var t = a; a = b; b = t; }
    }
}