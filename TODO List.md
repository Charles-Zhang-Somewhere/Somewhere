# Immediate To-Do List

This list is for myself.

```
Popup selection window, tag searching and clicking
Notebook page tags editing
Markdown preview for notes in Inventory panel

Move shared styles into App xaml - currently there is issue with buttons and layouts and behaviors
PopupSelectionWindow
FileSystemWaster for COmmands
Finish draft GUI
Read (file): by ID / filename, priority filename, automatically interpret integers as IDs (thus after a find or files command one can further inspect tags etc using read command
Files command rename to items
Add command during add if directory append trailing slash to differentiate it from regular files
Open: switch home directory
Archive: zip the home directory (and put the archive inside it)
Find action: copy (file to clipboard), copynames, copypaths

Add file watcher
`create` for knowledge when file without title (instead of creating a seperate **knowledge** table, it would be nice if we have all existing commands)
`add` wiht cut from foreign
Search ("find") + type + keywords (quoted): allow date entry, allow tag, allow file name, allow revision count
Delete tag (and all files): require confirmation  (show how many are affected). Show report list after action.
File (Read) - Name: Shows all detail about a particular file (including virtual file)
Clean ("purge"): clean all deleted files. Require confirmation. Show report list after action.
retag tags add/remove tags: filter select items then apply accordingly
update file newtags: remove all old tags from a file and apply a new set of tags
untag: When we do untag, remove empty tags
For all operations that involve physical files, add filename translation (clipping), clip to a length of (including path)...  Keep `_delete` and file extension
For Add *: check and add only if the file is not home database and is not already added.

Allow customized color definitions per repository in `ColorSettings` property


allow tagging external locations, like directly, using absolute path (this can be useful see in enterprise environment)
(Advanced Feature; Usability; Non-crucial) Find action: allow 'zip' directly all found items into an archive (what about virtual notes?) and automatically add and tag the final archive, all items within the archive is still tagged individually (with a path referencing into the archive file).

Currently GetPhysicalName cannot get names for those conflicted files  consider always use embedded IDStirng 

Finish organizing using Somewhere for one actual folder to see how well it works.

CLI Tab Positional Autoconplete: ConsoleX.ReadLine takes a function<int, string[], string[]> which gives current argument position, list of all arguments, and returns a set of autocomplete options, and an event function, which takes Func<int,  string[], string, string> for current position, current arguments, and selected match item (numbered 0-9, tab default select 0th), and returns the modified last(I.e. current) argument. It's a little bit complicated because our arguments are positional and not variable length and tags are comma delimited, likely under quotes 

Files: show a "total" at the end, like `tags`
(Implemented much later, put to road map) Advanced action on folder: flatten 
Zip Archive support for true homogeneous container.

Move Conceptual treatments to portfolio blog instead, and leave repository purely technical and code related; Leave usage notes there.
Add liscence;
```