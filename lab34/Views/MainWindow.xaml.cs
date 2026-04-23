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
        private float _modelOffsetX;
        private float _modelOffsetY;
        private Vector3 _originalCameraPos = new Vector3(0, 0, 5);
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
                    _rasterizer = new Rasterizer(_width, _height, _zBuffer, _cameraPos, _lightPos, _lightColor, _objectColor);
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
            float distance = _modelSize * 0.8f;
            _cameraPos = new Vector3(_modelCenter.X, _modelCenter.Y, distance);
            _originalCameraPos = _cameraPos;
            _rotationX = 0;
            _rotationY = 0;
            _modelOffsetX = 0;
            _modelOffsetY = 0;
            
            if (_rasterizer != null)
            {
                _rasterizer.UpdateCamera(_cameraPos);
            }
            
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
            
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                _rotationY += (float)delta.X * RotationSpeedMouse;
                _rotationX += (float)delta.Y * RotationSpeedMouse;
                _rotationX = Math.Clamp(_rotationX, -MathF.PI / 2.5f, MathF.PI / 2.5f);
            }
            
            if (Mouse.RightButton == MouseButtonState.Pressed)
            {
                _modelOffsetX += (float)delta.X * PanSpeed;
                _modelOffsetY -= (float)delta.Y * PanSpeed;
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
            
            if (_rasterizer != null)
            {
                _rasterizer.UpdateCamera(_cameraPos);
            }
            
            RenderModel(_model);
        }

        private Matrix4x4 CreateFullMatrix()
        {
            Matrix4x4 model = MatrixHelper.CreateModelMatrix(_modelCenter, _rotationX, _rotationY, _modelOffsetX, _modelOffsetY);
            Matrix4x4 view = MatrixHelper.CreateViewMatrix(_cameraPos, _modelCenter);
            Matrix4x4 projection = MatrixHelper.CreateProjectionMatrix(_width, _height);
            return model * view * projection;
        }

        private void RenderModel(ModelData? model)
        {
            if (model == null || _bitmap == null || _zBuffer == null || _isRendering) return;
            if (_width <= 0 || _height <= 0) return;
            if (_rasterizer == null) return;
            
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
                Vector4 c1 = MatrixHelper.ProjectToClip(model.Vertices[face.V1], mvp);
                Vector4 c2 = MatrixHelper.ProjectToClip(model.Vertices[face.V2], mvp);
                Vector4 c3 = MatrixHelper.ProjectToClip(model.Vertices[face.V3], mvp);

                if (!_frustumCuller.IsTriangleInFrustum(c1, c2, c3)) continue;

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

                _rasterizer?.FillTriangle(buffer, stride,
                    sx1, sy1, z1, sx2, sy2, z2, sx3, sy3, z3,
                    wn1, wn2, wn3, w1, w2, w3);
            }
        }

        private unsafe void RenderWireframe(ModelData model, int* buffer, int stride, Matrix4x4 mvp)
        {
            int wireColor = unchecked((int)0xFFFFFFFF);

            foreach (var face in model.Faces)
            {
                Vector4 c1 = MatrixHelper.ProjectToClip(model.Vertices[face.V1], mvp);
                Vector4 c2 = MatrixHelper.ProjectToClip(model.Vertices[face.V2], mvp);
                Vector4 c3 = MatrixHelper.ProjectToClip(model.Vertices[face.V3], mvp);

                if (!_frustumCuller.IsTriangleInFrustum(c1, c2, c3)) continue;
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

                _rasterizer?.DrawLine(buffer, sx1, sy1, sx2, sy2, wireColor, stride);
                _rasterizer?.DrawLine(buffer, sx2, sy2, sx3, sy3, wireColor, stride);
                _rasterizer?.DrawLine(buffer, sx3, sy3, sx1, sy1, wireColor, stride);
            }
        }
    }
}