using System;
using System.IO;
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

        private TextureMap? _diffuseMap;
        private TextureMap? _normalMap;
        private TextureMap? _specularMap;

        private float _rotationX;
        private float _rotationY;
        private float _modelOffsetX;
        private float _modelOffsetY;
        private Vector3 _cameraPos = new Vector3(0, 0, 5);
        private Vector3 _modelCenter = Vector3.Zero;
        private float _modelSize = 1f;
        
        private const float RotationSpeedMouse = 0.01f;
        private const float ZoomSpeed = 0.5f;
        private const float PanSpeed = 0.02f;
        
        private bool _isDragging;
        private Point _lastMousePos;
        private bool _isRendering;
        private float[]? _zBuffer;
        private bool _wireframeMode;

        private readonly Vector3 _lightPos = new Vector3(0f, 0f, 5f);
        private readonly Vector3 _lightColor = new Vector3(1f, 1f, 1f);
        private readonly Vector3 _objectColor = new Vector3(0f, 0f, 1f);

        private Rasterizer? _rasterizer;
        private FrustumCuller _frustumCuller = new FrustumCuller();

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
                    _rasterizer = new Rasterizer(_width, _height, _zBuffer, _cameraPos, _lightPos, _lightColor);
                }
            }
        }

        private void BtnLoadDiffuse_Click(object sender, RoutedEventArgs e) { _diffuseMap = OpenTex(); RenderModel(_model); }
        private void BtnLoadNormal_Click(object sender, RoutedEventArgs e) { _normalMap = OpenTex(); RenderModel(_model); }
        private void BtnLoadSpecular_Click(object sender, RoutedEventArgs e) { _specularMap = OpenTex(); RenderModel(_model); }

        private TextureMap? OpenTex()
        {
            var op = new OpenFileDialog { Filter = "Images|*.jpg;*.png;*.bmp;*.tga" };
            return op.ShowDialog() == true ? new TextureMap(op.FileName) : null;
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select 3D Model File",
                Filter = "3D Model Files (*.obj)|*.obj|All Files (*.*)|*.*",
                DefaultExt = ".obj"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _currentFilePath = openFileDialog.FileName;
                LoadModel(_currentFilePath);
            }
        }

        private void BtnResetCamera_Click(object sender, RoutedEventArgs e) => ResetCameraAndModel();

        private void BtnWireframe_Click(object sender, RoutedEventArgs e)
        {
            _wireframeMode = !_wireframeMode;
            RenderModel(_model);
        }

        private void ResetCameraAndModel()
        {
            float distance = _modelSize * 0.8f;
            _cameraPos = new Vector3(_modelCenter.X, _modelCenter.Y, distance);
            _rotationX = 0; _rotationY = 0;
            _modelOffsetX = 0; _modelOffsetY = 0;
            _rasterizer?.UpdateCamera(_cameraPos);
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
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void CalculateModelBounds(ModelData model)
        {
            if (model.Vertices.Length == 0) return;
            Vector3 min = model.Vertices[0], max = model.Vertices[0];
            foreach (var v in model.Vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
            _modelCenter = (min + max) / 2;
            _modelSize = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e) { if (_model != null) { _isDragging = true; _lastMousePos = e.GetPosition(this); Mouse.Capture(this); } }
        private void OnMouseUp(object sender, MouseButtonEventArgs e) { _isDragging = false; Mouse.Capture(null); }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _model == null) return;
            Point currentPos = e.GetPosition(this);
            Vector2 delta = new Vector2((float)(currentPos.X - _lastMousePos.X), (float)(currentPos.Y - _lastMousePos.Y));

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _rotationY += delta.X * RotationSpeedMouse;
                _rotationX += delta.Y * RotationSpeedMouse;
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                _modelOffsetX += delta.X * PanSpeed;
                _modelOffsetY -= delta.Y * PanSpeed;
            }
            _lastMousePos = currentPos;
            RenderModel(_model);
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_model == null) return;
            _cameraPos.Z += -e.Delta * ZoomSpeed * 0.01f;
            _rasterizer?.UpdateCamera(_cameraPos);
            RenderModel(_model);
        }

        private void RenderModel(ModelData? model)
        {
            if (model == null || _bitmap == null || _zBuffer == null || _isRendering) return;
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

                    Matrix4x4 modelMatrix = MatrixHelper.CreateModelMatrix(_modelCenter, _rotationX, _rotationY, _modelOffsetX, _modelOffsetY);
                    Matrix4x4 view = MatrixHelper.CreateViewMatrix(_cameraPos, _modelCenter);
                    Matrix4x4 projection = MatrixHelper.CreateProjectionMatrix(_width, _height);
                    Matrix4x4 mvp = modelMatrix * view * projection;

                    if (_wireframeMode) RenderWireframe(model, buffer, stride, mvp);
                    else RenderSolid(model, buffer, stride, modelMatrix, mvp);
                }
                _bitmap.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
            }
            finally { _bitmap.Unlock(); _isRendering = false; }
        }

        private unsafe void RenderSolid(ModelData model, int* buffer, int stride, Matrix4x4 modelMatrix, Matrix4x4 mvp)
        {
            foreach (var face in model.Faces)
            {
                Vector4 c1 = Vector4.Transform(new Vector4(model.Vertices[face.V1], 1), mvp);
                Vector4 c2 = Vector4.Transform(new Vector4(model.Vertices[face.V2], 1), mvp);
                Vector4 c3 = Vector4.Transform(new Vector4(model.Vertices[face.V3], 1), mvp);

                if (!_frustumCuller.IsTriangleInFrustum(c1, c2, c3)) continue;
                if (c1.W < 0.01f || c2.W < 0.01f || c3.W < 0.01f) continue;

                var v1 = PrepareVertex(model, face.V1, face.T1, face.N1, c1, modelMatrix);
                var v2 = PrepareVertex(model, face.V2, face.T2, face.N2, c2, modelMatrix);
                var v3 = PrepareVertex(model, face.V3, face.T3, face.N3, c3, modelMatrix);

                _rasterizer?.FillTriangle(buffer, stride, v1, v2, v3, _diffuseMap, _normalMap, _specularMap, modelMatrix);
            }
        }

        private Rasterizer.Vertex PrepareVertex(ModelData m, int vIdx, int tIdx, int nIdx, Vector4 clip, Matrix4x4 modelM)
        {
            float wi = 1.0f / clip.W;
            Vector3 wp = Vector3.Transform(m.Vertices[vIdx], modelM);
            Vector3 nm = Vector3.TransformNormal(m.Normals[nIdx], modelM);
            
            Vector2 uv = (m.TexCoords != null && m.TexCoords.Length > tIdx && tIdx >= 0) 
                         ? m.TexCoords[tIdx] 
                         : Vector2.Zero;

            return new Rasterizer.Vertex {
                Screen = new Vector2((clip.X * wi + 1) * 0.5f * _width, (1 - clip.Y * wi) * 0.5f * _height),
                WInv = wi,
                UVW = uv * wi,
                NormalW = nm * wi,
                PosW = wp * wi
            };
        }
