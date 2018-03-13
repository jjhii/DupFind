## Duplicate File Finder
### Use at your own risk.

This program is a test program and should be consider unstable at best and dangerous at least :)

# Syntacts

dotnet DupFind.dll [-r] [-q] [-sdir] [-cdir] [-ddir] [-lname] [-fname]

# Parameters

* -m
    * Move duplicate files to another directory.  This moves all files after the first file to the distination directory (-d)
* -r
    * Remove duplicate files.  This removes all duplicate files after the first matching file.
* -q
    * Quiet mode, messages are not written to the screen.
* -o
    * One directory, don't recurse subdirectories
* -s directory
    * Source directory
* -c directory
    * Compare directory.  Directory to compare to the source directory.  
    * If move or delete options are used, the system will not move or delete files from the compare directory.
* -d directory
    * Destination directory.  **not used**
* -f file name
    * Name of file to search for duplicates of in the source directory.
* -l file name
    * Log file name.

# Notes

When handling parameters, the system allow for joining, equal sign, or space.  The below three examples all yield the same results.

-mc:\dir  
-m=c:\dir  
-m c:\dir  
