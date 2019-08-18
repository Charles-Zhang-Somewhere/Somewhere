(This documentation is still work-in-progress)

# Overview

Welcome to **Somewhere**, a simple program that enables you to tag your files in a designated "**Home folder**". it's my best wish that this tool can come handy to you!

**Features**

1. Free, open source, dedicated (in terms of scope), cross-platform, SQLite based, almost 0 dependency (except *SQLite*, *YAML*, *.Net Core Runtime* and if Windows version is used, *WPF*, *NTFSReader*, *Markdig.WPF* and *WindowsAPICodePack*);
2. **Item** based, **first-class tags**, (custom, attemptive) **theory backed**;
3. Non-intrusive, File system friendly (existing FS based meta-layer), designed for custom files and decent interoperability with existing hierarchical structure;
4. Heavily documented including methodology;
5. Absolutely self contained;
6. Cutting Edge technology, C#.Net Core/Standard based, last for another 100 years, will migrate to .Net Core 3.0 when it comes out;
7. Dark mode, B/W color scheme, custom color scheme configuration (will be available in the future);
8. Long and expressive item names as you like - even something like this (notice each line is **real**):

```
This is /my file/ in Somewhere so I should be able to do <anything> I want with it \including "!!!!????**********"
This is /my file/ in Somewhere so I should be able to do <anything> I want with it \including "!!!!????**********"
This is /my file/ in Somewhere so I should be able to do <anything> I want with it \including "!!!!????**********"
This is /my file/ in Somewhere so I should be able to do <anything> I want with it \including "!!!!????**********"
This is /my file/ in Somewhere so I should be able to do <anything> I want with it \including "!!!!????**********"
This is /my file/ in Somewhere so I should be able to do <anything> I want with it \including "!!!!????**********"
This is /my file/ in Somewhere so I should be able to do <anything> I want with it \including "!!!!????**********"
This is /my file/ in Somewhere so I should be able to do <anything> I want with it \including "!!!!????**********"
This is /my file/ in Somewhere so I should be able to do <anything> I want with it \including "!!!!????**********"
This is /my file/ in Somewhere so I should be able to do <anything> I want with it \including "!!!!????**********".txt
```

However, names like that is *not really recommended* - you should name your file whatever way you wish but keep it **succinct and meaningful** so you can easily find it.

**Who should use it**

