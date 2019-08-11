using System;
using System.Collections.Generic;
using System.Text;

namespace Somewhere
{
    /// <summary>
    /// A representation of FileTag row in database table;
    /// This is not usually used, but needed for some functions
    /// </summary>
    public class FileTagRow
    {
        public int FileID { get; set; }
        public string TagID { get; set; }
    }
}
