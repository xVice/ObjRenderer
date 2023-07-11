using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;
using Assimp;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ObjRenderer
{
    public class ObjObject
    {
        float arrowSize = 10f; // Size of the arrowhead
        float arrowAngle = 20f; // Angle of the arrowhead
        float arrowLength = 50f; // Length of the arrow


        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; }
        }
        private bool _isSelected;

        public Mesh Mesh
        {
            get { return _mesh; }
        }

        public Vector3D Position
        {
            get { return _position; }
            set { _position = value; }
        }

        private Mesh _mesh;
        private Vector3D _position;
        private bool _isCulled;

        public ObjObject(string objPath, Vector3D position)
        {
            _isSelected = false;
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

        /*
        private Vector3D ComputeFaceNormal(Vector3D v1, Vector3D v2, Vector3D v3)
        {
            var normal = Vector3D.Cross(v2 - v1, v3 - v1);
            normal.Normalize();
            return normal;
        }
        */
        public Bitmap Render(float lightIntensity, int width, int height, TextureBrush texturebrush, Vector3D lightPos, Assimp.Matrix4x4 projectionMatrix, Assimp.Matrix4x4 viewMatrix, bool drawWireFrame, bool drawPolygonBoundingBox, bool drawLightingVectors, bool drawShadingOnly, bool drawSolid, bool enabledExpCulling)
        {
            var bmp = new Bitmap(width, height);
            var graphics = Graphics.FromImage(bmp);

            if (_isCulled)
                return null;

            var clientSize = new Size(width, height);

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

                    var vertex1 = Project(clientSize, vert1, projectionMatrix, viewMatrix);
                    var vertex2 = Project(clientSize, vert2, projectionMatrix, viewMatrix);
                    var vertex3 = Project(clientSize, vert3, projectionMatrix, viewMatrix);

                    // Apply object position to vertices
                    vertex1.X += _position.X;
                    vertex1.Y += _position.Y;
                    vertex2.X += _position.X;
                    vertex2.Y += _position.Y;
                    vertex3.X += _position.X;
                    vertex3.Y += _position.Y;

                    var points = new PointF[] { vertex1, vertex2, vertex3 };

                    var faceBoundingBox = ComputeBoundingBox(points);

                    if (enabledExpCulling)
                    {
                        if (!frustum.IsBoxVisible(frustum, faceBoundingBox))
                            continue;

                        if (!IsBackfaceVisible(vert1, vert2, vert3, viewMatrix))
                            continue;
                    }

                    var normal = ComputeFaceNormal(vert1, vert2, vert3);
                    var lightVectorNorm = lightPos - vert1;
                    lightVectorNorm.Normalize();
                    var dotProduct = Vector3D.Dot(normal, lightVectorNorm);

                    // Calculate the shade of gray based on the dot product
                    var shade = (int)((dotProduct * lightIntensity) + lightIntensity);
                    var color = Color.FromArgb((int)shade, (int)shade, (int)shade, (int)shade);

                    var brush = new SolidBrush(color);
                    var path = new GraphicsPath();
                    path.AddPolygon(points);

                    if (!drawShadingOnly && !drawSolid)
                    {
                        graphics.FillPath(texturebrush, path);
                        graphics.FillPath(brush, path);
                    }
                    else if (drawSolid)
                    {
                        graphics.FillPath(Brushes.Gray, path);
                        graphics.FillPath(brush, path);
                    }
                    else if (drawShadingOnly)
                    {
                        graphics.FillPath(brush, path);
                    }

                    if (drawWireFrame)
                    {
                        graphics.DrawPath(Pens.DarkRed, path);
                    }

                    if (_isSelected)
                    {
                        graphics.DrawPath(Pens.Black, path);
                        //  graphics.DrawLine();

                    }

                    brush.Dispose();
                    path.Dispose();

                

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

                if (drawPolygonBoundingBox)
                {
                    graphics.DrawRectangles(Pens.Green, boundingBoxRects.ToArray());
                }
            }

            return bmp;
        }



        private RectangleF ComputeBoundingBox(PointF[] points)
        {
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }
        private bool IsBackfaceVisible(Vector3D v1, Vector3D v2, Vector3D v3, Assimp.Matrix4x4 viewMatrix)
        {
            var normal = ComputeFaceNormal(v1, v2, v3);
            var viewDirection = new Vector3D(-viewMatrix.A3, -viewMatrix.B3, -viewMatrix.C3); // Invert view direction
            return Vector3D.Dot(normal, viewDirection) > 0; // Use > 0 for backface culling
        }


        private Vector3D ComputeFaceNormal(Vector3D v1, Vector3D v2, Vector3D v3)
        {
            var edge1 = v2 - v1;
            var edge2 = v3 - v1;
            var retVec = Vector3D.Cross(edge1, edge2);
            retVec.Normalize();
            return retVec;
          
        }
        public static PointF Project(Size vClientS, Vector3D vertex, Assimp.Matrix4x4 projectionMatrix, Assimp.Matrix4x4 viewMatrix)
        {
            // Apply transformations to the vertex
            var transformedVertex = viewMatrix * vertex;
            transformedVertex = projectionMatrix * transformedVertex;

            // Normalize the vertex coordinates and map them to screen space
            float x = transformedVertex.X * vClientS.Width / 2 + vClientS.Width / 2;
            float y = -transformedVertex.Y * vClientS.Height / 2 + vClientS.Height / 2;

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


        public bool IsBoxVisible(BoundingFrustum frustum, RectangleF boundingBox)
        {
            var boxPoints = new PointF[]
            {
        new PointF(boundingBox.Left, boundingBox.Top),
        new PointF(boundingBox.Right, boundingBox.Top),
        new PointF(boundingBox.Left, boundingBox.Bottom),
        new PointF(boundingBox.Right, boundingBox.Bottom)
            };

            return frustum.Intersects(boxPoints);
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


    public class ObjViewerControl : Form
    {
        private bool _needsRendering;
        private Timer _timer;


        private Vector3D _cameraPosition;
        private float _cameraRotationX;
        private float _cameraRotationY;

        private Assimp.Matrix4x4 projectionMatrix;
        private Assimp.Matrix4x4 viewMatrix;

        private ObjObject selectedObj;
        private float _lightIntesity;
        private float _cameraMoveSpeed;
        private const float CameraRotationSpeed = 0.001f;
        private float _cameraZoom;
        private bool drawWireFrame = false;
        private bool drawPolygonBoundingBox;
        private bool drawLightingVectors = false;
        private float CameraZoomSpeed = 0.0001f;
        private bool drawSolid = false;
        private bool drawShadingOnly = false;
        private Vector3D LightPosition { get; set; }
        private List<ObjObject> _objects;
        private TextureBrush textureBrush;
        private Point _lastMousePosition;
        private bool enableCulling = false;


     

        public ObjViewerControl(List<ObjObject> objects)
        {
            var textureImage = Image.FromFile("./text.jpg");

            AllowDrop = true;
            DoubleBuffered = true;
            // Create a texture brush from the texture image
            textureBrush = new TextureBrush(textureImage);

            Name = "ObjViewerForm";

            _lightIntesity = 100.0f;
            _cameraZoom = 1.0f;
            LightPosition = new Vector3D(0, 300, 0);
            drawPolygonBoundingBox = false;
            _cameraPosition = new Vector3D(0, 0, 0);
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
            this.MouseUp += ObjViewerForm_MouseUp;
            this.KeyDown += ObjViewerForm_KeyDown;
            this.KeyUp += ObjViewerForm_KeyUp;
            this.MouseWheel += ObjViewerForm_MouseWheel;
            this.DragEnter += ObjViewerForm_DragEnter;
            this.DragDrop += ObjViewerForm_DragDrop;
            this.MouseDown += ObjViewerForm_MouseDown;

            // Create ObjObject instances for each mesh in the scene
        }

        private void ObjViewerForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Iterate through the objects and check if the click falls inside their mesh
                foreach (var obj in _objects)
                {
                    if (IsPointInsideMesh(obj, e.Location))
                    {
                        obj.IsSelected = !obj.IsSelected;
                        break; // Exit the loop after the first object is clicked
                    }
                    else
                    {
                        obj.IsSelected = false;
                     
                   
                    }
                }

                _needsRendering = true;
            }
        }

        public static bool IsPointInPolygon(PointF[] polygon, PointF point)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddPolygon(polygon);

            Region region = new Region(path);

            return region.IsVisible(point);
        }

        private bool IsPointInsideMesh(ObjObject obj, Point point)
        {
            if (obj == null || obj.Mesh == null)
                return false;
            var mesh = obj.Mesh;

            foreach (var face in mesh.Faces)
            {
                var vert1 = mesh.Vertices[face.Indices[0]];
                var vert2 = mesh.Vertices[face.Indices[1]];
                var vert3 = mesh.Vertices[face.Indices[2]];

                // Apply object position to vertices
                vert1 += obj.Position;
                vert2 += obj.Position;
                vert3 += obj.Position;

                var points = new PointF[] { ObjObject.Project(ClientSize, vert1, projectionMatrix, viewMatrix),
                                            ObjObject.Project(ClientSize, vert2, projectionMatrix, viewMatrix),
                                            ObjObject.Project(ClientSize, vert3, projectionMatrix, viewMatrix) };

                if(IsPointInPolygon(points, point))
                {
                    return true;
                }

            
              
 
               
            }
            return false;
        }


        private void ObjViewerForm_DragEnter(object sender, DragEventArgs e)
        {
            // Check if the data being dragged contains file paths
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy; // Allow the drop
            else
                e.Effect = DragDropEffects.None; // Disallow the drop
        }

        public void ObjViewerForm_DragDrop(object sender, DragEventArgs e)
        {
            // Get the dragged text from the data
            string[] filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach(var path in filePaths)
            {
                LoadObject(new ObjObject(path, new Vector3D(0, 0, 0)));
            }
            _needsRendering = true;
        }


        public void LoadObject(ObjObject obj)
        {
            _objects.Add(obj);
        }

 

        public void ObjViewerForm_MouseWheel(object sender, MouseEventArgs e)
        {
            _cameraZoom += e.Delta * CameraZoomSpeed;
            _needsRendering = true;
        }

        public void ObjViewerForm_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Cursor.Show();
            }
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
                case Keys.X:
                    enableCulling = !enableCulling;
                    _needsRendering = true;
                    break;
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

            

            switch (e.KeyCode)
            {
                case Keys.W:
                    MoveCamera(0, 0, -_cameraMoveSpeed);
                    break;
                case Keys.S:
                    MoveCamera(0, 0, _cameraMoveSpeed);
                    break;
                case Keys.A:
                    MoveCamera(-_cameraMoveSpeed, 0, 0);
                    break;
                case Keys.D:
                    MoveCamera(_cameraMoveSpeed, 0, 0);
                    break;
                case Keys.Space:
                    MoveCamera(0, _cameraMoveSpeed, 0);
                    break;
                case Keys.ControlKey:
                    MoveCamera(0, -_cameraMoveSpeed, 0);
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
                case "setobjpos":

             

                    _objects.Where(x => x.IsSelected).FirstOrDefault().Position = new Vector3D(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
               
              
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
            if (ClientRectangle.Contains(e.Location))
            {
                if (Control.MouseButtons.HasFlag(MouseButtons.Left))
                {
                    float deltaX = e.X - ClientSize.Width / 2f;
                    float deltaY = e.Y - ClientSize.Height / 2f;
    

                    _cameraRotationX += deltaX * CameraRotationSpeed;
                    _cameraRotationY += deltaY * CameraRotationSpeed;
        

                    // Reset the cursor position to the center of the frame
                    Control mouseControl = FindForm() ?? (Control)this;
                    Point centerPoint = mouseControl.PointToScreen(new Point(ClientSize.Width / 2, ClientSize.Height / 2));
                    Cursor.Position = centerPoint;

                    _needsRendering = true;
                }
            }
        }
    

        private void MoveCamera(float offsetX, float offsetY, float offsetZ)
        {
            Vector3D moveVector = new Vector3D(offsetX, offsetY, offsetZ);

            // Convert the move vector from world space to camera space
            Assimp.Matrix4x4 rotationMatrix = Assimp.Matrix4x4.FromEulerAnglesXYZ(_cameraRotationX, _cameraRotationY, 0);
            moveVector = MultiplyVectorByMatrix(moveVector, rotationMatrix);

            _cameraPosition += moveVector;
            _needsRendering = true;
        }

        private Vector3D MultiplyVectorByMatrix(Vector3D vector, Assimp.Matrix4x4 matrix)
        {
            float x = (float)(vector.X * matrix.A1 + vector.Y * matrix.B1 + vector.Z * matrix.C1 + matrix.D1);
            float y = (float)(vector.X * matrix.A2 + vector.Y * matrix.B2 + vector.Z * matrix.C2 + matrix.D2);
            float z = (float)(vector.X * matrix.A3 + vector.Y * matrix.B3 + vector.Z * matrix.C3 + matrix.D3);

            return new Vector3D(x, y, z);
        }

        private Assimp.Matrix4x4 ComputeProjectionMatrix(float aspectRatio, float nearPlane, float farPlane)
        {
            float fov = MathHelper.ToRadians(60); // Example FOV of 60 degrees

            float scale = 1.0f / (float)Math.Tan(fov * 0.5f);

            Assimp.Matrix4x4 projectionMatrix = new Assimp.Matrix4x4
            {
                A1 = scale / aspectRatio,
                B2 = scale,
                C3 = -(farPlane + nearPlane) / (farPlane - nearPlane),
                C4 = -2.0f * farPlane * nearPlane / (farPlane - nearPlane),
                D3 = -1.0f
            };

            return projectionMatrix;
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_objects != null && _objects.Any())
            {
                float fov = MathHelper.ToRadians(60); // Example FOV of 60 degrees
                float aspectRatio = (float)ClientSize.Width / ClientSize.Height;
                float nearPlane = 0.1f;
                float farPlane = 100000.0f;

                projectionMatrix = ComputeProjectionMatrix(aspectRatio, nearPlane, farPlane);

                // Calculate the view matrix based on camera position and rotations
                viewMatrix = Assimp.Matrix4x4.FromTranslation(_cameraPosition) *
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
                     bmps.Add(obj.Render(_lightIntesity, ClientSize.Width, ClientSize.Height,textureBrush, LightPosition, projectionMatrix, viewMatrix, drawWireFrame, drawPolygonBoundingBox, drawLightingVectors, drawShadingOnly, drawSolid, enableCulling));
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
}