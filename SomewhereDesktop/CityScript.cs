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
            CSV
        }
        /// <summary>
        /// Generate output target file
        /// </summary>
        /// <param name="type">Provides default or overwrite format in code</param>
        /// <returns>Physical path to the file</returns>
        public string Output(string filePathWithoutExtension, OutputType type = OutputType.Default)
        {
            string OutputCSV()
            {
                string path = filePathWithoutExtension + ".csv";
                File.WriteAllLines(path, Placements.Select((p, i) 
                    => $"{i}, {p.Item1.X} {p.Item1.Y} {p.Item1.Z}, {string.Join(" ", p.Item2)}"));
                return path;
            }

            // Parse
            Parse();
            // Output depending on desired format
            if (type == OutputType.Default && Settings.ContainsKey("outputformat"))
                type = (OutputType)Enum.Parse(typeof(OutputType), Settings["outputformat"]);
            switch (type)
            {
                case OutputType.CSV:
                case OutputType.Default:
                default:
                    return OutputCSV();
            }
        }
        #endregion

        #region State Properties
        private Dictionary<string, string> Settings = new Dictionary<string, string>();
        private List<Tuple<Vector, string[]>> Placements = new List<Tuple<Vector, string[]>>();
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
        #endregion

        #region Actions
        private void Place(double x, double y, double z, params string[] parameters)
            => Placements.Add(new Tuple<Vector, string[]>(new Vector(x, y, z), parameters));
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
        #endregion
    }
}
