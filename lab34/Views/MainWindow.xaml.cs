using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using lab34.Models;
using lab34.Services;
using System.Windows.Input;

namespace lab34.Views
{
    public partial class MainWindow : Window
    {
        private WriteableBitmap? _bitmap;
        private int _width;
        private int _height;
        private ModelData? _model;
        private string? _currentFilePath;

        private float _rotationX;
        private float _rotationY;
        private float _modelOffsetX = 0f;  // Смещение модели по горизонтали
        private float _modelOffsetY = 0f;  // Смещение модели по вертикали
        private Vector3 _originalCameraPos = new Vector3(0, 0, 5);
        private Vector3 _cameraPos = new Vector3(0, 0, 5);
        private Vector3 _modelCenter = Vector3.Zero;
        private float _modelSize = 1f;
        
        private const float RotationSpeedMouse = 0.01f;
        private const float ZoomSpeed = 0.5f;
        private const float PanSpeed = 0.02f;  // Скорость перемещения модели
        
        private bool _isDragging;
        private Point _lastMousePos;
        private bool _isRendering;
        private float[]? _zBuffer;
        private bool _wireframeMode;

        private readonly Vector3 _lightPos = new Vector3(2f, 4f, 3f);
        private readonly Vector3 _lightColor = new Vector3(1f, 1f, 1f);
        private readonly Vector3 _objectColor = new Vector3(0.8f, 0.7f, 0.6f);

        private const float Ka = 0.15f;
        private const float Kd = 0.8f;
        private const float Ks = 0.5f;
        private const float Shininess = 32f;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnWindowLoaded;
            SizeChanged += OnWindowSizeChanged;
            
            MouseDown += OnMouseDown;
            MouseUp += OnMouseUp;
            MouseMove += OnMouseMove;
            MouseWheel += OnMouseWheel;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            UpdateSize();
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSize();
            if (_model != null)
            {
                RenderModel(_model);
            }
        }

