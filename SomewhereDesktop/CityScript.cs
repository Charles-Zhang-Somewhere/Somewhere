using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SomewhereDesktop
{
    /// <summary>
    /// Processes a pre-processed Lua CityScript
    /// </summary>
    internal class CityScript
    {
        #region Constructor
        public CityScript(string script)
            => Script = script;
        private string Script { get; set; }
        #endregion

        #region Main Interface
        public enum OutputType
        {
            Default, // Use default
            CSV,
            ThreeJS,
            Plane   // Plane Vector/Pixel Mixed Layout Drawing
        }
        /// <summary>
        /// Generate output target file
        /// </summary>
        /// <param name="type">Provides default or overwrite format in code</param>
        /// <returns>Physical path to the file</returns>
        public string Output(string filePathWithoutExtension, OutputType type, out OutputType preferred)
        {
            // Parse
            Parse();
            // Output depending on desired format
            if (type == OutputType.Default && Settings.ContainsKey("rendertype"))
                preferred = (OutputType)Enum.Parse(typeof(OutputType), Settings["rendertype"]);
            else
                preferred = type;
            switch (preferred)
            {
                case OutputType.ThreeJS:
                    return OutputThreeJS(filePathWithoutExtension);
                case OutputType.Plane:
                    return OutputPlane(filePathWithoutExtension);
                case OutputType.CSV:
                case OutputType.Default:
                default:
                    return OutputCSV(filePathWithoutExtension);
            }
        }
        #endregion

        #region State Properties
        /// <summary>
        /// Options for settings
        /// </summary>
        public static readonly string[] Options = new string[] { "rendertype" };
        private Dictionary<string, string> Settings = new Dictionary<string, string>();
        private List<Placement> Placements = new List<Placement>();
        private struct Vector
        {
            public double X { get; }
            public double Y { get; }
            public double Z { get; }

            public Vector(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }
        private class Placement
        {
            public Vector Location { get; set; }
            public string[] Parameters { get; set; }

            public Placement(Vector location, string[] parameters)
            {
                Location = location;
                Parameters = parameters;
            }
        }
        #endregion

        #region Actions
        private void Place(double x, double y, double z, params string[] parameters)
            => Placements.Add(new Placement(new Vector(x, y, z), parameters));
        private void Set(string name, string value)
            => Settings[name] = value;
        #endregion

        #region Subroutines
        /// <summary>
        /// Parse the code and evaluate its effects, 
        /// altering behaviors by changing environment settings during evaluation
        /// </summary>
        private void Parse()
        {
            // Create script object
            Script script = new Script();
            script.Globals["place"] = (Action<double, double, double, string[]>)Place;  // Notice in Lua we use lower case
            script.Globals["set"] = (Action<string, string>)Set;

            // Evaluate
            script.DoString(Script);
        }
        private string OutputCSV(string filePathWithoutExtension)
        {
            string path = filePathWithoutExtension + ".csv";
            File.WriteAllLines(path, Placements.Select((p, i)
                => $"{i}, {p.Location.X} {p.Location.Y} {p.Location.Z}, {string.Join(" ", p.Parameters)}"));
            return path;
        }
        private string OutputPlane(string filePathWithoutExtension)
        {
            string Rect(string x, string y, string width, string height, string fill)
                => $"<rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" fill=\"{fill}\" />";
            string Circle(string cx, string cy, string radius, string fill)
                => $"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{radius}\" fill=\"{fill}\" />";
            string Text(string x, string y, string font_size, string anchor, string fill, string text)
                => $"<text x=\"{x}\" y=\"{y}\" font-size=\"{font_size}\" text-anchor=\"{anchor}\" fill=\"{fill}\">{text}</text>";

            string path = filePathWithoutExtension + ".html_part";
            using (StreamWriter writer = new StreamWriter(path))
            {
                int width = 300; int height = 300;
                writer.WriteLine($"<svg version=\"1.1\" baseProfile=\"full\" width=\"{width}\" height=\"{height}\" xmlns=\"http://www.w3.org/2000/svg\">");
                foreach (var placement in Placements)
                {
                    if (placement.Parameters.First() == "Cube")
                        writer.WriteLine(Rect($"{width / 2}", $"{height / 2}", placement.Parameters[1], placement.Parameters[2], "red"));
                }
                writer.WriteLine("</svg>");
                writer.Flush();
            }
            return path;
        }
        private string OutputThreeJS(string filePathWithoutExtension)
        {
            string path = filePathWithoutExtension + ".three";
            using (StreamWriter writer = new StreamWriter(path))
            {
                foreach (var placement in Placements)
                {
                    if (placement.Parameters.First() == "Cube")
                    {
                        writer.WriteLine(@"
// Setup scene, camera and renderer
var scene = new THREE.Scene();
var camera = new THREE.PerspectiveCamera(75, window.innerWidth / window.innerHeight, 0.1, 1000);

var renderer = new THREE.WebGLRenderer();
renderer.setSize(window.innerWidth, window.innerHeight);
document.body.appendChild(renderer.domElement);

// Set a rotating code
var geometry = new THREE.BoxGeometry(1, 1, 1);
var material = new THREE.MeshBasicMaterial( {color: 0x00ff00 } );
var cube = new THREE.Mesh(geometry, material);
scene.add(cube);

camera.position.z = 5;

// Render something
function animate() {
	requestAnimationFrame(animate);
	cube.rotation.x += 0.01;
	cube.rotation.y += 0.01;
	renderer.render(scene, camera);
}
animate();");
                    }
                }
                writer.Flush();
            }
            return path;
        }
        #endregion
    }
}
