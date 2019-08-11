using System;
using System.Collections.Generic;
using System.Text;

namespace Somewhere
{
    /// <summary>
    /// A representation of Tag row in database table;
    /// This is not usually used, but needed for some functions
    /// </summary>
    public class TagRow
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }
}
