using System;
using System.Collections.Generic;
using System.Text;

namespace Somewhere
{
    /// <summary>
    /// A custom attribute indicating a function is a command for the program;
    /// All Command methods must accept `string[] args` as argument and return an `IEnumerable&lt;string&gt;` (can be null)
    /// with each item representing each line of result (this is decided, don't alter - returning a `string` is not more convinient);
    /// The description should be in a single line
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class CommandAttribute: Attribute
    {
        #region Constructor
        public CommandAttribute(string description, string documentation = null)
        {
            Description = description;
            Documentation = documentation;
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
        #endregion
    }
}
