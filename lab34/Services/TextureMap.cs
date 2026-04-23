using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace lab34.Services
{
    public class TextureMap
    {
        private readonly int _width;
        private readonly int _height;
        private readonly uint[] _pixels;

        public TextureMap(string filePath)
        {
            var bitmap = new BitmapImage(new Uri(filePath, UriKind.Absolute));
            var cb = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr32, null, 0);
            _width = cb.PixelWidth;
            _height = cb.PixelHeight;
            _pixels = new uint[_width * _height];
            cb.CopyPixels(new Int32Rect(0, 0, _width, _height), _pixels, _width * 4, 0);
        }

        public Vector3 Sample(Vector2 uv)
        {
            float u = uv.X - (float)Math.Floor(uv.X);
            float v = 1.0f - (uv.Y - (float)Math.Floor(uv.Y)); 

            int x = Math.Clamp((int)(u * (_width - 1)), 0, _width - 1);
            int y = Math.Clamp((int)(v * (_height - 1)), 0, _height - 1);

            uint color = _pixels[y * _width + x];
            return new Vector3(
                ((color >> 16) & 0xFF) / 255.0f,
                ((color >> 8) & 0xFF) / 255.0f,
                (color & 0xFF) / 255.0f
            );
        }
    }
}