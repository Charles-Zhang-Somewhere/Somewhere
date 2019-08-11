﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteExtension
{
    public class BinaryData
    {
        /// <summary>
        /// File name without suffix
        /// </summary>
        public string Name { get; set; }
        public string Suffix { get; set; }
        public byte[] Data { get; set; }

        public BinaryData(string name, string suffix, byte[] dataStream)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Suffix = suffix ?? throw new ArgumentNullException(nameof(suffix));
            Data = dataStream ?? throw new ArgumentNullException(nameof(dataStream));
        }
    }

    /// <summary>
    /// SQLite Reader extention method class
    /// </summary>
    public static class SQLiteDataReaderExtension
    {
        #region Interface Method
        /// <summary>
        /// Execute a SQL query (e.g. select) with given command and SQLite parameters defined in an object
        /// </summary>
        public static SQLiteDataReader ExecuteQuery(this SQLiteConnection connection, string command, object parameters = null, bool containsBlob = false)
            => connection.BuildSQL(command, parameters, cmd =>
                // Optionally we can store result to in-memory table: new DataTable().Load(reader);
                cmd.ExecuteReader(containsBlob ? CommandBehavior.KeyInfo : CommandBehavior.Default));    // Key info is needed if we want to be able to read blob data without explictly require rowid column while using an alias row; However this will also return extra fields for primary keys; However if just we specify rowID and there is an alias the returned rowID will be named as alias (e.g. ID)
        /// <summary>
        /// Execute a SQL non-query (e.g. insert and update) with given command and SQLite parameters defined in an object
        /// </summary>
        public static void ExecuteSQLNonQuery(this SQLiteConnection connection, string command, object parameters = null)
            => connection.BuildSQL(command, parameters, cmd => { cmd.ExecuteNonQuery(); return null; });
        /// <summary>
        /// Execute a bunch of sql non-queries in a transaction (e.g. insert many many items)
        /// </summary>
        public static void ExecuteSQLNonQuery(this SQLiteConnection connection, string command, IEnumerable<object> commandParameters)
            => connection.RunSQL(commandParameters.Select(cp => new Tuple<string, object>(command, cp)));
        /// <summary>
        /// Execute a bunch of sql non-queries in a transaction (e.g. insert many many items)
        /// </summary>
        public static void ExecuteSQLNonQuery(this SQLiteConnection connection, IEnumerable<Tuple<string, object>> commandAndParameters)
            => connection.RunSQL(commandAndParameters);
        /// <summary>
        /// Execute a bunch of sql non-queries in a transaction (e.g. insert many many items)
        /// </summary>
        public static void ExecuteSQLNonQuery(this SQLiteConnection connection, IEnumerable<string> commandsWithoutParameters)
            => connection.RunSQL(commandsWithoutParameters.Select(cwp => new Tuple<string, object>(cwp, null)));
        #endregion

        #region Core Abstraction Logic
        /// <summary>
        /// Build a sql command with given dynamic paramters using database provider methods
        /// </summary>
        private static SQLiteDataReader BuildSQL(this SQLiteConnection connection, string command, object parameters,
            Func<SQLiteCommand, SQLiteDataReader> action)
        {
            // Build command
            using (SQLiteCommand cmd = new SQLiteCommand(command.TrimEnd(';'), connection))
            {
                // Handle parameters
                if (parameters != null)
                {
                    foreach (PropertyInfo property in parameters.GetType().GetProperties())
                        cmd.Parameters.AddWithValue(property.Name, property.GetValue(parameters));
                }

                // Execute command
                SQLiteDataReader returnValue = action(cmd);    // May or may not return anything depending on 
                // whether we are executing query or nonquery but just catch it

                return returnValue;
            }
        }
        /// <summary>
        /// Build and run a sequence of sql commands with given dynamic paramters using database provider methods in a transaction
        /// </summary>
        private static void RunSQL(this SQLiteConnection connection, IEnumerable<Tuple<string, object>> commandAndParameters)
        {
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                foreach (Tuple<string, object> item in commandAndParameters)
                {
                    string command = item.Item1.TrimEnd(';');
                    object parameters = item.Item2;
                    using (SQLiteCommand cmd = new SQLiteCommand(command, connection, transaction))
                    {
                        // Handle parameters
                        if (parameters != null)
                            foreach (PropertyInfo property in parameters.GetType().GetProperties())
                                cmd.Parameters.AddWithValue(property.Name, property.GetValue(parameters));
                        // Execute command
                        cmd.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }
        #endregion

        #region Data Extraction Helpers
        /// <summary>
        /// Retrieve a list of objects for target type using reflection from the data table;
        /// Type must have public properties; Property names are case insensitive;
        /// Closes and disposed the reader
        /// </summary>
        public static List<Type> Unwrap<Type>(this SQLiteDataReader reader) where Type : new()
        {
            if (!reader.HasRows) return new List<Type>();

            // Get column name mapping
            Dictionary<string, string> columnNameMapping = new Dictionary<string, string>();
            foreach (string column in Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i)))
                columnNameMapping.Add(column.Replace(" ", string.Empty), column);   // Case-sensitive; Do notice though it cannot contain spaces

            // Initialize objects from rows
            List<Type> returnValues = new List<Type>();
            while (reader.Read())
            {
                // Create a new instance of type
                Type instance = new Type();
                // Initialize properties of the instance
                foreach (PropertyInfo prop in typeof(Type).GetProperties())
                {
                    string propertyName = prop.Name;
                    if (columnNameMapping.ContainsKey(propertyName))
                        prop.SetValue(instance, reader[columnNameMapping[propertyName]] == DBNull.Value
                            ? null
                            : Convert.ChangeType(reader[columnNameMapping[propertyName]], prop.PropertyType));
                }
                // Add to return
                returnValues.Add(instance);
            }

            reader.Close();

            return returnValues;
        }
        /// <summary>
        /// Get a single value of given type from the single cell of the read result;
        /// Throw exception when invalid
        /// </summary>
        public static Type Single<Type>(this SQLiteDataReader reader, bool returnDefaultWhenEmptyForValueType = false) where Type : IConvertible
        {
            // If there is no row and type is not nullable then throw an exception otherwise return null
            bool canBeNull = !typeof(Type).IsValueType || (Nullable.GetUnderlyingType(typeof(Type)) != null);
            if (!reader.HasRows)
            {
                reader.Close(); // Close reader
                if (canBeNull || returnDefaultWhenEmptyForValueType)
                    return default(Type);
                else throw new ArgumentException("Reader contains no value.");
            }

            object value = null;
            while (reader.Read())   // Necessary to close the reading even if we have only one row
                value = reader[0];

            if (value == DBNull.Value && canBeNull)
                return default(Type);
            else
                return ConvertExtension.ChangeType<Type>(value);
        }
        /// <summary>
        /// Read in a binary blob; Per convention, must be formatted in "ID, FileName (Contain type) Content";
        /// A complete copy of the binary data is saved in the memory
        /// User responsible for disposing the memory stream.
        /// </summary>
        public static BinaryData SingleBlob(this SQLiteDataReader reader)
        {
            // If there is no row and then return null
            if (!reader.HasRows)
            {
                reader.Close(); // Close reader
                return null;
            }
            // Type check
            if (reader.FieldCount != 3
                || reader.GetOrdinal("ID") == -1
                || reader.GetOrdinal("FileName") == -1
                || reader.GetOrdinal("Content") == -1)
                throw new ArgumentException("Input data table must have ID, Name, Type and Content fields.");

            BinaryData data = null;
            while (reader.Read())   // Loop is necessary to close the reading even if we have only one row
            {
                SQLiteBlob blob = reader.GetBlob(reader.GetOrdinal("Content"), true);
                if (blob.GetCount() != 0)
                {
                    string filename = reader.GetString(reader.GetOrdinal("FileName"));
                    string type = Path.GetExtension(filename).Replace(".", "");
                    string name = Path.GetFileNameWithoutExtension(filename);

                    byte[] buffer = new byte[blob.GetCount()];
                    blob.Read(buffer, blob.GetCount(), 0);
                    data = new BinaryData(name, type, buffer);
                }
                blob.Close();
                blob.Dispose();
            }

            reader.Close();
            return data;
        }
        /// <summary>
        /// Extract a list of single type of values from a dr
        /// </summary>
        public static List<type> List<type>(this SQLiteDataReader reader) where type : IConvertible
        {
            // If there is no row then return null
            if (!reader.HasRows)
            {
                reader.Close(); // Close reader
                return null;
            }

            // Read all the rows and get the first element of each row
            List<type> list = new List<type>();
            while (reader.Read())   // Necessary to close the reading even if we have only one row
                // Add to return list
                list.Add(ConvertExtension.ChangeType<type>(reader[0]));

            return list;
        }
        #endregion
    }

    public static class ConvertExtension
    {
        // Add handling of nullable types to ChangeType
        public static T ChangeType<T>(object value)
        {
            // Get type
            Type t = typeof(T);

            // Determin nullability
            if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                // Return null for nullable
                if (value == null)
                    return default(T);

                t = Nullable.GetUnderlyingType(t);
            }

            return (T)Convert.ChangeType(value, t);
        }
    }
}