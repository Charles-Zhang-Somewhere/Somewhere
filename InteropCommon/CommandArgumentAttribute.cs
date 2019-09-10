using System;
using System.Collections.Generic;
using System.Text;

namespace InteropCommon
{
    /// <summary>
    /// A custom attribute denoting an argument for the command
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public class CommandArgumentAttribute: Attribute
    {
        #region Constructor
        public CommandArgumentAttribute(string name, string explanation, bool optional = false)
        {
            Name = name;
            Explanation = explanation;
            Optional = optional;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Name of the argument
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// An explanation of usage for the argument
        /// </summary>
        public string Explanation { get;  }
        /// <summary>
        /// Indicates this argument is optional
        /// </summary>
        public bool Optional { get; }
        #endregion
    }
}