(I will change the tone below when I get a chance, doesn't sound very cool)

This app is absolutely intended for **personal use**; By design it's not tested for inter-network usage, e.g. between enterprise shared drives. Sharing files on network drive might be OK but it's definitely not intended for *multi-user simultaneously editing*. The key use case is for **heterogeneous file management** - (which is defined as) the more diverse the files, the more specific the file, the better this system can work. If you have tens of thousands of photos for very specific circumstances then you are OK or even better just stick with **regular folders**, but if you have a few thousand pictures that you refer to regularly and each contains a very different **subject** (much like my own Pinterest boards - which is deficient despite the variety because Pinterest's board based management approach is absolutely arcane - but well it's functional because "there is no better (I.e. tag based) solutions yet" (for me)), then this system will be much more efficient. Besides, you can mix the two as mentioned below.

1. Those who have been bothered by note and file organization
2. Those who enjoy tags in other platforms
3. Those who want to explore new ways to organize their documents 
4. Those who are really into *extreme organization* yet find a custom database too much for the purpose of daily uses
5. Developers should all use this
6. Serious everyone who have kept very diverse information on their computer and always had trouble retrieving and backup those data
7. This is NOT for those who never bothered organizing files or giving proper names to files - unless they are seeking a change
8. Those who do not trust cloud and specialize in local file management with sophisticated multi-disk backup plans

**How to Install It**

1. Download the corresponding version of zip file for your computer and unzip it;
2. Add application folder to environment PATH;
3. Run `somewhere` or `sw` to see help;
4. Have fun tagging!

# Treatments

> An effective software is not just a **tool**, it's also a **method**. A good software enables people to **think differently**.

## Definitions

1. (Not used) **Item**: Anything that contains information or data, this corresponds to a "File" entry in the File table, however in practice, and as in most operating system, "Files" can also denote "Folders", so "item" is used to avoid confusion.
2. Physical Folder: a folder is a file.... and item...
3. (Virtual) Note:
4. (File Meta) Remark: 

## Terminology

1. TBFS: Tab based file system

## Concepts

1. Strictly item based - not for managing tags (just like bookmark ninja). Operations are focused around specific items.
2. Tag with efficiency: tagging is only so useful when we spend less time organizing stuff and more easily receive stuff, when it's much faster to assign (as mentioned above) multiple tags than deciding which single folder to put something - a proper command line interface is one step, but it's subject to typos - an efficient GUI (like Bookmark Ninja) is thus especially important .
3. Let me put it this way: there are at least N (N>3) ways to tag a file while there is only one way to put it under a folder - you can see the limit of traditional hierarchical based systems. This applies both in terms of organization and retrieval.
4. Virtual file/note/resource: in the command only notes are supported. Files that exist directly (and only) in database are called "virtual files", in terms of the contents it contains - if it contains text file, it's "virtual note", otherwise for binary it's terms "virtual resource". Filenames are required to be unique among all managed files, no matter whether it's virtual or not. (idea) "Virtual file as resource".. bookmark ninja as notes, OneNote....Evernote...... (virtual note are those that content is not null, including resources and temperaty editing results in UI)
5. `Home.Somewhere`: The idea of using a dedicated home folder for managing all the files originated in MULTITUDE, and is partially inspired by Git which have the idea of a root repository folder - but in this case we are **flattenning files** by exploiting the capability of underlying disk FS.
6. **Flat Physical FS**: I used to have layers of layers of folder structures, making sure **each layer is semantically meaningful**, but then that introduces a lot of management burder, then I discovered that by flattening this structure, and sacrifce "meaning" at root folder, (screenshot of current home disk root folder for D drive), with folder at second level, it actually makes things much easier... with tags, it's even better...
7. (Best practice)**Best possible match filename**: semantics without limit of underlying FS file length
8. Central managed filename: inside database, a mapping...
9. Expect 10k-100k files: for personal data (as defined in [MULTITUDE](#)), for theoratical limit of NTFS FS system for single folder....efficiency...
6. Tracking/Managed files: By adding files to DB, just like adding files to repository like when using git, we *conscisouly manage each file* and decide which files participate in the system.
7. Intention: Wrapper around sqlite with dedicated interface for specialized oprations; I used to do this for my personal portfolio and while during work at OTPP, partly manually, and found that quite effective, though with proper tools the **scale** can grow.
8. Non-intrusive meta layer: easier maintainence, works well with other editors...
8. Personal data (see def in [MULTITUDE](#)), original data.... scheme should suffice.... Personal FS, not for network and collboration purpose... Data driven definitions...tool driven management...
9. Workflow design: .... download/archive, name, tag, retrieve...
10. Git friendly....Embeeded simple revision control....
11. Support for lightweight developer-oriented parts for easier Web and desktop GUI and direct C# API access of files.... (inspired by Fossil version control.....)
12. SQLite: Fast and efficient, see [proper use](https://www.sqlite.org/whentouse.html)
13. Multi-solution: dedicated single folder for each subject matter, e.g. how I used tiddly wiki... (screenshot?)
14. (deprecated)Master-slave copy: ...
15. (deprecated)Never touch physical files directly: to avoid erroenous file errors from programmer's fault..... something like master-slave copy and building a meta layer.... except virtual notes and text files we don't allow direct content modification from our side.... (lesson from MULTITUDE....)
16. Simple + focused....
17. No tag shall exist without an item? Or maybe we do want to pre-allocate tags
18. （Experimental; Feature） Flat notes/tags and knowledge sub-system: don't assume a context, e.g. "chapter 1" doesn't make sense - how to avoid redundancy? - Indexed knowledge point document Workflow, along with *annotated knowledge points* (must be succint/specific to differentiate from documents, or even wikipedia): extract knowledge at *bit level*.
	* Operators: up, down, filter (scope and scroll details), import (multi line list, assign tags)
	* Use `\`\`two tilts\`\`` with functional expression for math equations - but devising DSL is a dangerous thing because you risk never having an implementation, so just use LaTex instead.
19. Reflection on Hierarchical Structure: some might mistakenly think hierarchical structures are thus not useful or even bad - however hierarchical structures are indeed very powerful structures for organizing knowledge for trees are very efficient for searching purpose. However the biggest issue as we shall identify here, is trees are good only for relatively static data, e.g. English dictionary with thumb index - the reorganization cost is high - but that's exactly the case for personal data. Also it might occur that trees are better to model topological features (e.g. shapes of words or animals) rather than semantically features (which doesn't have solid forms).
20. Everything: unambiguous, folder and file names can be utilized, not intended for organizing, very specific; XYPlorer: not pure, temporary for work sessions is good, limited capacity due to mechanism (click and drag) - again, only suitable for sessional work not permanent organization.
21. Repository/Home: each Somewhere home is a repository, managed by SQLite database
1. Physical (File) Name: File must exist and must be managed by Somewhere. This name for max efficiency depends on its database presence. We require only a max of'#DDDDDDDD' (database item ID) size of characters (when it's not unique). Notice a file's ID is guaranteed to be unique throughout **Somewhere repository lifetime**.
2. Knowledge Link: knowledge item is linked by a either: item name (case sensitive), item ID (starting with # sign), tags list (start with t:), or a more general **search filter** (everything-lile, pending reference definition) (find: filter strings separated by space and quotes)
3. (Reflection note) Key motivation: we find it perfectly fine for the purpose of managing personal data using a SQLite database as a back store for all tagging information or even with actual data contents, but manually editing data tables are not very efficient (even with the help of SQLite DB Browser it can still be less than optimal), and manually dealing with tabular cells is not that keyboard efficient as well. Instead of devising a completely new scheme, nowadays I value more data operability and wish to create a tool that augments existings infrastructure instead of creating new things, thus a very well-defined customized SQLite database with pre-defined table formats are used to manage those files.
    * Later during the development, and partly inspired by some discussion with Author of Bookmark ninja, I realized this tool can also accomodating note taking purpose, with better integration with file linking (compared with and inspired by limitation of TiddlyWiki) - all we need is MD, and this can promote better data interopreability. In this case Tiddly Wiki becomes more of a plain indexed note tool. I do recognize this change of usage is mostly due to the limit of my phone's operating power, otherwise Tiddly wiki is very good for self-contained plain text notes.
4. Homogenous Container: as proposed in MULTITUDE (theory treatment pending summary on detailed proposed knowledge management framework differentiating between tags and folders and how to properly use them).

## References

1. (Pending) OTPP work reflection article (especially duirng writing documentation): see my portfolio article...
2. Nayuki article (link...): a confirmation of need and practical value, a survey of strategies
3. MULTITUDE article (link...): single home folder inspiration
4. XYPlorer: Virtual files and tags and multiple tabs and favorites, minitrees - too much! But it shows scale and framework. It inspired the idea of build TBFS on top of mata layer (e.g. XYPlorer uses text files to book keep various things).
5. Bookmark Ninja: Major inspiration with hands on experience with 8000+ bookmarks of webpages with over 1700 tags, very helpful ideas during communicating with the developer (name?).
6. (Anti) TagSpace: very slow, super-uneasy interface; But inspired name-based scheme, practiced for a while but the limitation is obvious
	* Screenshot of personal fodler for software installers with partial implementation of that scheme manually...
7. (Anti) Tabble: Java based, database based, super slow, non-self-contained, too much a scope, service based
8. Git: well everyone knows it and loves it
9. RoboOS: Flattened FS inspiration
10. (Anti)tagxfs: good [reading](http://tagxfs.sourceforge.net), some excerts of points..., too long a name, the scheme is not flexible, the underlying architecture/structure is not obvious, no **GUI support is not good** (even for efficient typers, GUI when utilized properly can provide much more information at a glance, so a proper GUI should be provided as a complement)
11. Everything (voidtools): inspired the name for Somewhere.

# Design Principal

Minimal UI, Functional, Data Driven, Text based.

1. Tags field are plain text box, for autocompletion pupup is used. No small tag button with even smaller cancel button is provided. So it's simple and less error prone, and plain enough to ensure data operability with CLI and GUI.
2. No fancy features, strictly necessary. Arguments are positional.
3. CLI driven, GUI is just a client.  

# Developement Notes and Roadmap

1. When focusing on NTFS and Windows, we can enable direct tracking of file movement of underlying FS, just like XYplorer did and keep track of things seamlessly everytime we hit `sync` or `update`; For linux I am not aware of a journal based FS so this will not work

## Next Steps

A functional and effective application may not just end here, I have several simple ammendities to make this software a bit more useful:

1. Auto-Discovery (Theory backed): 
	* Intelligent tag text files (enforced) by vocabulary list in **auto tag list (pink)** (seperated from user tag)
	* Inteligent tag image files (pink auto tags)
	* This is to support original MULTITUDE's inspiration plan and Airi interface (now in Somewhere)
2. Emoji text support
3. Embedded browser and wbesite bookmark
4. Meta-based dedicated note (called "remark") section
5. Feature Requests (Usability): Simple embedded revision control (full file based, like google drive)
	* Add whole file version revision, either simply natively, or using git. Per file based, require message.
6. ASP.Net Core Server Web client for viewr and navigation purpose (or Single page app, allowing static hosting, JS based)
	* Front Page: search, (edit, import, export)
	* View Page (dedicated for items and item types)
	* REST API (rename etc...)
7. **Add and Move with a Style** with embedded scripts (Cross Platform friendly, Lua?) in a loop inside a resource file, or at least basic regular expression (search and replace syntax) + tag (filter and name reference)  support.
8. Test support for linux and mac for CLI.
9. Enable embedding as an option (disabled, "Content" dedicated to text notes)
10. Add support for compression and archiving

## Software Components

1. SQLite Handling Layer: .Net Core
2. **Somewhere Application** (Library, Command Line): 
	* Commands (also as library functions): 
		- ✓: add, rm, mv (intelligent; if existing then physically move), mvt, rmt, delete (tag based, files), purge (clean up marked `_delete`), new (home), sync (discover physically renamed and update managed file names; if managed file is deleted, then issue warning); 
		- ☆: create, tag, untag
		- ⓘ: status, files, tags, read, log, find ("search", shorter), help
	* UI Interface: 
		- Left Pane (DockPanel: 
			+ Top: Name search, type (suffix) dropdown filter
			+ Main Area: List of (managed) files - just name + type suffix (round button icon)
		- Right Pane:
			+ Top: Filter tag flow list; tag search area, autosave check box (if checked all text preview editing will be saved when selected file is changed - consider deprecating this function, always use external editor, otherwise a copy will be saved for the text file in "content" field, which also makes it a "virtual note"), "Edit Tag" tag manager pop up button; All tags scroll panel flow list
			+ Middle: File preview and inforpanel
				* Left: Dedicated Preview area (for texts it also support editing), in the future this is also for embedded web browser
				* Right: Info and meta area: name, tag, meta edit; For name edit it will automatically rename underlying file using FS filename comliant format, this is done transparently)
			+ Bottom: Actions Buttons Panel
				* Basic: (Row 1) Import (Files, add as copy), Import as Folder (add folder, cut whole folder), Export, (Save), (Edit/Clear) | (Row 2) Physical (make virtual notes a physical file), Create, Open, New (Home)
				* Advanced Mode: Divide, Merge (in), "Status" Pop up (shows difference between mangaed and unmanaged physical files), Sync, Branch
		- Bottom Row: Stat & Help row
3. (Deprecated) GUI Desktop Application (WPF)
	* I think I can make it more efficient just with command line, maybe a website though (End remark: I would do it in CLI, especially since we are going to do auto-complete tag and tweak with some ReadKey() level code - but CLI's support for UNICODE display is arcane and code won't be clean if we do that, so WPF/ASP.Net is better option)
	* The only problem is with **auto-completion**, only if we can solve that - capture tabs in interactive mode?
4. (Pending) ASP.Net Core Viewer

Notice to compile and link SomewhereStandard and SQLite we need to uncheck **Prefer 32-bit** for SomewhereDesktop project.

## Design (CLI Focused, Remove GUI completely - "I hate clicking") (also promotes a more purer experience and enhanced memory capacity, also GUI is I herently a bad and easily abused feature/design by many software developers and is not inherently efficient, even for monitoring purpose? Maybe j should write an article on "when to use GUI - a software development choice", and definitely don't consider users, but purely from the perspective and efficiency of tools) And to be a bit more opinionated - windows has put a great deal of shit into its anti-human GUI and make people stupid (just check out Excels and Office suite with its strange nonintuitove and non standard keyboard shortcuts.) - A great console app (like vim) can be very great, and especially better if it combines some GUI elements (e.g. Unicode support and helpful hints).)

1. FileSystemWatcher: when running as dedicated console app, this monitors changes
2. NTFS reader: supports and replaces Everything - thank you free software
3. ConsoleX: tab completion with advanced customization (position and lambdas); Useful CLI information (later development after GUI is drafted)
4. (Windows only) Shell (Windows Explorer) integration for editing Item information (tags and notes)

## Underlying SQLite Table Schemes

Database schemas: the idea of adding tags to physics file resources is quite simple. In fact cefore creating this application I am already doing it at work manually for documents, reading materials, test data, and misc resource files - that's much slower,  but the advantage of having a database for this purpose is very superior, by avoiding the limits of underlying OS FS, I gain the ability to annotate files (notably name length limit, hierarchical structure, and no meta data available) like never before and this enables much easier later retrieval.

In summary, those tables:

1. **Tag**: ID (Always incremental, unique), Name (Tags are first-class objects)
2. **File (item)**: ID (Always incremental, unique), Name (Name and or path; Nott-nullable), Content (Blob), Meta (YAML: Size, MD5, Remark), EntryDate - for physical file, physical folder, virtual file (note and resource)
3. **FileTag**: FileID, TagID
4. **Log**: DateTime, Event (include relevant IDs and all details) - server mosltly reference purpose
3. **Configuration**: key, value
4. (Future) Rule (A,B,C)
5. (Future) Hierarchy (A/B/C)
6. (Future) Revision: FileID, RevisionID, Content, RevisionTime

# Usage

Download: Package with Cmder (for Unicode, colors and better overall experience)

Tricks: 

1. Win32 on filename and new button for file and folder win33 context menu

Cautious:

1. Filename must be either unique or null (for safe add and remove operations, and for compatibility with file system), though length limit is removed (for meaningful description for notes). And as such meaningful filename is encouraged (if not required). For practical (and safe) reasons, files are never actually physically deleted - they are instead marked with suffix `_deleted` at the very end of filename (and disable the extension).

## Shortcuts (Desktop Version)

1. F1: Help Popup
2. F2: OPen (Home)
3. F3: Reserved
4. F4: Advanced Actions (Divide, Merge (in), Diff (status popup), Sync, Branch)
5. F5: Tag Editor Popup (Addition + Rename + Remove) - Search select from list and enter in textbox
6. Double Click on file:
7. Right click on file:
8. Right click on home: 

## Best Practices

1. Tag name convention: per convention of other existing tag based systems, I found this a good starting point: 1) comma or space seperated thus no comma or space in name, 2) lower case, case insensitive 3)
	* Do note by design any characters are allowed in tags in the underlying databse, but command line and gui interface enforce no comma and no space policy
2. No folder unless and homogenous container (not enforced): .... folders can still be directly added as an "item"
3. Try to be specific and consistent with naming, give meaningful and simple to remember tag names, avoid plurals
4. Virtual Notes supports full-text (potentially indexed) content search. For regular files this is not supported (to avoid development need and overlapping with external existing tools e.g. grep etc.)
5. Advanced importing will be provided ,however it's recommended you do not use that if you want a cleaner repository - importing existing hierarchies will only mess up efficient knowledge organization and the better way is to keep the hierarchy, then gradually migrate new contents to Somewheres framework. When the time comes, you can just perform the `flatten` operation on selected folders to eliminate hierarchical structures.

## Case Studies

1. (General Tagging) Think about how you would like to (be able to) access it: "ECE, Formula" vs "ECE_Formula"
2. (Knowledge Tag System) Think about how you would like to ask questions - technically speaking, a proper **filename is also byitself a tag** (e.g. "a handbook of practical structures" -> "practical structures"), knowledge points should be **infitestimal small** and **self-contained** and at the same time contain **all other relevant knowledge**. So a proper knowledge point should have only: (plaintext) content, (plaintext) tags - wihtout any indirection. All other heavilifting should be done by the system, e.g. the system shall keep a "tag table" (a tag by itself) which contains all current tags, and this system should detect rather than interfere with normal file tag system (i.e. the main Somewhere application itself). And of course, *tags as either words or phrases, should be tagged on as well*. If everything can be treated as a tag, then tags can have two levels, according to its abstraction level - either up or down, and sentences, being the most specific ones, are on the bottomest level.

## Questions

1. Should I still use folders? Certainly. E.g. Musics put together in one folder (i.e. by **subject**), by **type** and **project**, or as mentioned above: use folders as **homogenous containers** - but flatted under parent/home folder.
2. I have *30+* Miku png images under *MyPicture/Anime/Miku*, what should I do? Eat it. Not, tag **the whole folder** directly, and **don't** tag individual files (unless some specific ones have special importance) - just let them go wild in there and keep the content growing. Or **zip it** and **tag the compressed file**.
3. I have 30+ software icon assets in *MyPictures/Graphical Assets/Icons*, what should I do? This case is slightly different because it may have need for **future reference**. You have two options: 1) As above; 2) There is something *seriously worong* with how you organize those images - *"Icons"*? Seriously? Does it mean anything? A *file/note/item* should never be categorized udner a **type description alone**: the categorization should always conform to its **content and meaning** instead. The same applies to specific **application domains** - and those simply cannot be appropriately addressed by hierarchical file systems. Use at least **two tags** (one is "icon", the other indicates its actual content - but in the case of simple application icons, you can probably avoid a secondary tag and instead use a **proper icon file name** (don't use "Icon 1.png", "Icon 2.png" etc.) instead. However **two-tag scheme** can work for things more sophisticated e.g. a digital painting) for **each of the files** in this folder, and throw that folder away. *Semantics matter*.
4. Why not as a VS code extension? JS?
5. Proper workflow: give a folder a beaituful name; never look at it.
6. What will happen if I put an emoji in my filename?
7. How to handle name collision: really should give a descriptive name, much like you would to webpage bookmarks (show a screenshot), SW will automatically clamp file name size to 64 characters (with extension, handled transparently) and keep full name only in database. For updating tags one can also use ID directly (physical file with name with same numerical value first, then we check ID, for tag and untag command)

# Comprehensive Commands Reference

Below is generated directly from the application by issuing `sw doc` command. All commands are **case-insensitive**.

```
Available Commands: 
	add - Add a file to home.
	create - Create a virtual file (virtual text note).
	doc - Generate documentation of Somewhere program.
	files - Show a list of all files.
	find - Find with (or without) action.
	help - Show available commands and general usage help. Use `help commandname` to see more.
	mv - Rename file.
	mvt - Move Tags, renames specified tag.
	new - Create a new Somewhere home at current home directory.
	rm - Remove a file from Home directory, deletes the file both physically and from database.
	rmt - Removes a tag.
	status - Displays the state of the Home directory and the staging area.
	tag - Tag a specified file.
	tags - Show all tags currently exist.
	ui - Run desktop version of Somewhere.
	untag - Untag a file.

add - Add a file to home.
	Options:
		filename - name of file; use * to add all in current directory
		tags(Optional) - tags for the file
create - Create a virtual file (virtual text note).
	Options:
		filename - name for the virtual file, must be unique among all managed files
		content - initial content for the virtual file
		tags - comma delimited list of tags in double quotes; any character except commas and double quotes are allowed.
doc - Generate documentation of Somewhere program.
	Options:
		path - path for the generated file.
files - Show a list of all files.
	Use command line arguments for more advanced display setup.
	Options:
		pageitemcount(Optional) - number of items to show each time
		datefilter(Optional) - a formatted string filtering items with a given entry date; valid formats: specific date string, recent (10 days)
find - Find with (or without) action.
	Find with filename, tags and extra information, and optionally perform an action with find results.
	Options:
		searchtype (either `name` or `tag`) - indicates search type; more will be added
		searchstring - for `name`, use part of file name to search; for `tag`, use comma delimited list of tags to search
		action (either `show` or `open`)(Optional) - optional action to perform on search results; default `show`; more will be added
help - Show available commands and general usage help. Use `help commandname` to see more.
	Options:
		commandname(Optional) - name of command
mv - Rename file.
	If the file doesn't exist on disk or in database then will issue a warning instead of doing anything.
	Options:
		filename - name of file
		newfilename - new name of file
mvt - Move Tags, renames specified tag.
	If source tag doesn't exist in database then will issue a warning instead of doing anything. If the target tag name already exist, then this action will merge the two tags.
	Options:
		sourcetag - old name for the tag
		targettag - new name for the tag
new - Create a new Somewhere home at current home directory.
rm - Remove a file from Home directory, deletes the file both physically and from database.
	If the file doesn't exist on disk or in database then will issue a warning instead of doing anything.
	Options:
		filename - name of file
		-f(Optional) - force physical deletion instead of mark as "_deleted"
rmt - Removes a tag.
	This command deletes the tag from the database, there is no going back.
	Options:
		tags - comma delimited list of tags in double quotes
status - Displays the state of the Home directory and the staging area.
	Shows which files have been staged, which haven't, and which files aren't being tracked by Somewhere. Notice only the files in current directory are checked, we don't go through children folders. We also don't check folders. The (reasons) last two points are made clear in design document.
tag - Tag a specified file.
	Tags are case-insensitive and will be stored in lower case; Though allowed, it's recommended tags don't contain spaces. Use underscore "_" to connect words. Spaces immediately before and after comma delimiters are trimmed. Commas are not allowed in tags, otherwise any character is allowed. If specified file doesn't exist on disk or in database then will issue a warning instead of doing anything.
	Options:
		filename - name of file
		tags - comma delimited list of tags in double quotes; any character except commas and double quotes are allowed; double quotes will be replaced by underscore if entered.
tags - Show all tags currently exist.
	The displayed result will be a plain alphanumerically ordered list of tag names, along with ID and tag usage count.
ui - Run desktop version of Somewhere.
untag - Untag a file.
	Options:
		filename - name of file
		tags - comma delimited list of tags in double quotes; any character except commas and double quotes are allowed; if the file doesn't have a specified tag then the tag is not effected
```

# Somewhere Desktop Shortcuts

The list of shortcuts are also available using `F12` button inside Somewhere Desktop app.

...

# Developer Doc

(Add detailed examples on interoperability)

To extend and define custom functions, open solution in VS, and create a new project by .....Add reference....

Example (see Examples solution folder for real and simplified examples):

1. An ASP.Net core website (see ASPNETCoreExample)