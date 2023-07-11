using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ObjRenderer
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var objects = new List<ObjObject>();
            objects.Add(new ObjObject("./teapot.obj", new Assimp.Vector3D(0,0,0)));

            Application.Run(new ObjViewerControl(objects));
        }
    }
}
