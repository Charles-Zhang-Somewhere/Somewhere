using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;

namespace InteropCommon
{
    /// <summary>
    /// A representation of loosely typed yaml deserialization result:
    /// it's either a Dictionary (mapping), a List of KeyValuePairs (list) 
    /// or a single KeyValuePair/string (scalar)
    /// </summary>
    /// <remarks>
    /// Adopted from StackOverFlow, codeteq's answer to 
    /// "With C#, is querying YAML possible without defining lots of types?"
    /// (Links are not provided so this file doesn't look messy)
    /// </remarks>
    /// <example>
    /// var data = new YamlQuery(yamlObject)
    ///             .On("ressources")
    ///             .On("pods")
    ///             .Get("name")
    ///             .ToList<string>();
    /// </example>
    public class YamlQuery
    {
        #region Private Members
        /// <summary>
        /// Original deserialized object
        /// </summary>
        private object YamlDic;
        /// <summary>
        /// Current key
        /// </summary>
        private string Key;
        /// <summary>
        /// Current level
        /// </summary>
        private object Current;
        #endregion

        #region Constructor
        public YamlQuery(object yamlDic)
            => YamlDic = Current = yamlDic;
        public YamlQuery(string yaml)
        {
            using (var r = new StringReader(yaml))
                YamlDic = Current = new Deserializer().Deserialize(r);
        }
        #endregion

        #region Query Functions
        /// <summary>
        /// Drill-down one level of hierarchy by key of dictionary
        /// </summary>
        public YamlQuery On(string key)
        {
            Key = key;
            Current = Query(Current ?? YamlDic, Key, null);
            return this;
        }
        /// <summary>
        /// Get all property values at current level by name (e.g. a list of dictionary)
        /// </summary>
        public YamlQuery Get(string prop)
        {
            if (Current == null)
                throw new InvalidOperationException();

            Current = Query(Current, null, prop, Key);
            return this;
        }
        /// <summary>
        /// Convert a query into typed list
        /// </summary>
        public List<string> ToList()
        {
            if (Current == null)
                throw new InvalidOperationException();

            return (Current as List<object>).Cast<string>().ToList();
        }
        /// <summary>
        /// Convert a query into typed list
        /// </summary>
        public List<T> ToList<T>()
            => ToList().Select(v => (T)Convert.ChangeType(v, typeof(T))).ToList();
        /// <summary>
        /// Get properties at current level as list, this is a shorthand to using Get()
        /// </summary>
        public List<T> GetList<T>(string prop)
            => Get(prop).ToList().Select(v => (T)Convert.ChangeType(v, typeof(T))).ToList();
        /// <summary>
        /// Get a property at current level as scalar
        /// </summary>
        public T Get<T>(string prop, bool throwExceptionWhenNotFound = true)
        {
            if (Current == null)
                throw new InvalidOperationException();

            // Check if obj is a dictionary
            if (typeof(IDictionary<object, object>).IsAssignableFrom(Current.GetType()))
            {
                var dic = (IDictionary<object, object>)Current;
                var pairs = dic.Cast<KeyValuePair<object, object>>();

                // Enumerate and find first matching property
                foreach (KeyValuePair<object, object> pair in pairs)
                {
                    // Get key value
                    if (pair.Key as string == prop)
                        return (T)Convert.ChangeType(pair.Value, typeof(T));
                }
            }
            if (throwExceptionWhenNotFound)
                throw new ArgumentException("Specified property cannot be found");
            else
                return default(T);
        }
        /// <summary>
        /// Search in-depth for a pattern of key-property starting at current level
        /// </summary>
        public T Find<T>(string key, string prop, bool throwExceptionWhenNotFound = true)
        {
            IEnumerable<object> result = Query(Current, key, prop, key);
            if (result.Count() == 0 && throwExceptionWhenNotFound)
            {
                if (throwExceptionWhenNotFound)
                    throw new ArgumentException($"Pair {key}.{prop} cannot be found.");
                else
                    return default(T);
            }
            else
                return (T)Convert.ChangeType(result.First(), typeof(T));
        }
        /// <summary>
        /// Get properties at current level as dictionary
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            if (Current == null)
                throw new InvalidOperationException();

            // Check if obj is a dictionary
            if (typeof(IDictionary<object, object>).IsAssignableFrom(Current.GetType()))
            {
                var dic = (IDictionary<object, object>)Current;
                var pairs = dic.Cast<KeyValuePair<object, object>>();
                return pairs.ToDictionary(p => p.Key as string, p => p.Value as string);
            }
            else
                throw new InvalidOperationException();
        }
        #endregion

        #region Private Routine
        /// <summary>
        /// Query a list of property values at a given dicionary level
        /// </summary>
        /// <remarks>
        /// Looks like this recursive algorithm can look for a pattern defined by key.prop at any level 
        /// starting from root by drilling down
        /// </remarks>
        private static IEnumerable<object> Query(object dicObject, string key, string prop, string fromKey = null)
        {
            var result = new List<object>();
            // Null check
            if (dicObject == null)
                return null;
            // Check if obj is a dictionary
            if (typeof(IDictionary<object, object>).IsAssignableFrom(dicObject.GetType()))
            {
                var dic = (IDictionary<object, object>)dicObject;
                var pairs = dic.Cast<KeyValuePair<object, object>>();

                foreach (KeyValuePair<object, object> pair in pairs)
                {
                    // We are key at current level
                    if (pair.Key as string == key)
                    {
                        // Get key value
                        if (prop == null)
                            result.Add(pair.Value);
                        // Drill down one level
                        else
                            result.AddRange(Query(pair.Value, key, prop, pair.Key as string));
                    }
                    // We are sent from drill down from a previous level
                    else if (fromKey == key && pair.Key as string == prop)
                        result.Add(pair.Value);
                    // Keep going
                    else
                        result.AddRange(Query(pair.Value, key, prop, pair.Key as string));
                }
            }
            // Check if obj is a list of KeyValuePairs (a mapping) - i.e. a list of dictionaries
            else if (typeof(IEnumerable<object>).IsAssignableFrom(dicObject.GetType()))
            {
                var mapping = (IEnumerable<object>)dicObject;
                foreach (var dic in mapping)
                    result.AddRange(Query(dic, key, prop, key));
            }
            return result;
        }
        #endregion
    }
}
