using System;
using System.Collections.Generic;
using System.Text;

namespace Somewhere
{
    /// <summary>
    /// A representation of Configuration row in database table
    /// </summary>
    public class ConfigurationRow
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public string Comment { get; set; }
    }
}
