using System;
using System.Diagnostics;
using System.Linq;

namespace SW
{
    class Program
    {
        static void Main(string[] args)
        {
            // Just runs Somewhere.exe
            using (System.Diagnostics.Process pProcess = new System.Diagnostics.Process())
            {
                pProcess.StartInfo.FileName = @"Somewhere";  // Relative to assembly location, not working dir
                pProcess.StartInfo.Arguments = string.Join(' ', args.Select(a =>
                {
                    string escaped = a;
                    // Double escape quotes
                    if (a.Contains("\""))
                        escaped = a.Replace("\"", "\"\"");
                    // Always add quotes
                    escaped = $"\"{escaped}\"";
                    return escaped;
                })); // argument
                pProcess.StartInfo.UseShellExecute = false;
                pProcess.StartInfo.RedirectStandardOutput = true;
                pProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                pProcess.StartInfo.CreateNoWindow = true; // not diplay a windows
                pProcess.Start();
                string output = pProcess.StandardOutput.ReadToEnd(); //The output result
                pProcess.WaitForExit();
                Console.WriteLine(output);
            }
        }
    }
}
