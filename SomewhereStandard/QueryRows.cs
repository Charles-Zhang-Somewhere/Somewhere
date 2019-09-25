using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

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

        public class FileDetail
        {
            public int ID { get; set; }
            public DateTime EntryDate { get; set; }
            public string Name { get; set; }
            [YamlMember(ScalarStyle = ScalarStyle.Literal)]
            public string Content { get; set; }
            public string Tags { get; set; }
            /// <summary>
            /// Text of all meta attribtues
            /// </summary>
            [YamlMember(ScalarStyle = ScalarStyle.Literal)]
            public string Meta { get; set; }
            public DateTime? RevisionTime { get; set; }
            public int RevisionCount { get; set; }
        }
    }
}
