# Immediate To-Do List

This list is for myself.

```
(Important)SW programming on imports/exports, or physical filename - test on references folder and P9
Power Mode tab compmletion
GetPhysicalName(string itemName) has very serious issue - as when called by Add(): What if the name indicates a file inside folder?
	We need to fix this "Add" command;
	See example trying to add reference to Wendy's photos in Project Nine repo

For note editor: when multiple lines are selected and first cursor is beginning of line, allow using `tab` to add tabs, and use `shift+tab` to remove tabs

Add renderer support: pbrt

Allow foreign reference (absolute path) implementation.
Desktop change window title so it's more identifiable on Windows task bar for specific repository (just keep the last folder name)

(Bug) If we are at Notebook tab tags field and press F2, tags edit will not be saved
(Bug) Currently for `mvt` command if the target tag is already one of the tags in the items that are tagged with source tag there will be an UNIQUE constraint failture during `mvt` process because we are trying to add the target tag again to the item. This will be fixed inside the logic for updating item tags.
(Bug) The right meta edit panel when maximized (i.e. hiding the left preview panel), when clicking on items, it can cause an error (probably due to preview panel collapsed)

Import/Export implementation
Filename Tests
Filename Commands check and implementation
Doc update and standardization
flatten: on current home and managed files only, flatten their path, add tags per path, rename appropriately, generate reports in a separate tagged file under "_report" and "_somewhere" (can be deleted by user) as note.

Urgent: Define standard for filter and add SD Markdown preview link handling formats

Full journalling mplementation in Core
Unit tests for jounaling opertaion - test simulated virual repository state
GUI integration - all GUI operations especially notes

Immediately work on Knowledge sybsystem, see new implementation ui note.
Write knowledge subsystem in commandline.

Work on define (very simply) filter syntax, implement for `find` and SD Notebook Tab search and MD hyperlink, refer to somewhere

im * flatten (for reference images) (non-home folder)
im HomeFolder clean (default copy) - entires and files (first copy/cut all contents, second make a one-by-one item transition from original home, third double check everything exists, finally generate a report as a **file**; No need to delete original home folder)
Move from SD by changing name (path) e.g. for files under a folder
For MD shortcut, allow ALT key to unformat brackets.

[Bugs]

1. (SD) Rename in item's name won't cause item name update in items list untill explicitly pressing F5 to refresh
	* Even if F5 refresh updated item's name in item's list, actual file name is not updated
	* This happens for image files, probably because the image file is currently being previewed and renaming in this case will raise an exception and silently ignored by SD

[Desktop]

* Status command textbox: should remember previous entries (in a stack) and allow rolling back using Up arrow keys.
* Video player add support for Note content that are HTTP address.
* Provide a shortcut from NT to IT just like "edit content" but this time "preview content" and select note in items and focus on Remark area. Also provide a label for Remark textbox.
* (SD IT - Inventory Tab) Add Convert action to Advanced and create dedicated window containing two regions and two combo box for preview before clicking Confirm button or close button on top right. Useful for (even incomplete is better) conversion from TW to MD.

[Core, Commands]

* `read`: allow reading notes
* `mat`: materialize a note
* `vr`: virtualize a text file

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
2. (Landing Page Features List) Preview for image formats:... Preview for video formats:.... Preview for audio formats:.... (Achieved with libVLC)

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
During purge operation, generate a structured CSV format report (just like File table), containing all details (including tags, just like `files` command), specifically, include full Meta contents (as a single string, so it's easier for people to parse, just like it is in the table).

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
6. Add importing internal folder without flattening it, i.e. import as-if;
	* Notice to import items from an external folder, rather than the folder itself, currently the correct way to do that is to `add` first which will cut it, then do `im` to import and flatten it
7. For remark, a special dictionary format is supported for single-line pair values which have special significance in Remark search (or filtering): `^(Key):(Value)$`
8. (Advanced) Allow interactive session directly inside Status Tab interacting with process and input outptu redirection, this can enable direct command line interface with external programs like Python (pending evaluation) or other utilities that we are going to develop - though do notice currently with CMD we can run the process seperately, there is no direct to read outptu from those programs

[(Urgent) Issues and Bugs - Affects Usability]

1. Add exception handling for `sw ui` when current directory is not a valid home folder
2. Same as above, currently if we just call `SomewhereDesktop` without going to a valid Home folder first, it will just crash
3. Notice list views used labels for underscore in file names are not displayed correctly, change to text blocks
4. Links in dialog window currently cannot be clicked, might be related to how the window is displayed.
5. Move shared styles into App xaml - currently there is issue with buttons and layouts and behaviors
6. Bug: add command cannot add files under a folder in current dir
7. Create note should not allow ending slashes to avoid confusion
8. Unit test: add case when name collision actually happens during add/create
9. Better find
10. Physical path support, absolute path support, import and export with path configuration, unit tests for importing and exporting
11. IT page Tags Textbox seems not support scrolling. Also we might want to make it wrapping stead of horizontal scrolling (but don't support return or newlines)
12. (Issue, SD): Suppressed commands not generating any info, show "interactive commands not supported in GUI" or make it capable of being noninteractive (and forced). - maybe use that IsGenerateConsoleOutput variable. 
13. Can only call DragMove when mouse button is down - when doing two finger gesture on UI. (Using a laptop?)
14. (Desktop) (Error) When an items tags are updated (added or updated), tags list (if had filter tags selected) don't return to full (refresh will return it to full). How is duplicate tags handled in SD textbox?
15. There is still one major issue with physicla filename: what if let's say a file after collision resolutoin gets a new name `test_#2.txt` yet the originally conflicting file `test.txt` was deleted, then when the program tries to find the file it looks for name `test.txt`? We'd better add a meta attribute "CurrentFilename" to the file to aid in this case.

