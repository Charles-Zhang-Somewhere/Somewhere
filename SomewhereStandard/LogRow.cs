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
    /// <summary>
    /// Details of log
    /// </summary>
    public class LogEvent
    {
        public string Command { get; set; }
        public string[] Arguments { get; set; }
        public string Result { get; set; }
    }

    /// <summary>
    /// A representation of journal row in database table
    /// </summary>
    public class JournalRow: LogRow
    {
        public JournalType Type { get; set; }
        public JournalEvent JournalEvent
            => new Deserializer().Deserialize<JournalEvent>(Event);
    }
    /// <summary>
    /// Type of journal
    /// </summary>
    public enum JournalType
    {
        Log,
        Commit
    }
    /// <summary>
    /// Journal is a superset of log
    /// </summary>
    public class JournalEvent: LogEvent
    {
    }
}
