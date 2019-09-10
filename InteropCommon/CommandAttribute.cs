using System;
using System.Collections.Generic;
using System.Text;

namespace InteropCommon
{
    /// <summary>
    /// A custom attribute indicating a function is a command for the program;
    /// All Command methods must accept `params string[] args` as argument and return an `IEnumerable&lt;string&gt;` (can be null)
    /// with each item representing each line of result (this is decided, don't alter - returning a `string` is not more convinient);
    /// The description should be in a single line
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class CommandAttribute: Attribute
    {
        #region Constructor
        public CommandAttribute(string description, string documentation = null, bool logged = true, string category = "Default")
        {
            Description = description;
            Documentation = documentation;
            Logged = logged;
            Category = category;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Command usage description
        /// </summary>
        public string Description { get; }
        /// <summary>
        /// Detailed command documentation
        /// </summary>
        public string Documentation { get; }
        /// <summary>
        /// Whether the execution of this command should be logged;
        /// If true the date time, command argument details, and command results are logged
        /// </summary>
        public bool Logged { get; }
        /// <summary>
        /// Category of the command; Used for organization purpose
        /// </summary>
        public string Category { get; }
        #endregion
    }
}
