using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Serialization;

namespace Somewhere
{
    /// <summary>
    /// A representation of Log row in database table
    /// </summary>
    public class LogRow
    {
        public DateTime DateTime { get; set; }
        public string Event { get; set; }
        public LogEvent LogEvent
            => new Deserializer().Deserialize<LogEvent>(Event);
    }

    public class LogEvent
    {
        public string Command { get; set; }
        public string[] Arguments { get; set; }
        public string Result { get; set; }
    }
}
