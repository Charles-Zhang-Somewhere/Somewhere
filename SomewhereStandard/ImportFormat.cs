using StringHelper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Somewhere
{
    /// <remarks>
    /// Case sensitive
    /// </remarks>
    public class Tiddler
    {
        #region Raw Properties
        public string title { get; set; }
        public string text { get; set; }
        public string tags { get; set; }
        public string created { get; set; }
        #endregion

        #region Helpers
        public DateTime CreatedDate
            => DateTime.Parse(Regex.Replace(created, @"(\d\d\d\d)(\d\d)(\d\d)(\d\d)(\d\d)(\d\d)(\d\d\d)", "$1-$2-$3 $4:$5:$6.$7"));
        public string[] Tags
            => Regex.Replace(
                // Replace double quote with underscore
                tags.Replace('"', '_'),
                // Replace square brackets escape with double quotes escape
                @"\[\[(.*?)\]\]", @"""$1""")
            // Parse as command line style
            .BreakCommandLineArgumentPositions();
        #endregion
    }
}
