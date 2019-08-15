using System;
using System.Collections.Generic;
using System.Text;

namespace Somewhere
{
    /// <summary>
    /// A representation of joined row between File, Tag, FileTag tables
    /// </summary>
    public class FileTagMapping
    {
        public int FileID { get; set; }
        public string FileName { get; set; }
        public int TagID { get; set; }
        public string TagName { get; set; }
    }
}
