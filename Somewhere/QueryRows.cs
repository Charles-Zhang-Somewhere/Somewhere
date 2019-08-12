using System;
using System.Collections.Generic;
using System.Text;

namespace Somewhere
{
    /// <summary>
    /// A collection of types for temporary query results for unwrapping operation
    /// </summary>
    public class QueryRows
    {
        public class TagCount
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public int Count { get; set; }
        }
    }
}
