using System;
using System.Collections.Generic;
using System.Drawing;

using System.Linq;


using System.Windows.Forms;
using Assimp;
using Assimp.Configs;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace ObjRenderer
{
    public class ObjObject
    {
        private Mesh _mesh;
        private Vector3D _position;
        private bool _isCulled;

        public ObjObject(string objPath, Vector3D position)
        {
            _mesh = LoadMesh(objPath);
            _position = position;
            _isCulled = false;
        }

        private Mesh LoadMesh(string path)
        {
            var context = new AssimpContext();
            var scene = context.ImportFile(path, PostProcessPreset.TargetRealTimeMaximumQuality);
            return scene.Meshes.FirstOrDefault();
        }

        private Vector3D ComputeFaceNormal(Vector3D v1, Vector3D v2, Vector3D v3)
        {
            var normal = Vector3D.Cross(v2 - v1, v3 - v1);
            normal.Normalize();
            return normal;
        }

        public Bitmap Render(int widht, int height, TextureBrush texturebrush, Vector3D lightPos, Assimp.Matrix4x4 projectionMatrix, Assimp.Matrix4x4 viewMatrix, bool drawWireFrame, bool drawPolygonBoundingBox, bool drawLightingVectors, bool drawShadingOnly, bool drawSolid)
        {
            var bmp = new Bitmap(widht, height);
            var graphics = Graphics.FromImage(bmp);


            if (_isCulled)
                return null;
          

            if (_mesh != null)
            {
                var frustum = new BoundingFrustum(viewMatrix * projectionMatrix);
                var colors = new List<Color>();


                var wireframePoints = new List<PointF>();
                var boundingBoxRects = new List<RectangleF>();

                foreach (var face in _mesh.Faces)
                {
                    var vert1 = _mesh.Vertices[face.Indices[0]];
                    var vert2 = _mesh.Vertices[face.Indices[1]];
                    var vert3 = _mesh.Vertices[face.Indices[2]];

                    var vertex1 = Project(vert1, projectionMatrix, viewMatrix);
                    var vertex2 = Project(vert2, projectionMatrix, viewMatrix);
                    var vertex3 = Project(vert3, projectionMatrix, viewMatrix);

                    // Apply object position to vertices
                    vertex1.X += _position.X;
                    vertex1.Y += _position.Y;
                    vertex2.X += _position.X;
                    vertex2.Y += _position.Y;
                    vertex3.X += _position.X;
                    vertex3.Y += _position.Y;

                    var points = new PointF[] { vertex1, vertex2, vertex3 };

                    //if (!frustum.Intersects(points)) // Perform frustum culling
                    //    continue;

                    var normal = ComputeFaceNormal(vert1, vert2, vert3);
                    var lightVectorNorm = lightPos - vert1;
                    lightVectorNorm.Normalize();
                    var dotProduct = Vector3D.Dot(normal, lightVectorNorm);

                    // Calculate the shade of gray based on the dot product
                    var shade = (int)(dotProduct * 125) + 125;
                    var color = Color.FromArgb(shade, shade, shade, shade);

                    var brush = new SolidBrush(color);
                    if (!drawShadingOnly && !drawSolid)
                    {
                        graphics.FillPolygon(texturebrush, points);
                        graphics.FillPolygon(brush, points);
                    }
                    else if (drawSolid)
                    {
                        graphics.FillPolygon(Brushes.White, points);
                        graphics.FillPolygon(brush, points);
                   
                    }
                    else if (drawShadingOnly)
                    {
                 
                        graphics.FillPolygon(brush, points);
                     
                    }
    

                    brush.Dispose();
          

      

                    if (drawWireFrame)
                    {
                        wireframePoints.Add(vertex1);
                        wireframePoints.Add(vertex2);
                        wireframePoints.Add(vertex3);
                   
                

                    }

                    if (drawPolygonBoundingBox)
                    {
                        var minX = points.Min(p => p.X);
                        var maxX = points.Max(p => p.X);
                        var minY = points.Min(p => p.Y);
                        var maxY = points.Max(p => p.Y);
                        var boundingBox = new RectangleF(minX, minY, maxX - minX, maxY - minY);
                        boundingBoxRects.Add(boundingBox);
                    }

                    if (drawLightingVectors)
                    {
                        var startPoint = vertex1;
                        var lightVector = lightPos - vert1;
                        lightVector.Normalize();
                        var endPoint = new PointF(vertex1.X + lightVector.X * 50, vertex1.Y - lightVector.Y * 50);
                        graphics.DrawLine(Pens.Yellow, startPoint, endPoint);
                    }
                }


                if (drawWireFrame)
                {
                    graphics.DrawLines(Pens.Cyan, wireframePoints.ToArray());
                }

                if (drawPolygonBoundingBox)
                {
                    graphics.DrawRectangles(Pens.Green, boundingBoxRects.ToArray());
                }
            }
            return bmp;
        }

        private PointF Project(Vector3D vertex, Assimp.Matrix4x4 projectionMatrix, Assimp.Matrix4x4 viewMatrix)
        {
            var _viewer = Application.OpenForms.Cast<ObjViewerForm>().FirstOrDefault(form => form.Name == "ObjViewerForm");
            // Apply transformations to the vertex
            var transformedVertex = viewMatrix * vertex;
            transformedVertex = projectionMatrix * transformedVertex;

            // Normalize the vertex coordinates and map them to screen space
            float x = transformedVertex.X * _viewer.ClientSize.Width / 2 + _viewer.ClientSize.Width / 2;
            float y = -transformedVertex.Y * _viewer.ClientSize.Height / 2 + _viewer.ClientSize.Height / 2;

            return new PointF(x, y);
        }
    }


    public class BoundingFrustum
    {
        private Assimp.Matrix4x4 _matrix;
        private Plane[] _planes;

        public BoundingFrustum(Assimp.Matrix4x4 matrix)
        {
            _matrix = matrix;
            _planes = ExtractPlanes(matrix);
        }


        private Plane[] ExtractPlanes(Assimp.Matrix4x4 matrix)
        {
            Plane[] planes = new Plane[6];

            // Left plane
            planes[0] = new Plane(
                matrix.A4 + matrix.A1,
                matrix.B4 + matrix.B1,
                matrix.C4 + matrix.C1,
                matrix.D4 + matrix.D1
            );

            // Right plane
            planes[1] = new Plane(
                matrix.A4 - matrix.A1,
                matrix.B4 - matrix.B1,
                matrix.C4 - matrix.C1,
                matrix.D4 - matrix.D1
            );

            // Bottom plane
            planes[2] = new Plane(
                matrix.A4 + matrix.A2,
                matrix.B4 + matrix.B2,
                matrix.C4 + matrix.C2,
                matrix.D4 + matrix.D2
            );

            // Top plane
            planes[3] = new Plane(
                matrix.A4 - matrix.A2,
                matrix.B4 - matrix.B2,
                matrix.C4 - matrix.C2,
                matrix.D4 - matrix.D2
            );

            // Near plane
            planes[4] = new Plane(
                matrix.A3,
                matrix.B3,
                matrix.C3,
                matrix.D3
            );

            // Far plane
            planes[5] = new Plane(
                matrix.A4 - matrix.A3,
                matrix.B4 - matrix.B3,
                matrix.C4 - matrix.C3,
                matrix.D4 - matrix.D3
            );

            return planes;
        }


        public bool Intersects(PointF[] points)
        {
            foreach (Plane plane in _planes)
            {
                if (IsInFrustum(plane, points))
                    return true;
            }
            return false;
        }

        private bool IsInFrustum(Plane plane, PointF[] points)
        {
            Vector3D planeNormal = new Vector3D(plane.A, plane.B, plane.C);

            foreach (PointF point in points)
            {
                Vector3D point3D = new Vector3D(point.X, point.Y, 0);
                float dotProduct = Vector3D.Dot(planeNormal, point3D) + plane.D;

                if (dotProduct < 0)
                    return false;
            }

            return true;
        }
    }


    public class ObjViewerForm : Form
    {
        private bool _needsRendering;
        private Timer _timer;


        private Vector3D _cameraPosition;
        private float _cameraRotationX;
        private float _cameraRotationY;
        private float _cameraMoveSpeed;
        private const float CameraRotationSpeed = 0.00001f;
        private float _cameraZoom;
        private bool drawWireFrame = false;
        private bool drawPolygonBoundingBox;
        private bool drawLightingVectors = false;
        private float CameraZoomSpeed = 0.0001f;
        private bool drawSolid = false;
        private bool drawShadingOnly = false;
        private Vector3D _lastCamPos;
        private Vector3D LightPosition { get; set; }
        private List<ObjObject> _objects;
        private TextureBrush textureBrush;

        public ObjViewerForm(List<ObjObject> objects)
        {
            var textureImage = Image.FromFile("./text.jpg");

            DoubleBuffered = true;
            // Create a texture brush from the texture image
            textureBrush = new TextureBrush(textureImage);

            Name = "ObjViewerForm";
   
            _cameraZoom = 1.0f;
            LightPosition = new Vector3D(5, 5, 5);
            drawPolygonBoundingBox = false;
            _cameraPosition = new Vector3D(0, 0, 0);
            _lastCamPos = _cameraPosition;
            _cameraRotationX = 0f;
            _cameraRotationY = 0f;
            _cameraMoveSpeed = 0.1f;
            _needsRendering = true;
            _timer = new Timer();
            _objects = objects;

            // Set up the form
            this.Text = "OBJ Viewer Demo";
            this.ClientSize = new Size(800, 600);
            this.BackColor = Color.Black;

            // Set up the rendering timer
            _timer.Interval = 16; // ~60 FPS
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Set up event handlers
            this.MouseMove += ObjViewerForm_MouseMove;
            this.KeyDown += ObjViewerForm_KeyDown;
            this.KeyUp += ObjViewerForm_KeyUp;
            this.MouseWheel += ObjViewerForm_MouseWheel;

            // Create ObjObject instances for each mesh in the scene
        }

        public void ObjViewerForm_MouseWheel(object sender, MouseEventArgs e)
        {
            _cameraZoom += e.Delta * CameraZoomSpeed;
            _needsRendering = true;
        }

        public class BoundingBox
        {
            public Vector3D Min { get; set; }
            public Vector3D Max { get; set; }

            public BoundingBox()
            {
                Min = new Vector3D(float.MaxValue, float.MaxValue, float.MaxValue);
                Max = new Vector3D(float.MinValue, float.MinValue, float.MinValue);
            }
        }

        private void ObjViewerForm_KeyUp(object sender, KeyEventArgs e)
        {
            // Update camera movement based on key releases
            switch (e.KeyCode)
            {
                case Keys.C:
                    drawWireFrame = !drawWireFrame;
                    _needsRendering = true;
                    break;
                case Keys.V:
                    // Toggle the bounding box flags
                    drawPolygonBoundingBox = !drawPolygonBoundingBox;
                    _needsRendering = true;
                    break;
                case Keys.B:
                    drawLightingVectors = !drawLightingVectors;
                    _needsRendering = true;
                    break;
                case Keys.N:
                    drawSolid = !drawSolid;
                    _needsRendering = true;
                    break;
                case Keys.M:
                    drawShadingOnly = !drawShadingOnly;
                    _needsRendering = true;
                    break;
            }

        }



        private void ObjViewerForm_KeyDown(object sender, KeyEventArgs e)
        {
            // Update camera movement based on key presses
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.A:
                    MoveCamera(-_cameraMoveSpeed, 0f, 0);
                    break;
                case Keys.Right:
                case Keys.D:
                    MoveCamera(_cameraMoveSpeed, 0f, 0);
                    break;
                case Keys.Up:
                case Keys.W:
                    // Reduce the camera's zoom level
                    MoveCamera(0f, -_cameraMoveSpeed, 0);

                    break;
                case Keys.Down:
                case Keys.S:
                    // Reduce the camera's zoom level
                    MoveCamera(0f, _cameraMoveSpeed, 0);
                    break;

            }
        }

        private void ExecuteCommand(string command)
        {
            string[] parts = command.Split(' ');
            switch (parts[0])
            {
                
                case "echo":
                    switch (parts[1])
                    {
                        case "camrot":
                            Console.WriteLine($"{_cameraRotationX.ToString()} | {_cameraRotationY.ToString()}");
                            break;
                        case "campos":
                            Console.WriteLine($"{_cameraPosition.ToString()}");
                            break;
                        case "lightpos":
                            Console.WriteLine($"{LightPosition.ToString()}");
                            break;
                    }
                    break;
                case "light":
                    LightPosition = new Vector3D(int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
                    Console.WriteLine("Updated lightpos");
                    _needsRendering = true;
                    break;

                case "camrot":
                    _cameraRotationX = float.Parse(parts[1]);
                    _cameraRotationY = float.Parse(parts[1]);
                    Console.WriteLine("Updated Cam rotation");
                    _needsRendering = true;
                    break;
                case "campos":
                    _cameraPosition = new Vector3D(int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
                    Console.WriteLine("Updated Cam Position");
                    _needsRendering = true;
                    break;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Render the scene if needed
            if (_needsRendering)
            {
                this.Invalidate();
                _needsRendering = false;
            }

            if (Console.KeyAvailable)
            {
                string command = Console.ReadLine();
                ExecuteCommand(command);
            }

        }

        private void ObjViewerForm_MouseMove(object sender, MouseEventArgs e)
        {
            // Update the camera rotation based on mouse movement while left button is pressed
            if (e.Button == MouseButtons.Left)
            {
                _cameraRotationX += CameraRotationSpeed * e.X;
                _cameraRotationY += CameraRotationSpeed * e.Y;
                _needsRendering = true;
            }
        }

        private void MoveCamera(float offsetX, float offsetY, float offsetZ)
        {
            Vector3D moveVector = new Vector3D(offsetX, offsetY, offsetZ);


            _cameraPosition += moveVector;
            _needsRendering = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_objects != null && _objects.Any())
            {
                // Calculate the aspect ratio based on the client size
                float aspectRatio = (float)ClientSize.Width / ClientSize.Height;
                float left = -5 * aspectRatio;
                float right = 5 * aspectRatio;
                float bottom = -5;
                float top = 5;
                float near = 0.1f;
                float far = 100000.0f;

                // Set up the projection matrix as orthographic
                Assimp.Matrix4x4 projectionMatrix = new Assimp.Matrix4x4(
                    2 / (right - left), 0, 0, -(right + left) / (right - left),
                    0, 2 / (top - bottom), 0, -(top + bottom) / (top - bottom),
                    0, 0, -2 / (far - near), -(far + near) / (far - near),
                    0, 0, 0, 1
                );

                // Calculate the view matrix based on camera position and rotations
                var viewMatrix = Assimp.Matrix4x4.FromTranslation(_cameraPosition) *
                                 Assimp.Matrix4x4.FromRotationX(_cameraRotationX) *
                                 Assimp.Matrix4x4.FromRotationY(_cameraRotationY);
                               



                // Apply the zoom factor to the translation part of the view matrix


                viewMatrix.A1 *= _cameraZoom;
                viewMatrix.A2 *= _cameraZoom;
                viewMatrix.A3 *= _cameraZoom;
                viewMatrix.A4 *= _cameraZoom;

                viewMatrix.B1 *= _cameraZoom;
                viewMatrix.B2 *= _cameraZoom;
                viewMatrix.B3 *= _cameraZoom;
                viewMatrix.B4 *= _cameraZoom;

                viewMatrix.C1 *= _cameraZoom;
                viewMatrix.C2 *= _cameraZoom;
                viewMatrix.C3 *= _cameraZoom;
                viewMatrix.C4 *= _cameraZoom;

                viewMatrix.D1 *= _cameraZoom;
                viewMatrix.D2 *= _cameraZoom;
                viewMatrix.D3 *= _cameraZoom;
                viewMatrix.D4 *= _cameraZoom;


                // Clear the buffer
                e.Graphics.Clear(Color.FromArgb(30,30,30));

                var bmps = new List<Bitmap>();
                // Render each object in the scene
                foreach (var obj in _objects)
                {
                     bmps.Add(obj.Render(ClientSize.Width, ClientSize.Height,textureBrush, LightPosition, projectionMatrix, viewMatrix, drawWireFrame, drawPolygonBoundingBox, drawLightingVectors, drawShadingOnly, drawSolid));
                }
                foreach(var bmp in bmps)
                {
                    e.Graphics.DrawImage(bmp, new PointF(0, 0));
                }

            }
        }



    }



    public static class MathHelper
    {
        public const float Pi = (float)Math.PI;
        public const float TwoPi = 2 * Pi;

        public static float ToRadians(float degrees)
        {
            return degrees * Pi / 180.0f;
        }

        public static float ToDegrees(float radians)
        {
            return radians * 180.0f / Pi;
        }
    }




    public class Program
    {
        [STAThread]
        public static void Main()
        {
            List<ObjObject> objects = new List<ObjObject>();

            // Create ObjObject instances and add them to the list
            //objects.Add(new ObjObject("./DocBrown.obj", new Vector3(0, 0, 0)));
            objects.Add(new ObjObject("./teapot.obj", new Vector3D(0, 0, 0)));
            //objects.Add(new ObjObject("./teapot.obj", new Vector3D(-375, 0, 0)));


            Application.Run(new ObjViewerForm(objects));
        }
    }
}