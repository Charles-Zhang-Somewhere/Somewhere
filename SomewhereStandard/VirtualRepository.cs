using StringHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Somewhere
{
    /// <summary>
    /// An in-memory representation of repository for `dump` command;
    /// Utilizes data from `Journal` table;
    /// Virtual Repository simulates all operations in journal and creates
    /// a state that mimic effects to database from input commits at a given time
    /// </summary>
    /// <remarks>
    /// The implementation of this class is subject to change so it's internal
    /// </remarks>
    internal class VirtualRepository
    {
        #region Private Types
        /// <summary>
        /// A very generic and simple representation of all information we 
        /// care about for an item (per current implementation)
        /// </summary>
        private class Item
        {
            public Item(int rowID)
                => RowID = rowID;
            public int RowID { get; set; }
            public string Name { get; set; }
            public string Tags { get; set; }
            public string Content { get; set; }
            /// <summary>
            /// Used for providing extra communicative information to the user, not data content
            /// </summary>
            public string Remark { get; set; }
        }
        #endregion

        #region Private Properties
        private List<Item> Items { get; set; } = new List<Item>();
        #endregion

        #region Types
        /// <summary>
        /// The format for dumping state of virtual repository
        /// </summary>
        public enum DumpFormat
        {
            /// <summary>
            /// Dump into portable csv
            /// </summary>
            CSV,
            /// <summary>
            /// Dump table entries into log text file, includes row ids
            /// </summary>
            LOG,
            /// <summary>
            /// Dump into a plain HTML report
            /// </summary>
            HTML,
            /// <summary>
            /// Dump into a SQLite database
            /// </summary>
            SQLite
        }
        #endregion

        #region Methods
        /// <summary>
        /// Pass-through a sequence of commits and update states accordingly
        /// </summary>
        public void PassThrough(IEnumerable<JournalRow> commits)
        {
            // Initialize a state representing unique items
            Dictionary<string, Item> items = new Dictionary<string, Item>();
            // Simulate through commit history
            foreach (var commit in commits)
            {
                // Skip irrelevant entries
                if (commit.JournalType == JournalType.Log) continue;
                // Simulate state per operation type
                var commitEvent = commit.JournalEvent;
                switch (commitEvent.Operation)
                {
                    case JournalEvent.CommitOperation.CreateNote:
                        items.Add(commitEvent.Target, new Item(commit.RowID) { Name = commitEvent.Target });    // Assum only success operations are recorded as commit journal entry
                        break;
                    case JournalEvent.CommitOperation.AddFile:
                        items.Add(commitEvent.Target, new Item(commit.RowID) { Name = commitEvent.Target });    // Assum only success operations are recorded as commit journal entry
                        break;
                    case JournalEvent.CommitOperation.DeleteFile:
                        items.Remove(commitEvent.Target);   // Assume it exists
                        break;
                    case JournalEvent.CommitOperation.ChangeItemName:
                        items[commitEvent.Target].Name = commitEvent.UpdateValue;    // Assume it exists and there is no name conflict due to natural order of commit journal records
                        items.Add(commitEvent.UpdateValue, items[commitEvent.Target]);
                        items.Remove(commitEvent.Target);
                        break;
                    case JournalEvent.CommitOperation.ChangeItemTags:
                        items[commitEvent.Target].Tags = commitEvent.UpdateValue;    // Assume it exists
                        break;
                    case JournalEvent.CommitOperation.DeleteTag:
                        foreach (var item in items)
                        {
                            List<string> oldTags = item.Value.Tags.SplitTags().ToList();
                            if (oldTags.Contains(commitEvent.Target))
                                item.Value.Tags = oldTags.Except(new string[] { commitEvent.Target }).JoinTags();
                        }
                        break;
                    case JournalEvent.CommitOperation.RenameTag:
                        foreach (var item in items)
                        {
                            List<string> oldTags = item.Value.Tags.SplitTags().ToList();
                            if (oldTags.Contains(commitEvent.Target))
                                item.Value.Tags = oldTags
                                    .Except(new string[] { commitEvent.Target })
                                    .Union(new string[] { commitEvent.UpdateValue })
                                    .JoinTags();
                        }
                        break;
                    case JournalEvent.CommitOperation.ChangeItemContent:
                        if (commitEvent.ValueFormat == JournalEvent.UpdateValueFormat.Difference)
                            throw new ArgumentException("Difference based content commit journal is not supported yet.");
                        items[commitEvent.Target].Content = commitEvent.UpdateValue;    // Assume it exists
                        break;
                    default:
                        // Silently skip un-implemented simulation operations
                        continue;
                }
            }
            Items = items.Values.ToList();
        }
        /// <summary>
        /// Pass-through a sequence of commits and track one particular file;
        /// The output for this tracking will be a sequence of updates to that particular file
        /// </summary>
        /// <param name="targetFilename">Can be any particular name the file ever taken</param>
        /// <remarks>
        /// The complexity is that the name can occur anywhere any time and can be delted then recreated
        /// which is in essence different entities but it still make sense to show it
        /// </remarks>
        public void PassThrough(List<JournalRow> commits, string targetFilename)
        {
            // Extract all commit events
            var events = commits.Where(row => row.JournalType == JournalType.Commit)
                .Select(row => new Tuple<int, JournalEvent>(row.RowID, row.JournalEvent)).ToList();
            // Go through each commit once to collect name progression
            Dictionary<string, List<string>> nameProgression = new Dictionary<string, List<string>>();  // The development of name changes for any given path
            Dictionary<string, HashSet<List<string>>> nameOccurences = new Dictionary<string, HashSet<List<string>>>();   // The occurences of names in any paths
            void UpdateNameOccurence(string name, List<string> sourcePath)
            {
                if (nameOccurences.ContainsKey(name))
                    nameOccurences[name].Add(sourcePath);
                else
                    nameOccurences[name] = new HashSet<List<string>>() { sourcePath };
            }
            foreach (var commit in events)
            {
                var commitEvent = commit.Item2;
                switch (commitEvent.Operation)
                {
                    case JournalEvent.CommitOperation.CreateNote:
                    case JournalEvent.CommitOperation.AddFile:
                        if(!nameProgression.ContainsKey(commitEvent.Target))    // The progression might already contains the name if it was created before then renamed or deleted
                            nameProgression.Add(commitEvent.Target, new List<string>());
                        nameProgression[commitEvent.Target].Add(commitEvent.Target);
                        UpdateNameOccurence(commitEvent.Target, nameProgression[commitEvent.Target]);
                        break;
                    case JournalEvent.CommitOperation.ChangeItemName:
                        foreach (var namePath in nameOccurences[commitEvent.Target])
                        {
                            namePath.Add(commitEvent.UpdateValue);
                            UpdateNameOccurence(commitEvent.UpdateValue, namePath);
                        }
                        break;
                    default:
                        break;
                }
            }
            // Go through each commit to collect tag progression
            Dictionary<string, List<string>> tagProgression = new Dictionary<string, List<string>>();  // The development of tag changes for any given path
            Dictionary<string, HashSet<List<string>>> tagOccurences = new Dictionary<string, HashSet<List<string>>>();   // The occurences of tags in any paths
            Dictionary<string, HashSet<string>> tagUsers = new Dictionary<string, HashSet<string>>();   // Tags and the names it was used with
            void UpdateTagOccurence(string tag, List<string> sourcePath)
            {
                if (tagOccurences.ContainsKey(tag))
                    tagOccurences[tag].Add(sourcePath);
                else
                    tagOccurences[tag] = new HashSet<List<string>>() { sourcePath };
            }
            foreach (var commit in events)
            {
                var commitEvent = commit.Item2;
                switch (commitEvent.Operation)
                {
                    case JournalEvent.CommitOperation.ChangeItemTags:
                        var tags = commitEvent.UpdateValue.SplitTags();
                        // Tag creation
                        foreach (var tag in tags)
                        {
                            // Record tag
                            if (!tagProgression.ContainsKey(tag))   // Tag might already exist because it was created before
                            {
                                var path = new List<string>() { tag };
                                tagProgression.Add(tag, path);
                                UpdateTagOccurence(tag, path);
                            }
                            // Record item name to tag
                            if (!tagUsers.ContainsKey(tag))
                                tagUsers[tag] = new HashSet<string>();
                            tagUsers[tag].Add(commitEvent.Target);
                        }
                        break;
                    case JournalEvent.CommitOperation.RenameTag:
                        // Tag renaming
                        foreach (var tagPath in tagOccurences[commitEvent.Target])
                        {
                            tagPath.Add(commitEvent.UpdateValue);
                            UpdateTagOccurence(commitEvent.UpdateValue, tagPath);
                        }
                        break;
                    default:
                        break;
                }
            }
            // Null check
            if (!nameOccurences.ContainsKey(targetFilename))
                return;
            // Go through each commit to find relevant commits
            List<Tuple<int, JournalEvent>> relevantCommits = new List<Tuple<int, JournalEvent>>();
            HashSet<string> usedNames = new HashSet<string>(nameOccurences[targetFilename].SelectMany(path => path));
            bool IsTagUserEverInvolvedTargetFilename(string sourceTag)
                => tagOccurences[sourceTag]
                // Get all paths items for the source tag
                .SelectMany(path => path)
                // Get all users for the ever taken variety of the tag and check whether them contained the wanted target file
                .Select(tag => tagUsers[tag]
                    // Get the names that tag are created with
                    .SelectMany(name =>
                        // Get the sequences of all changes for that name
                        nameProgression[name])
                    .Contains(targetFilename))
                .Contains(true);
            foreach (var commit in events)
            {
                var commitEvent = commit.Item2;
                // If event target is one of used names, then it's relevant
                if (usedNames.Contains(commitEvent.Target))
                    relevantCommits.Add(commit);
                // If this operation is about tags, i.e. its target parameter is not name of item, then it can still be relevant
                else if (commitEvent.Operation == JournalEvent.CommitOperation.RenameTag
                    || commitEvent.Operation == JournalEvent.CommitOperation.DeleteTag)
                {
                    // Check whether the tag's tagged items involve the wanted item
                    var oldTag = commitEvent.Target;
                    var newTag = commitEvent.UpdateValue;   // Can be null if operation is DeleteTag
                    if ( // Old tag's user's name progression contains target file name
                        IsTagUserEverInvolvedTargetFilename(oldTag) || 
                        // New tag's user is involved
                        (newTag != null && IsTagUserEverInvolvedTargetFilename(newTag)))
                        relevantCommits.Add(commit);
                }
            }
            // Go through each commit again to simulate changes in normal order
            int stepSequence = 1;
            foreach (var commit in relevantCommits)
            {
                var commitEvent = commit.Item2;
                // Initialize new item by referring to an earlier version
                Item newItem = new Item(commit.Item1)
                {
                    Name = $"(Step: {stepSequence})" + Regex.Replace(Items.LastOrDefault()?.Name ?? string.Empty, "\\(Step: .*?\\)(.*?)", "$1"), // Give it a name to show stages of changes throughout lifetime
                    Tags = Items.LastOrDefault()?.Tags,
                    Content = Items.LastOrDefault()?.Content
                };
                // Handle this commit event
                switch (commitEvent.Operation)
                {
                    case JournalEvent.CommitOperation.CreateNote:
                        newItem.Name = $"(Step: {stepSequence})" + commitEvent.Target;  // Use note name
                        newItem.Tags = null; // Clear tags for new item
                        newItem.Content = null; // Clear content for new item
                        newItem.Remark = "New creation";  // Indicate the note has been created at this step
                        break;
                    case JournalEvent.CommitOperation.AddFile:
                        newItem.Name = $"(Step: {stepSequence})" + commitEvent.Target;  // Use file name
                        newItem.Tags = null; // Clear tags for new item
                        newItem.Content = null; // Clear content for new item
                        newItem.Remark = "New add";  // Indicate the file has been created at this step
                        break;
                    case JournalEvent.CommitOperation.DeleteFile:
                        newItem.Remark = "Deleted";    // Indicate the file has been deleted at this step
                        break;
                    case JournalEvent.CommitOperation.ChangeItemName:
                        newItem.Name = $"(Step: {stepSequence})" + commitEvent.UpdateValue;  // Use updated file name
                        newItem.Remark = "Name change";  // Indicate name change at this step
                        break;
                    case JournalEvent.CommitOperation.ChangeItemTags:
                        newItem.Tags = commitEvent.UpdateValue; // Use updated tags
                        newItem.Remark = "Tag change";  // Indicate tag change at this step
                        break;
                    case JournalEvent.CommitOperation.ChangeItemContent:
                        newItem.Content = commitEvent.UpdateValue;    // Use updated file content
                        newItem.Remark = "Content change";  // Indicate content change at this step
                        break;
                    case JournalEvent.CommitOperation.RenameTag:
                        newItem.Tags = newItem.Tags.SplitTags()
                            .Except(new string[] { commitEvent.Target })
                            .Union(new string[] { commitEvent.UpdateValue })
                            .JoinTags();
                        newItem.Remark = "Tag change";  // Indicate tag change at this step
                        break;
                    case JournalEvent.CommitOperation.DeleteTag:
                        newItem.Tags = newItem.Tags.SplitTags()
                            .Except(new string[] { commitEvent.Target })
                            .JoinTags();
                        newItem.Remark = "Tag change";  // Indicate tag change at this step
                        break;
                    default:
                        break;
                }
                // Record sequence of changes
                Items.Add(newItem);
                // Increment
                stepSequence++;
            }
        }
        /// <summary>
        /// Dump current state of virtual repository into specified format at given location
        /// </summary>
        public void Dump(DumpFormat format, string location)
        {
            string EscapeCSV(string field)
            {
                if (field != null && field.Contains(','))
                {
                    if (field.Contains('"'))
                        field.Replace("\"", "\"\"");
                    field = $"\"{field}\"";
                }
                return field ?? string.Empty;
            }

            switch (format)
            {
                case DumpFormat.CSV:
                    using (StreamWriter writer = new StreamWriter(location))
                    {
                        Csv.CsvWriter.Write(writer, 
                            new string[] { "Name", "Tags", "Content", "Remark" }, 
                            Items.Select(i => new string[] { EscapeCSV(i.Name), EscapeCSV(i.Tags), EscapeCSV(i.Content), EscapeCSV(i.Remark) }));
                        writer.Flush();
                    }
                    break;
                case DumpFormat.LOG:
                    using (StreamWriter writer = new StreamWriter(location))
                    {
                        writer.WriteLine("[Commits]");
                        writer.WriteLine($"ROWID\tName\tTags");
                        foreach (var item in Items)
                            writer.WriteLine($"{item.RowID}\t{item.Name}\t{item.Tags}");
                        writer.Flush();
                    }
                    break;
                case DumpFormat.HTML:
                    throw new NotImplementedException();
                case DumpFormat.SQLite:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentException($"Invalid format: {format}.");
            }
        }
        #endregion
    }
}
