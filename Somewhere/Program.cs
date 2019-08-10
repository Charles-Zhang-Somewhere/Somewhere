using System;
using System.IO;

namespace Somewhere
{
    class Program
    {
        static void Main(string[] args)
        {
            // Print working directory for identification
            Console.WriteLine($"Working Directory: {Directory.GetCurrentDirectory()}");

            // Summon desktop UI if no argument is given
            if (args.Length == 0)
            {
                Console.WriteLine("Run desktop.");
                RunDesktop();
            }

            // Exit
            return;
        }

        private static void RunDesktop()
        {
            using (System.Diagnostics.Process pProcess = new System.Diagnostics.Process())
            {
                pProcess.StartInfo.FileName = @"SomewhereDesktop";  // Relative to assembly location, not working dir
                pProcess.StartInfo.Arguments = ""; //argument
                // pProcess.StartInfo.UseShellExecute = false;
                // pProcess.StartInfo.RedirectStandardOutput = true;
                // pProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                // pProcess.StartInfo.CreateNoWindow = true; // not diplay a windows
                pProcess.Start();
                // string output = pProcess.StandardOutput.ReadToEnd(); //The output result
                // pProcess.WaitForExit();
            }
        }
    }
}
