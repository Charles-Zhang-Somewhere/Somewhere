using Somewhere;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using StringHelper;

namespace SM
{
    class Program
    {
        #region Program Entry
        static void Main(string[] args)
        {
            // Parse arguments
            if (args.Length == 0)
            {
                Console.WriteLine("Enter path to home folders/home repository as arguments.");
                return;
            }
            // Save arguments 
            Homes = args
                // Filter only valid paths
                .Where(arg => File.Exists(arg) || Directory.Exists(arg))
                .Where(path => File.Exists(path) 
                    // If it's a file, validate it's a valid DB
                    ? (Path.GetFileName(path) == Commands.DBName)
                    // If it's folder, make sure it contains a DB
                    : File.Exists(Path.Combine(path, Commands.DBName)))
                .Select(path => File.Exists(path)
                    // If it's a file, get folder path
                    ? Path.GetDirectoryName(path)
                    // If it's a folder, pass
                    : path)
                // Create Commands objects for each home folder
                .Select(home => new Commands(home))
                .ToArray();

            // Print welcome
            Console.WriteLine("Welcome to Somewhere Manager/Home Explorer, a dedicated tool for multi-home searching.");
            Console.WriteLine("Prepare to search in: ");
            foreach (var item in Homes)
                Console.WriteLine($"\t- {item.HomeDirectory}");
            Console.Write("Initializing...");
            // Initialize
            var worker = new BackgroundWorker()
            {
                WorkerReportsProgress = false,
                WorkerSupportsCancellation = false,
            };
            worker.DoWork += IndexHomes;
            worker.RunWorkerAsync();
            // Initializing animation
            char[] animation = new char[] { '\\', '-', '/', '-' };
            int location = Console.CursorLeft + 1;
            int counter = 0;
            while (IsLoading)
            {
                Console.CursorLeft = location;
                Console.Write(animation[counter % animation.Length]);
                counter++;
                Thread.Sleep(100);  // Poll requests at 100ms interval is good enough
            }
            // Report initialize results
            int totalItemCount = Homes.Sum(h => h.ItemCount);
            Console.WriteLine($"{Homes.Length} {(Homes.Length > 1 ? "homes" : "home")} loaded, " +
                $"with a total of {totalItemCount} {(totalItemCount > 1 ? "items": "item")}.");
            // Clean up initialization resources
            Console.CursorLeft = location;
            Console.WriteLine(' '); // Clear animation symbol
            worker.Dispose();
            // Interactive session
            while (true)
            {
                Console.WriteLine("Enter any find filter below:");
                Console.Write("> ");
                string filter = Console.ReadLine();
                if (filter.ToLower() == "exit")
                    break;
                else
                    // Perform searching and return results
                    Find(filter);
            }
        }
        #endregion

        #region Member Properties
        /// <summary>
        /// Indicates whether we are still initializing homes
        /// </summary>
        private static bool IsLoading = true;
        /// <summary>
        /// Path to home folders/repositories
        /// </summary>
        private static Commands[] Homes;
        #endregion

        #region Private Routines
        private static void IndexHomes(object sender, DoWorkEventArgs e)
        {
            Thread.Sleep(3000); // Emulate performing loading tasks
            // Trigger loading finish signal
            IsLoading = false;
        }
        /// <summary>
        /// Find and output results
        /// </summary>
        private static void Find(string filter)
        {
            // Very simple find implementation
            foreach (var home in Homes)
            {
                Console.WriteLine($"{home.HomeDirectory}:");
                home.Find(filter.BreakCommandLineArgumentPositions());
            }
        }
        #endregion
    }
}
