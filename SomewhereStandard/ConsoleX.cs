using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Somewhere
{
    /// <summary>
    /// Specialized console writing extention
    /// </summary>
    public static class ConsoleX
    {
    }

    /// <summary>
    /// Capture console output to string and at the same time allow regular writing to Console;
    /// This class gives Console write two destinations at once, one being the main console window and the other being an internal store.
    /// Instantiate this class will immediately become effective and alter console behavior.
    /// </summary>
    public class OutputCapture : TextWriter, IDisposable
    {
        #region Internal Properties
        private TextWriter stdOutWriter;
        public TextWriter Captured { get; private set; }
        public override Encoding Encoding { get { return Encoding.UTF8; } }
        #endregion

        #region Constructor
        public OutputCapture()
        {
            this.stdOutWriter = Console.Out;
            Console.SetOut(this);
            Captured = new StringWriter();
        }
        #endregion

        #region Text Writer Interface (Optional Usage)
        override public void Write(string output)
        {
            // Capture the output and also send it to StdOut
            Captured.Write(output);
            stdOutWriter.Write(output);
        }
        override public void WriteLine(string output)
        {
            // Capture the output and also send it to StdOut
            Captured.WriteLine(output);
            stdOutWriter.WriteLine(output);
        }
        #endregion
    }
}
