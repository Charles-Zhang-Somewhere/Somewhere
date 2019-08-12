using System;
using System.Collections.Generic;
using System.Text;

namespace Somewhere
{
    /// <summary>
    /// Represents meta info for a file
    /// </summary>
    public class FileMeta
    {
        public long Size { get; set; }
        public string MD5 { get; set; }
        public string Remark { get; set; }
    }
}
