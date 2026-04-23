using System;
using System.Numerics;

namespace lab34.Services
{
    public unsafe class Rasterizer
    {
        private int _width;
        private int _height;
        private float[] _zBuffer;
        private Vector3 _cameraPos;
        private Vector3 _lightPos;
        private Vector3 _lightColor;
        private Vector3 _objectColor;

        public Rasterizer(int width, int height, float[] zBuffer, Vector3 cameraPos, Vector3 lightPos, Vector3 lightColor, Vector3 objectColor)
        {
            _width = width;
            _height = height;
            _zBuffer = zBuffer;
            _cameraPos = cameraPos;
            _lightPos = lightPos;
            _lightColor = lightColor;
            _objectColor = objectColor;
        }

        public void UpdateSize(int width, int height, float[] zBuffer)
        {
            _width = width;
            _height = height;
            _zBuffer = zBuffer;
        }

        public void UpdateCamera(Vector3 cameraPos)
        {
            _cameraPos = cameraPos;
        }

        public void FillTriangle(int* buffer, int stride,
            float x1, float y1, float z1,
            float x2, float y2, float z2,
            float x3, float y3, float z3,
            Vector3 n1, Vector3 n2, Vector3 n3,
            Vector3 p1, Vector3 p2, Vector3 p3)
        {
            if (y1 > y2) { Swap(ref x1, ref x2); Swap(ref y1, ref y2); Swap(ref z1, ref z2); Swap(ref n1, ref n2); Swap(ref p1, ref p2); }
            if (y1 > y3) { Swap(ref x1, ref x3); Swap(ref y1, ref y3); Swap(ref z1, ref z3); Swap(ref n1, ref n3); Swap(ref p1, ref p3); }
            if (y2 > y3) { Swap(ref x2, ref x3); Swap(ref y2, ref y3); Swap(ref z2, ref z3); Swap(ref n2, ref n3); Swap(ref p2, ref p3); }

            int iy1 = (int)y1, iy2 = (int)y2, iy3 = (int)y3;
            float totalH = y3 - y1;
            if (totalH < 1f) return;

            for (int y = iy1; y <= iy3; y++)
            {
                if (y < 0 || y >= _height) continue;

                bool inBottom = y <= iy2;
                float alpha = (y - y1) / totalH;
                float beta = inBottom
                    ? ((y2 - y1) < 1f ? 0f : (y - y1) / (y2 - y1))
                    : ((y3 - y2) < 1f ? 0f : (y - y2) / (y3 - y2));

                float ax = x1 + (x3 - x1) * alpha;
                float az = z1 + (z3 - z1) * alpha;
                Vector3 an = Vector3.Normalize(n1 + (n3 - n1) * alpha);
                Vector3 ap = p1 + (p3 - p1) * alpha;

                float bx = inBottom ? x1 + (x2 - x1) * beta : x2 + (x3 - x2) * beta;
                float bz = inBottom ? z1 + (z2 - z1) * beta : z2 + (z3 - z2) * beta;
                Vector3 bn = Vector3.Normalize(inBottom ? n1 + (n2 - n1) * beta : n2 + (n3 - n2) * beta);
                Vector3 bp = inBottom ? p1 + (p2 - p1) * beta : p2 + (p3 - p2) * beta;

                if (ax > bx)
                {
                    Swap(ref ax, ref bx);
                    Swap(ref az, ref bz);
                    Swap(ref an, ref bn);
                    Swap(ref ap, ref bp);
                }

                int ixStart = Math.Max((int)ax, 0);
                int ixEnd = Math.Min((int)bx, _width - 1);
                float dx = bx - ax;

                for (int x = ixStart; x <= ixEnd; x++)
                {
                    float t = dx < 1f ? 0f : (x - ax) / dx;
                    float z = az + (bz - az) * t;

                    int idx = y * _width + x;
                    if (z < _zBuffer[idx])
                    {
                        _zBuffer[idx] = z;
                        Vector3 norm = Vector3.Normalize(an + (bn - an) * t);
                        Vector3 pos = ap + (bp - ap) * t;
                        buffer[y * stride + x] = LightingHelper.ComputePhongColor(norm, pos, _lightPos, _lightColor, _objectColor, _cameraPos);
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

        private void Swap<T>(ref T a, ref T b)
        {
            (a, b) = (b, a);
        }
    }
}