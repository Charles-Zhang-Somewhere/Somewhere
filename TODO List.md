# Immediate To-Do List

This list is for myself.

```
Work on define (very simply) filter syntax, implement for `find` and SD Notebook Tab search and MD hyperlink, refer to somewhere

im * flatten (for reference images) (non-home folder)
im HomeFolder clean (default copy) - entires and files (first copy/cut all contents, second make a one-by-one item transition from original home, third double check everything exists, finally generate a report as a **file**; No need to delete original home folder)
Move from SD by changing name (path) e.g. for files under a folder
For MD shortcut, allow ALT key to unformat brackets.

[Practical Tests and Usage]

1. university folder test; Finish organizing using Somewhere for one actual folder to see how well it works.
2. Finish draft GUI
3. Enable deleting files/notes from GUI

[Planned Essentials]

1. Finish implementing PopupSelectionWindow, implement using it for tag searching and clicking
2. CLI Tab Positional Autoconplete: ConsoleX.ReadLine takes a function<int, string[], string[]> which gives current argument position, list of all arguments, and returns a set of autocomplete options, and an event function, which takes Func<int,  string[], string, string> for current position, current arguments, and selected match item (numbered 0-9, tab default select 0th), and returns the modified last(I.e. current) argument. It's a little bit complicated because our arguments are positional and not variable length and tags are comma delimited, likely under quotes 
3. (New Command) Read (file): by ID / filename, priority filename, automatically interpret integers as IDs (thus after a find or files command one can further inspect tags etc using read command
4. (New Command) `Files` command rename to `items` for generity?
5. (New Command) Archive: zip the home directory (and put the archive inside it)
6. (New Command) Find action: copy (file to clipboard), copynames, copypaths
7. Conceptual and theoratical (attemptive) development to guide further implementation priority decisions - UI, CLI, commands, import/export, knowledge system?

[Marketing]

1. Make some meaningful and informative screenshots and update to README and website

[Misc]

Search ("find") + type + keywords (quoted): allow date entry, allow tag, allow file name, allow revision count
Delete tag (and all files): require confirmation  (show how many are affected). Show report list after action.
File (Read) - Name: Shows all detail about a particular file (including virtual file)
Clean ("purge"): clean all deleted files. Require confirmation. Show report list after action.
retag tags add/remove tags: filter select items then apply accordingly
update file newtags: remove all old tags from a file and apply a new set of tags
untag: When we do untag, remove empty tags
For all operations that involve physical files, add filename translation (clipping), clip to a length of (including path)...  Keep `_delete` and file extension
For Add *: check and add only if the file is not home database and is not already added.

Item remark (in inventory panel)

allow tagging external locations, like directly, using absolute path (this can be useful see in enterprise environment)
(Advanced Feature; Usability; Non-crucial) Find action: allow 'zip' directly all found items into an archive (what about virtual notes?) and automatically add and tag the final archive, all items within the archive is still tagged individually (with a path referencing into the archive file).

Currently GetPhysicalName cannot get names for those conflicted files  consider always use embedded IDStirng 

[Documentation]

1. Move Conceptual treatments to portfolio blog instead, and leave repository purely technical and code related; Leave usage notes there.
2. Add license;

[Features and Functionality - Versatility]

1. Further implementation of FileSystemWaster for Commands library; Add file watcher to SD
2. Basic integration of NtfsReader and NTFS search;
3. In-depth integration of NtfsReader and actions for NTFS search results;
4. Notice FileSystemWatcher might break dependency and cause incompatible for Linux, pending verification; Maybe .Net Core has better support for FileSystemWatcher. Focus on Windows for now.
5. Use rsync or some C# library for storing diff for revision

[(Urgent) Issues and Bugs - Affects Usability]

1. Add exception handling for `sw ui` when current directory is not a valid home folder
2. Same as above, currently if we just call `SomewhereDesktop` without going to a valid Home folder first, it will just crash
3. Notice list views used labels for underscore in file names are not displayed correctly, change to text blocks
4. Links in dialog window currently cannot be clicked, might be related to how the window is displayed.
5. Move shared styles into App xaml - currently there is issue with buttons and layouts and behaviors
6. Bug: add command cannot add files under a folder in current dir
7. Create note should not allow ending slashes to avoid confusion
8. Unit test: add case when name collision actually happens during add/create

[Potential Design Flaws]

1. Currently GetPhysicalName cannot get names for those conflicted files  consider always use embedded IDStirng 

[Usability and UI Improvement - Non Essential]

1. Allow customized color definitions per repository in `ColorSettings` property
2. Add browsing recent repositories (e.g. in a dialog window) - that information shall be saved along with executable folder's own database

[Pending Documentation: Future Roadmap Features]

1. Advanced action on folder: `flatten` and `import flatten`
	* Allow **import flatten** with auto tagging of copied files and report of total file count (generate summary), the most basic way is to implement this by calling add command as underlying

[Big Plans]

1. Zip Archive support for true homogeneous container.
	* Zip Archive support for true homogeneous container.
	* It's clearly important, so I say it twice....
2. allow tagging external locations, like directly, using absolute path (this can be useful see in enterprise environment)
3. (Advanced Feature; Usability; Non-crucial) Find action: allow 'zip' directly all found items into an archive (what about virtual notes?) and automatically add and tag the final archive, all items within the archive is still tagged individually (with a path referencing into the archive file).

[Sample Project Setup/Idea]

1. Wholeshare Era - An Explorable Novel
	* (Sounds, notes, visuals, items, notes...)....
```