[Potential Design Flaws]

1. Currently GetPhysicalName cannot get names for those conflicted files  consider always use embedded IDStirng 

[Usability and UI Improvement - Non Essential]

1. Allow customized color definitions per repository in `ColorSettings` property
2. Add browsing recent repositories (e.g. in a dialog window) - that information shall be saved along with executable folder's own database
3. Better style that not less blazingly white horizontal scroll bar (or better, don't show it and use instead wrappable text block for file names)
4. For binary contents, we can provide preview of first N bytes in Hex.
5. (Desktop) Further Note content editing augmentation: 1) Simple auto bracketing like Sublime for bold, italic, brackets etc. when texts are selected. 2) Ctrl+I Selection pop-up insertion of "find" filter for file names link to file ID; 3) Update Textbox redo steps to infinity

[Pending Documentation: Future Roadmap Features]

1. Advanced action on folder: `flatten` and `import flatten`
	* Allow **import flatten** with auto tagging of copied files and report of total file count (generate summary), the most basic way is to implement this by calling add command as underlying
2. (Major Feature) Annotation using radius circle directly on picture (defined completely by name as note): `(#ID) Image name::Note name[Parameters]` - and supported preview for clicking or (even better) hover showing (markdown supported). Application cases: localized Pinterest images - studied. Give reference images more value. 
    * Futher inspiration: develop this kind of meta-schemes for further utilizing existing systems, and focus on add meaning and convenience to things.

[Big Plans]

1. Zip Archive support for true homogeneous container.
	* Zip Archive support for true homogeneous container.
	* It's clearly important, so I say it twice....
2. allow tagging external locations, like directly, using absolute path (this can be useful see in enterprise environment)
3. (Advanced Feature; Usability; Non-crucial) Find action: allow 'zip' directly all found items into an archive (what about virtual notes?) and automatically add and tag the final archive, all items within the archive is still tagged individually (with a path referencing into the archive file).

[Sample Project Setup/Idea]

1. Wholeshare Era - An Explorable Novel
	* (Sounds, notes, visuals, items, notes...)....

[Code Organization]

1. Function `GetRealFilename()` can be improved slightly: instead of going back and iterate we can use whatever current folder name has already saved when parsing file name

[Utilities]

1. Utilities (command line, may develop as separate program called SU): explode (automatically break all tags into words by spaces and underscores , show preview before action.

[Packaging]

1. Automate packaging and even publishing process (i.e. publish to github). 	Packaging Instructions:
	* Package Linux .Net Core version and Windows version separately
	* Don't package Cmder but provide link
	* Package VLC separately for Windows version, or actually in the future this will become a required component when related features grow
```

# Unidentified

Somethings that used to be an issue and is no longer identified:

1. (Bug) Look like when editing Tags for active note then switching directly to IT, tags are not saved. Also F5 refresh probably should refresh active item/note properties for safety.
2. (Bug) There is a potential bug that during video preview when adding new videos it can cause a crash or take too long to respond especially on a networking environment - check how during such circumstances files are added and how it affect behavior of the program.
3. (Issue) IT region type filter still too large on laptop.
4. (Issue) There is an exception with DragMove() event on laptop when clicking on buttons and hold and move finger.
5. (Issue) Inventory panel tags need sorting, filters don't need sorting.
6. (Core, Command Behavior) `mv` command if old not exist and new already exist and new not exist in DB then we can automatically treat this as an explicit renaming and just update it. (No idea what that means)
7. (Issue) Exception Error dialog seems to have a height larger than screen resolution, adjust MaxHeight property.

# Don't Do

Don't do following things with a reason, and seek alternatives.

1. For recent working directories: add a task bar recent history implementation first.
	* Don't do this. This cause tigher integration with Windows, which is OK but less ideal.
	* Alternative: We implemented a popup dialog for history. This also has the benefit that history is managed by the application itself rather than Windows.

# Logic Review

1. When people remove tags in SD for active item what will happen if the tag is no longer referenced by any other files - will it be deleted automatically from database (as should happen) or hang there (if we haven't made a check to see whether anyone else still referenc the tag)