private void BtnLoadAllTextures_Click(object sender, RoutedEventArgs e)
{
    var op = new OpenFileDialog 
    { 
        Title = "Выберите диффузную карту (Diffuse Map)",
        Filter = "Images|*.jpg;*.png;*.bmp;*.tga" 
    };

    if (op.ShowDialog() == true)
    {
        try
        {
            string diffPath = op.FileName;
            string directory = Path.GetDirectoryName(diffPath)!;
            string fileName = Path.GetFileNameWithoutExtension(diffPath);
            string ext = Path.GetExtension(diffPath);

            _diffuseMap = new TextureMap(diffPath);

            string baseName = fileName.Replace("_diffuse", "").Replace("_diff", "").Replace("_d", "").Replace("_color", "");

            string[] normalSuffixes = { "_normal", "_norm", "_n", "_nm" };
            _normalMap = null; 
            foreach (var s in normalSuffixes)
            {
                string path = Path.Combine(directory, baseName + s + ext);
                if (File.Exists(path))
                {
                    _normalMap = new TextureMap(path);
                    break;
                }
            }

            string[] specularSuffixes = { "_specular", "_spec", "_s", "_sp" };
            _specularMap = null; 
            foreach (var s in specularSuffixes)
            {
                string path = Path.Combine(directory, baseName + s + ext);
                if (File.Exists(path))
                {
                    _specularMap = new TextureMap(path);
                    break;
                }
            }

            MessageBox.Show($"Текстуры загружены:\nDiffuse: Да\nNormal: {(_normalMap != null ? "Найдена" : "Нет")}\nSpecular: {(_specularMap != null ? "Найдена" : "Нет")}");
            
            RenderModel(_model);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при автозагрузке текстур: {ex.Message}");
        }
    }
}
        private unsafe void RenderWireframe(ModelData model, int* buffer, int stride, Matrix4x4 mvp)
        {
            int wireColor = unchecked((int)0xFFFFFFFF);
            foreach (var face in model.Faces)
            {
                Vector4 c1 = Vector4.Transform(new Vector4(model.Vertices[face.V1], 1), mvp);
                Vector4 c2 = Vector4.Transform(new Vector4(model.Vertices[face.V2], 1), mvp);
                Vector4 c3 = Vector4.Transform(new Vector4(model.Vertices[face.V3], 1), mvp);

                if (c1.W < 0.1f || c2.W < 0.1f || c3.W < 0.1f) continue;

                int sx1 = (int)((c1.X / c1.W + 1) * 0.5f * _width), sy1 = (int)((1 - c1.Y / c1.W) * 0.5f * _height);
                int sx2 = (int)((c2.X / c2.W + 1) * 0.5f * _width), sy2 = (int)((1 - c2.Y / c2.W) * 0.5f * _height);
                int sx3 = (int)((c3.X / c3.W + 1) * 0.5f * _width), sy3 = (int)((1 - c3.Y / c3.W) * 0.5f * _height);

                _rasterizer?.DrawLine(buffer, sx1, sy1, sx2, sy2, wireColor, stride);
                _rasterizer?.DrawLine(buffer, sx2, sy2, sx3, sy3, wireColor, stride);
                _rasterizer?.DrawLine(buffer, sx3, sy3, sx1, sy1, wireColor, stride);
            }
        }
    }
}