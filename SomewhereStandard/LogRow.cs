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
        public int RowID { get; set; }
        public string Type { get; set; }
        public JournalType JournalType 
            => (JournalType)Enum.Parse(typeof(JournalType), Type);
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
    public class JournalEvent
    {
        #region Type
        /// <summary>
        /// Types of recognized commit operations
        /// </summary>
        public enum CommitOperation
        {
            CreateNote,
            AddFile,
            DeleteFile,
            ChangeItemName,
            ChangeItemTags,
            ChangeItemContent,
            RenameTag,
            DeleteTag
        }
        /// <summary>
        /// Format of the recorded updated value
        /// </summary>
        public enum UpdateValueFormat
        {
            /// <summary>
            /// The value is recorded in full
            /// </summary>
            Full,
            /// <summary>
            /// The value is recorded as a difference from a previous one
            /// </summary>
            Difference
        }
        #endregion

        #region Properties
        /// <summary>
        /// Type of commit operation
        /// </summary>
        public CommitOperation Operation { get; set; }
        /// <summary>
        /// Target of operation, i.e. name of item
        /// </summary>
        public string Target { get; set; }
        /// <summary>
        /// New value for the target, depending on operation, e.g. for change name, this is new name;
        /// Notice due to the nature of such records - i.e. they are atomic and single value operations, 
        /// for a composite action e.g. add a note with initial contents - it should be recorded as two 
        /// commits
        /// </summary>
        /// <remarks>
        /// For available operations, those are the updated values:
        /// - CreateNote: N/A
        /// - AddFile: N/A
        /// - DeleteFile: N/A
        /// - ChangeItemName: New name
        /// - ChangeItemTags: New updated tags (complete)
        /// - ChangeItemContent: New update content
        /// - DeleteTag: N/A
        /// - RenameTag: New name
        /// </remarks>
        public string UpdateValue { get; set; }
        /// <summary>
        /// Indicates the format of the updated value, only useful for ChangeContent operation;
        /// Currently only applicable to ChangeContent operation, and we are implementing only `Full` mode.
        /// </summary>
        public UpdateValueFormat ValueFormat { get; set; }
        #endregion
    }
}
