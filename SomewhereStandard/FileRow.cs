using System;
using System.Collections.Generic;
using System.Text;

namespace Somewhere
{
    /// <summary>
    /// A representation of File row in database table;
    /// This is not usually used, but needed for some functions
    /// </summary>
    public class FileRow
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public DateTime EntryDate { get; set; }
        public string Meta { get; set; }
    }
}
