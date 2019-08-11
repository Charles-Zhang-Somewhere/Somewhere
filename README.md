```
If no arguments is given, enter interactive session.

Tags: show all tags (along with ID and total and tag usage count) (in a plain ordered single line comma delimited list

Files + count + recent: show all files with: ID, add date, name, tags, remark (if any) in command line, per count number of lines each time (Add Date, Revision Date, Revision Count are all derived, not in meta)

File (Read) - Name: Shows all detail about a particular file (inckluding virtual file)

Tag: replace quote characters (which can still be entered) with underscore at handler, skip empty entries

Large scale performance test: generate 50000 files and add each to home and generate 1000 random tags and apply 5-10 to each file, then inspect using command line and sqlite browser manually.

Search ("find") + type + keywords (quoted): allow date entry, allow tag, allow file name, allow revision count

FAQ: How to handle name collision: really should give a descriptive name, much like you would to webpage bookmarks (show a screenshot), SW will automatically clamp file name size to 64 characters (with extension, handled transparently) and keep full name only in database. For updating tags one can also use ID directly (physical file with name with same numerical value first, then we check ID, for tag and untag command)

Best Practice: Try to be specific and consistent with naming, give meaningful and simple to remember tag names, avoid plurals

Delete tag (and all files): require confirmation  (show how many are affected). Show report list after action.

Clean: clean all deleted files. Require confirmation. Show report list after action.

Database schemas: the idea of adding tags to physics file resources is quite simple. In fact cefore creating this application I am already doing it at work manually for documents, reading materials, test data, and misc resource files - that's much slower,  but the advantage of having a database for this purpose is very superior, by avoiding the limits of underlying OS FS, I gain the ability to annotate files (notably name length limit, hierarchical structure, and no meta data available) like never before and this enables much easier later retrieval.

FAQ: What will happen if I put an emoji in my filename?

Update: Change "note" meta to "remark" to disignify it.

Feature Requests (Usability): Add whole file version revision, either simply natively, or using git. Per file based, require message.
```

> (let) Organization ends here. Already in final compact form. (The next step would be to enable embedding as an option, and support compression)
> this application has got an opinion.

# Overview

**Features**

1. Free, open source, dedicated, cross-platform, SQLite based, almost 0 dependency (except SQLite and YAML and .Net Runtime)
2. Item based, first order/primary citizen (how to call it?) tags, (custom) theory backed 
3. Non-intrusive, FS based, designed for custom files and decent interoperability with existing hierarchical structure
4. Heavily documented including methodology
5. Absolutely self contained
6. Cutting Edge technology, C#.Net Core based, last for another 100 years.
7. Dark mode, B/W color scheme

**Who should use it**

This app is absolutely intended for personal use. Sharing files on network drive is OK but it's not intended for multi-user simultaneously editing. The key use case is for heterogeneous file management - the more diverse the files, the more specific the file, the better this system can work. If you have tens of thousands of photos for very specific circumstances then you are OK or even better just stick with regular folders, but if you have a few thousand pictures that you refer to regularly and each contains a very different subject (much like my own Pinterest boards - which sucks because Pinterest's board based management approach is absolutely arcane - but well it's functional because "there is no better (I.e. tag based) solutions yet" (for me)), then this system will be much more efficient. Besides, you can mix the two as mentioned below.

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

## Terminology

1. TBFS: Tab based file system

## Concepts

1. Strictly item based - not for managing tags (just like bookmark ninja). Operations are focused around specific items.
2. Tag with efficiency: tagging is only so useful when we spend less time organizing stuff and more easily receive stuff, when it's much faster to assign (as mentioned above) multiple tags than deciding which single folder to put something - a proper command line interface is one step, but it's subject to typos - an efficient GUI (like Bookmark Ninja) is thus especially important .
3. Let me put it this way: there are at least N (N>3) ways to tag a file while there is only one way to put it under a folder - you can see the limit of traditional hierarchical based systems. This applies both in terms of organization and retrieval.
4. Virtual file/note/resource: in the command only notes are supported. Files that exist directly (and only) in database are called "virtual files", in terms of the contents it contains - if it contains text file, it's "virtual note", otherwise for binary it's terms "virtual resource". Filenames are required to be unique among all managed files, no matter whether it's virtual or not. (idea) "Virtual file as resource".. bookmark ninja as notes, OneNote....Evernote......
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
5. Simple embedded revision control (full file based, like google drive)
6. ASP.Net Core Server Web client for viewr and navigation purpose (or Single page app, allowing static hosting, JS based)
	* Front Page: search, (edit, import, export)
	* View Page (dedicated for items and item types)
	* REST API (rename etc...)

## Software Components

1. SQLite Handling Layer: .Net Core
2. 

## Underlying SQLite Table Schemes

In summary, those tables:

1. Tag: ID (Always incremental, unique), Name (Tags are first-class objects)
2. File (item): ID (Always incremental, unique), Name (Name and or path; Nott-nullable), Content (Blob), Meta (YAML: Size, MD5, Remark), EntryDate - for physical file, physical folder, virtual file (note and resource)
3. FileTag: FileID, TagID
4. Log: DateTime, Event (include relevant IDs and all details) - server mosltly reference purpose
3. Configuration: key, value
4. (Future) Rule (A,B,C)
5. (Future) Hierarchy (A/B/C)
6. (Future) Revision: FileID, RevisionID, Content, EntryDate

# Usage

Tricks: 

1. Win32 on filename and new button for file and folder win33 context menu

Cautious:

1. Filename must be either unique or null (for safe add and remove operations, and for compatibility with file system), though length limit is removed (for meaningful description for notes). And as such meaningful filename is encouraged (if not required). For practical (and safe) reasons, files are never actually physically deleted - they are instead marked with suffix `_deleted` at the very end of filename (and disable the extension).

## Best Practices

1. Tag name convention: per convention of other existing tag based systems, I found this a good starting point: 1) comma or space seperated thus no comma or space in name, 2) lower case, case insensitive 3)
	* Do note by design any characters are allowed in tags in the underlying databse, but command line and gui interface enforce no comma and no space policy
2. No folder unless and homogenous container (not enforced): .... folders can still be directly added as an "item"

# Commands

All commands are **case-insensitive**.

1. `add`: 



# Comprehension Command Reference

Below is generated directly from the application by using `sw doc` command.