        private void UpdateSize()
        {
            if (gridDisplay.ActualWidth > 0 && gridDisplay.ActualHeight > 0)
            {
                _width = (int)gridDisplay.ActualWidth;
                _height = (int)gridDisplay.ActualHeight;

                if (_width > 0 && _height > 0)
                {
                    _bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgr32, null);
                    imageContainer.Source = _bitmap;
                    _zBuffer = new float[_width * _height];
                }
            }
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select 3D Model File",
                Filter = "3D Model Files (*.obj)|*.obj|All Files (*.*)|*.*",
                DefaultExt = ".obj",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _currentFilePath = openFileDialog.FileName;
                LoadModel(_currentFilePath);
            }
        }

        private void BtnResetCamera_Click(object sender, RoutedEventArgs e)
        {
            ResetCameraAndModel();
        }

        private void BtnWireframe_Click(object sender, RoutedEventArgs e)
        {
            _wireframeMode = !_wireframeMode;
            RenderModel(_model);
        }

        private void ResetCameraAndModel()
        {
            float distance = _modelSize * 1.5f;
            _cameraPos = new Vector3(_modelCenter.X, _modelCenter.Y, distance);
            _originalCameraPos = _cameraPos;
            _rotationX = 0;
            _rotationY = 0;
            _modelOffsetX = 0; 
            _modelOffsetY = 0;  
            RenderModel(_model);
        }

        private void LoadModel(string filePath)
        {
            try
            {
                _model = ObjLoader.Load(filePath);
             
                
                CalculateModelBounds(_model);
                ResetCameraAndModel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CalculateModelBounds(ModelData model)
        {
            if (model.Vertices.Length == 0) return;

            Vector3 min = model.Vertices[0];
            Vector3 max = model.Vertices[0];

            foreach (var vertex in model.Vertices)
            {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            _modelCenter = (min + max) / 2;
            _modelSize = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
            
            if (_modelSize < 0.001f) _modelSize = 1f;
            
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_model == null) return;
            
            if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _lastMousePos = e.GetPosition(this);
                Mouse.Capture(this);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            Mouse.Capture(null);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _model == null) return;
            
            Point currentPos = e.GetPosition(this);
            Point delta = new Point(
                currentPos.X - _lastMousePos.X,
                currentPos.Y - _lastMousePos.Y
            );
            
            // Вращение модели при зажатой ЛЕВОЙ кнопке
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                _rotationY += (float)delta.X * RotationSpeedMouse;
                _rotationX += (float)delta.Y * RotationSpeedMouse;
                _rotationX = Math.Clamp(_rotationX, -MathF.PI / 2.5f, MathF.PI / 2.5f);
            }
            
            // Перемещение модели по экрану при зажатой ПРАВОЙ кнопке
            if (Mouse.RightButton == MouseButtonState.Pressed)
            {
                _modelOffsetX += (float)delta.X * PanSpeed;  // Движение вправо/влево
                _modelOffsetY -= (float)delta.Y * PanSpeed;  // Движение вверх/вниз
            }
            
            _lastMousePos = currentPos;
            RenderModel(_model);
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_model == null) return;
            
            float zoomDelta = -e.Delta * ZoomSpeed * 0.01f;
            _cameraPos.Z += zoomDelta;
            _cameraPos.Z = Math.Clamp(_cameraPos.Z, _modelSize * 0.5f, _modelSize * 10f);
            
            RenderModel(_model);
        }
        

        private Matrix4x4 CreateModelMatrix()
        {
            // Сначала смещаем модель в центр координат
            Matrix4x4 toCenter = Matrix4x4.CreateTranslation(-_modelCenter);
            
            // Затем применяем вращение
            Matrix4x4 rotation = Matrix4x4.CreateRotationY(_rotationY) * Matrix4x4.CreateRotationX(_rotationX);
            
            // Затем перемещаем модель по экрану (в плоскости X-Y экрана)
            Matrix4x4 translation = Matrix4x4.CreateTranslation(_modelOffsetX, _modelOffsetY, 0);
            
            return translation * rotation * toCenter;
        }

        private Matrix4x4 CreateFullMatrix()
        {
            Matrix4x4 model = CreateModelMatrix();
            Vector3 target = _modelCenter;
            Matrix4x4 view = Matrix4x4.CreateLookAt(_cameraPos, target, Vector3.UnitY);
            float aspect = (float)_width / _height;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, aspect, 0.1f, 100f);
            return model * view * projection;
        }

        private Vector4 ProjectToClip(Vector3 vertex, Matrix4x4 mvp)
        {
            return Vector4.Transform(new Vector4(vertex, 1.0f), mvp);
        }

        private void RenderModel(ModelData? model)
        {
            if (model == null || _bitmap == null || _zBuffer == null || _isRendering) return;
            if (_width <= 0 || _height <= 0) return;
            
            _isRendering = true;

            try
            {
                _bitmap.Lock();
                
                unsafe
                {
                    int* buffer = (int*)_bitmap.BackBuffer;
                    int stride = _bitmap.BackBufferStride / 4;

                    NativeMemory.Clear(buffer, (nuint)(_height * stride * sizeof(int)));
                    Array.Fill(_zBuffer, float.MaxValue);

                    Matrix4x4 modelMatrix = CreateModelMatrix();
                    Matrix4x4 mvp = CreateFullMatrix();

                    if (_wireframeMode)
                    {
                        RenderWireframe(model, buffer, stride, mvp);
                    }
                    else
                    {
                        RenderSolid(model, buffer, stride, modelMatrix, mvp);
                    }
                }

                _bitmap.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
            }
            finally
            {
                _bitmap.Unlock();
                _isRendering = false;
            }
        }

        private unsafe void RenderSolid(ModelData model, int* buffer, int stride, 
            Matrix4x4 modelMatrix, Matrix4x4 mvp)
        {
            foreach (var face in model.Faces)
            {
                Vector4 c1 = ProjectToClip(model.Vertices[face.V1], mvp);
                Vector4 c2 = ProjectToClip(model.Vertices[face.V2], mvp);
                Vector4 c3 = ProjectToClip(model.Vertices[face.V3], mvp);

                if (c1.W < 0.1f || c2.W < 0.1f || c3.W < 0.1f) continue;

                float nx1 = c1.X / c1.W, ny1 = c1.Y / c1.W;
                float nx2 = c2.X / c2.W, ny2 = c2.Y / c2.W;
                float nx3 = c3.X / c3.W, ny3 = c3.Y / c3.W;

                float sx1 = (nx1 + 1f) * 0.5f * _width;
                float sy1 = (1f - ny1) * 0.5f * _height;
                float sx2 = (nx2 + 1f) * 0.5f * _width;
                float sy2 = (1f - ny2) * 0.5f * _height;
                float sx3 = (nx3 + 1f) * 0.5f * _width;
                float sy3 = (1f - ny3) * 0.5f * _height;

                Vector3 w1 = Vector3.Transform(model.Vertices[face.V1], modelMatrix);
                Vector3 w2 = Vector3.Transform(model.Vertices[face.V2], modelMatrix);
                Vector3 w3 = Vector3.Transform(model.Vertices[face.V3], modelMatrix);

                Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(w2 - w1, w3 - w1));
                Vector3 viewDir = Vector3.Normalize(w1 - _cameraPos);
                if (Vector3.Dot(faceNormal, viewDir) >= 0) continue;

                float z1 = c1.Z / c1.W;
                float z2 = c2.Z / c2.W;
                float z3 = c3.Z / c3.W;

                Vector3 wn1 = Vector3.Normalize(Vector3.TransformNormal(model.Normals[face.N1], modelMatrix));
                Vector3 wn2 = Vector3.Normalize(Vector3.TransformNormal(model.Normals[face.N2], modelMatrix));
                Vector3 wn3 = Vector3.Normalize(Vector3.TransformNormal(model.Normals[face.N3], modelMatrix));

                FillTriangleZ(buffer, stride,
                    sx1, sy1, z1, sx2, sy2, z2, sx3, sy3, z3,
                    wn1, wn2, wn3, w1, w2, w3);
            }
        }

        private unsafe void RenderWireframe(ModelData model, int* buffer, int stride, Matrix4x4 mvp)
        {
            int wireColor = unchecked((int)0xFFFFFFFF);  

            foreach (var face in model.Faces)
            {
                Vector4 c1 = ProjectToClip(model.Vertices[face.V1], mvp);
                Vector4 c2 = ProjectToClip(model.Vertices[face.V2], mvp);
                Vector4 c3 = ProjectToClip(model.Vertices[face.V3], mvp);

                if (c1.W < 0.1f || c2.W < 0.1f || c3.W < 0.1f) continue;

                float nx1 = c1.X / c1.W, ny1 = c1.Y / c1.W;
                float nx2 = c2.X / c2.W, ny2 = c2.Y / c2.W;
                float nx3 = c3.X / c3.W, ny3 = c3.Y / c3.W;

                int sx1 = (int)((nx1 + 1f) * 0.5f * _width);
                int sy1 = (int)((1f - ny1) * 0.5f * _height);
                int sx2 = (int)((nx2 + 1f) * 0.5f * _width);
                int sy2 = (int)((1f - ny2) * 0.5f * _height);
                int sx3 = (int)((nx3 + 1f) * 0.5f * _width);
                int sy3 = (int)((1f - ny3) * 0.5f * _height);

                DrawLineBresenham(buffer, sx1, sy1, sx2, sy2, wireColor, stride);
                DrawLineBresenham(buffer, sx2, sy2, sx3, sy3, wireColor, stride);
                DrawLineBresenham(buffer, sx3, sy3, sx1, sy1, wireColor, stride);
            }
        }

        private unsafe void DrawLineBresenham(int* buffer, int x1, int y1, int x2, int y2, int color, int stride)
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

        private int ComputePhongColor(Vector3 normal, Vector3 fragPos)
        {
            Vector3 ambient = Ka * _lightColor;

            Vector3 lightDir = Vector3.Normalize(_lightPos - fragPos);
            float diff = MathF.Max(Vector3.Dot(normal, lightDir), 0f);
            Vector3 diffuse = Kd * diff * _lightColor;

            Vector3 viewDir = Vector3.Normalize(_cameraPos - fragPos);
            Vector3 reflectDir = Vector3.Reflect(-lightDir, normal);
            float spec = MathF.Pow(MathF.Max(Vector3.Dot(viewDir, reflectDir), 0f), Shininess);
            Vector3 specular = Ks * spec * _lightColor;

            Vector3 result = (ambient + diffuse + specular) * _objectColor;

            int r = (int)(Math.Clamp(result.X, 0f, 1f) * 255f);
            int g = (int)(Math.Clamp(result.Y, 0f, 1f) * 255f);
            int b = (int)(Math.Clamp(result.Z, 0f, 1f) * 255f);

            return unchecked((int)(0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b));
        }

        private unsafe void FillTriangleZ(int* buffer, int stride,
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
                    if (z < _zBuffer![idx])
                    {
                        _zBuffer[idx] = z;
                        Vector3 norm = Vector3.Normalize(an + (bn - an) * t);
                        Vector3 pos = ap + (bp - ap) * t;
                        buffer[y * stride + x] = ComputePhongColor(norm, pos);
                    }
                }
            }
        }

        private void Swap<T>(ref T a, ref T b)
        {
            (a, b) = (b, a);
        }
    }
}