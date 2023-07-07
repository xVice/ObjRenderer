using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Assimp;
using Assimp.Configs;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace ObjRenderer
{
    public class ObjViewerForm : Form
    {
        private AssimpContext _context;
        private Scene _scene;
        private bool _needsRendering;
        private Timer _timer;

        private Vector3 _cameraPosition;
        private float _cameraRotationX;
        private float _cameraRotationY;
        private float _cameraMoveSpeed;
        private const float CameraRotationSpeed = 0.00001f;
        private float _cameraZoom;
        private float _cameraZoomSpeed;
        private const float CameraZoomSpeed = 0.0001f;
        private bool drawWireFrame = false;
        private Vector3 LightPosition { get; set; }

        public ObjViewerForm(string path)
        {
            _context = new AssimpContext();
            _scene = null;
            _cameraZoom = 1.0f;
            _cameraZoomSpeed = 0.0001f;
            LightPosition = new Vector3(5, 5, 5);
            _cameraPosition = new Vector3(0f, 0f, -5f);
            _cameraRotationX = 0f;
            _cameraRotationY = 0f;
            _cameraMoveSpeed = 0.1f;
            _needsRendering = true;
            _timer = new Timer();

            // Set up the form
            this.Text = "OBJ Viewer Demo";
            this.ClientSize = new Size(800, 600);
            this.BackColor = Color.Black;

            // Load the OBJ file
            _scene = _context.ImportFile(path, PostProcessPreset.TargetRealTimeMaximumQuality);

            // Set up the rendering timer
            _timer.Interval = 16; // ~60 FPS
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Set up event handlers
            this.MouseMove += ObjViewerForm_MouseMove;
            this.KeyDown += ObjViewerForm_KeyDown;
            this.KeyUp += ObjViewerForm_KeyUp;
            this.MouseWheel += ObjViewerForm_MouseWheel;
        }

        private Vector3 ComputeFaceNormal(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            var normal = Vector3.Cross(v2 - v1, v3 - v1);
            return Vector3.Normalize(normal);
        }

        public void ObjViewerForm_MouseWheel(object sender, MouseEventArgs e)
        {
            _cameraZoom += e.Delta * CameraZoomSpeed;
            _needsRendering = true;
        }

        public class BoundingBox
        {
            public Vector3 Min { get; set; }
            public Vector3 Max { get; set; }

            public BoundingBox()
            {
                Min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
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
            }

        }

        

        private void ObjViewerForm_KeyDown(object sender, KeyEventArgs e)
        {
            // Update camera movement based on key presses
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.A:
                    MoveCamera(-_cameraMoveSpeed, 0f);
                    break;
                case Keys.Right:
                case Keys.D:
                    MoveCamera(_cameraMoveSpeed, 0f);
                    break;
                case Keys.Up:
                case Keys.W:
                    // Reduce the camera's zoom level
                    MoveCamera(0f, -_cameraMoveSpeed);
          
                    break;
                case Keys.Down:
                case Keys.S:
                    // Reduce the camera's zoom level
                    MoveCamera(0f, _cameraMoveSpeed);
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

        private void MoveCamera(float offsetX, float offsetY)
        {
            Vector3 moveVector = Vector3.Transform(new Vector3(offsetX, offsetY, 0f), Matrix4x4.CreateRotationY(_cameraRotationX));
            _cameraPosition += moveVector;
            _needsRendering = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_scene != null && _scene.RootNode != null)
            {
                // Set up the projection matrix
                // Set up the projection matrix with zoom
                var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                    (float)Math.PI / 4, // 45 degrees field of view
                    (float)this.ClientSize.Width / this.ClientSize.Height,
                    0.1f,
                    1000.0f
                );

                projectionMatrix.M11 *= _cameraZoom;
                projectionMatrix.M22 *= _cameraZoom;

                // Calculate the view matrix based on camera position and rotations
                var viewMatrix = Matrix4x4.CreateTranslation(-_cameraPosition) *
                                 Matrix4x4.CreateRotationX(_cameraRotationY) *
                                 Matrix4x4.CreateRotationY(_cameraRotationX);

                // Clear the buffer
                e.Graphics.Clear(Color.Black);

                // Render each mesh in the scene
                var tasks = new List<Task>();
                var lockObject = new object(); // Lock object for synchronization

                foreach (var mesh in _scene.Meshes)
                {
                    foreach (var face in mesh.Faces)
                    {
                        var currentFace = face; // Create a local copy to avoid modified closure issue

                        tasks.Add(Task.Run(() =>
                        {
                            var vert1 = mesh.Vertices[currentFace.Indices[0]];
                            var vert2 = mesh.Vertices[currentFace.Indices[1]];
                            var vert3 = mesh.Vertices[currentFace.Indices[2]];

                            var vertex1 = Project(new Vector3(vert1.X, vert1.Y, vert1.Z), projectionMatrix, viewMatrix);
                            var vertex2 = Project(new Vector3(vert2.X, vert2.Y, vert2.Z), projectionMatrix, viewMatrix);
                            var vertex3 = Project(new Vector3(vert3.X, vert3.Y, vert3.Z), projectionMatrix, viewMatrix);

                            var rawVec1 = new Vector3(vert1.X, vert1.Y, vert1.Z);
                            var rawVec2 = new Vector3(vert2.X, vert2.Y, vert2.Z);
                            var rawVec3 = new Vector3(vert3.X, vert3.Y, vert3.Z);

                            var normal = ComputeFaceNormal(rawVec1, rawVec2, rawVec3);
                            var lightVector = Vector3.Normalize(LightPosition - rawVec1);
                            var dotProduct = Vector3.Dot(normal, lightVector);

                            // Calculate the shade of gray based on the dot product
                            var shade = (int)(dotProduct * 127) + 127;
                            var color = Color.FromArgb(shade, shade, shade);

                            lock (lockObject)
                            {
              

                                var fillBrush = new SolidBrush(color);

                                // Draw the face using the projected vertices and the gradient brush
                                var points = new PointF[] { new PointF(vertex1.X, vertex1.Y), new PointF(vertex2.X, vertex2.Y), new PointF(vertex3.X, vertex3.Y) };
                                e.Graphics.FillPolygon(fillBrush, points);

                                if (drawWireFrame)
                                {
                                    e.Graphics.DrawPolygon(Pens.Cyan, points);
                                }
                            }
                        }));
                    }
                }

                // Wait for all tasks to complete
                Task.WaitAll(tasks.ToArray());
            }
        }




        private Vector2 Project(Vector3 vertex, Matrix4x4 projectionMatrix, Matrix4x4 viewMatrix)
        {
            // Apply transformations to the vertex
            var transformedVertex = Vector3.Transform(vertex, viewMatrix);
            transformedVertex = Vector3.Transform(transformedVertex, projectionMatrix);

            // Normalize the vertex coordinates and map them to screen space
            float x = transformedVertex.X * this.ClientSize.Width / 2 + this.ClientSize.Width / 2;
            float y = -transformedVertex.Y * this.ClientSize.Height / 2 + this.ClientSize.Height / 2;

            return new Vector2(x, y);
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
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if(openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Application.Run(new ObjViewerForm(openFileDialog.FileName));
            }
            else
            {
                Application.Run(new ObjViewerForm("./teapot.obj"));
            }

         
        }
    }
}
