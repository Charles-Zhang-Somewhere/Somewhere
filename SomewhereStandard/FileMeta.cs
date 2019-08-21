using System;
using System.Collections.Generic;
using System.Text;

namespace Somewhere
{
    /// <summary>
    /// Represents pre-defined meta info for an item
    /// </summary>
    public class SystemMeta
    {
        public long Size { get; set; }
        public string MD5 { get; set; }
        public string Remark { get; set; }
    }
}
