Implementation *change log* per **release**; Contents here should match that from [Release page](https://github.com/szinubuntu/Somewhere/releases) and serve as a text copy.

# V0.0.1 "CLI Functional Release"

## Summary

A functional version of CLI interface, with support for following commands. GUI hasn't been prepared yet.

* add - Add a file to home.
* create - Create a virtual file (virtual text note).
* doc - Generate documentation of Somewhere program.
* files - Show a list of all files.
* find - Find with (or without) action.
* help - Show available commands and general usage help. Use `help commandname` to see more.
* mv - Rename file.
* mvt - Move Tags, renames specified tag.
* new - Create a new Somewhere home at current home directory.
* rm - Remove a file from Home directory, deletes the file both physically and from database.
* rmt - Removes a tag.
* status - Displays the state of the Home directory and the staging area.
* tag - Tag a specified file.
* tags - Show all tags currently exist.
* ui - Run desktop version of Somewhere.
* untag - Untag a file.

## Major Changes

* \[Standard\] Basic commands implementations.

# V0.0.5 "Functional Desktop!"

## Summary

* \[Windows\] Basic Somewhere Desktop implementation.

## Detailed Change List

# V0.1.0-beta Somewhere "New Beta"🎉

Up-to-date executable..

# V0.1.0-gamma "Gamma"

Just a bit closer to actual release of `V0.1.0`.

This version is distinct from [V0.1.0-beta](https://github.com/szinubuntu/Somewhere/releases/tag/V0.1.0-beta) with notable changes to **update mechanism** and **data table format**. Some other in-progress implementations are also packed in this release, e.g. **journaling** (i.e. anything up to [commit 140](https://github.com/szinubuntu/Somewhere/commit/cea08ca857f317a9a0a2b8e58babbc464af0223c)).

All details of changes from V0.0.5 are expected to be summarized in the formal release of `V0.1.0`, which is expected real soon.

# V0.1.0 "The Real Thing"

* \[Major Feature\] Journaling system!
* \[Major Feature\] This release is not compatible with earlier releases! Upon executing this version, your home repository will be updated automatically and earlier versions is no longer usable.

The specific updates to the repository database are as follows:

1. Change "**Log**" table's name to "**Journal**";
2. Add a new attribute column "**Type**" to **Journal** table;
3. Set all existing entries' **Type** field's value to "**Log**";
4. Update repository version to `V0.1.0`.

## Summary

* \[Windows\] Add **Recent homes** history;
* \[Standard\]


