using System;
using System.Collections.Generic;
using System.Text;

namespace Somewhere
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
        public string Name { get; }
        public String Explanation { get;  }
        public bool Optional { get; }
        #endregion
    }
}
