using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DupFind
{
    internal class FileHandler : IDisposable
    {
        /// <summary>
        /// List of files to compare ordered by file size.
        /// </summary>
        private SortedDictionary<long, List<string>> _FileSizes = new SortedDictionary<long, List<string>>();
        /// <summary>
        /// List of files to compare ordered by hash value.
        /// </summary>
        private SortedDictionary<string, List<string>> _DupFiles = new SortedDictionary<string, List<string>>();
        /// <summary>
        /// Lock for updating the dictionary.
        /// </summary>
        private object _Lock = new object();
        /// <summary>
        /// Argument reader for processing arguments
        /// </summary>
        private ArgReader _Args;
        /// <summary>
        /// Flag if we need to move the duplicate files
        /// this is updated/set when the move files directory is updated
        /// </summary>
        private bool _DoMoveFiles { get; set; }
        /// <summary>
        /// Directory to move files to
        /// </summary>
        private string _MoveFilesDirectory
        {
            get { return this._MoveFilesDirectoryName; }
            set
            {
                this._MoveFilesDirectoryName = value;
                this._DoMoveFiles = 
                    !string.IsNullOrEmpty(value) &&
                    Directory.Exists(value);
            }
        }
        /// <summary>
        /// Don't use this directory
        /// Directory to move files to
        /// </summary>
        private string _MoveFilesDirectoryName { get; set; }
        /// <summary>
        /// Flag for if files should be removed
        /// </summary>
        private bool _RemoveFiles { get; set; }
        /// <summary>
        /// Flag to enable quiet mode.
        /// Disable writing to the console.
        /// </summary>
        private bool _QuietMode { get; set; }
        /// <summary>
        /// Flag to enable recursing directories.
        /// </summary>
        private bool _OneDirectory { get; set; }
        /// <summary>
        /// Directory to move duplicate files too
        /// </summary>
        private string _DestinationDirectory { get; set; }
        /// <summary>
        /// Directory from which to look for duplicate files
        /// </summary>
        private string _SourceDirectory { get; set; }
        /// <summary>
        /// Directory to compare to the source directory
        /// </summary>
        private string _CompareDirectory
        {
            get { return this._CompareDirectoryName; }
            set
            {
                this._CompareDirectoryName = value;
                this._DoDirectoryCompare = 
                    !string.IsNullOrEmpty(value) &&
                    Directory.Exists(value);
            }
        }
        /// <summary>
        /// Flag tell us if we need to do the diectory compare
        ///   This is updated each time the compare directory (name) is updated
        /// </summary>
        private bool _DoDirectoryCompare { get; set; }
        /// <summary>
        /// Don't use directly
        /// Name of the directory to compare to
        /// </summary>
        private string _CompareDirectoryName { get; set; }
        /// <summary>
        /// File to write the duplicate file name too.
        /// </summary>
        private string _LogFileName { get; set; }
        /// <summary>
        /// Stream writer to use for the log file
        /// </summary>
        private StreamWriter _Writer { get; set; }
        /// <summary>
        /// Count of duplicate files found
        /// </summary>
        private int _DuplicateFiles = 0;
        /// <summary>
        /// Name of a file to look for a duplicate file of.
        /// </summary>
        private string _CompareFile { get; set; }
        /// <summary>
        /// Flag if comparing only one file
        /// We assume that if any values exists the system should try
        /// to compare so don't check for file's existance at this point
        /// all the code below to handle it.
        /// </summary>
        private bool _DoFileCompare { get { return !string.IsNullOrEmpty(this._CompareFile); } }
        /// <summary>
        /// Flag to write the log file if a log file name exists.
        /// </summary>
        private bool _WriteLog {  get { return !string.IsNullOrEmpty(this._LogFileName); } }

        /// <summary>
        /// Build the class and process the data
        /// </summary>
        /// <param name="args">List of arguments from the user</param>
        internal FileHandler(ArgReader args)
        {
            this._Args = args;

            this._CompareDirectory = _Args.GetValue("-c");
            this._DestinationDirectory = _Args.GetValue("-d");
            this._CompareFile = _Args.GetValue("-f");

            this._LogFileName = _Args.GetValue("-l");
            this._MoveFilesDirectory = _Args.GetValue("-m");
            this._OneDirectory = _Args["-o"];

            this._QuietMode = _Args["-q"];
            this._RemoveFiles = _Args["-r"];
            this._SourceDirectory = _Args.GetValue("-s");

            // Check for arguments
            if (_Args.IsEmpty)
            {
                throw new ArgumentException("You must supply at least one argument");
            }

            // Make sure we have a directory to search
            if (string.IsNullOrEmpty(this._SourceDirectory))
            {
                throw new ArgumentException($"Directory does not exist {this._SourceDirectory}", "-s");
            }

            // Create a log file to write to if log files are enabled.
            if (this._WriteLog)
            {
                this._Writer = File.CreateText(this._LogFileName);
            }

            // User wants to compare files so do that and return.
            if (this._DoFileCompare)
            {
                this.FileCompareHelper();
                return;
            }

            // Revert to default file compare
            this.FindDuplicateFiles();
        }

        private void FindDuplicateFiles()
        {
            // If we are comparing a directory then read that
            // directory first, this will force these files to remain
            // if duplicates are found.
            if(this._DoDirectoryCompare)
            {
                this.WriteMessage($"Reading {this._CompareDirectory}");
                this.FillFiles(this._CompareDirectory);
            }

            // Fill the file list, we will use this to 
            // filter from.
            this.WriteMessage($"Reading {this._SourceDirectory}");
            this.FillFiles(this._SourceDirectory);

            // Tell the user how many files we have to start with
            this.WriteMessage($"Collected {this._FileSizes.Count} files");

            // Get the groups of file where more than one of them is the same size
            var dupSizes = from p in this._FileSizes where p.Value.Count > 1 select p;

            // Tell the user how many same size files were found
            this.WriteMessage($"Same size files {dupSizes.Count()} files");

            // Get the hash values for the files that have the same size and
            // put them in the second list so we can count them.
            foreach (var g in dupSizes)
            {
                Parallel.ForEach(g.Value, file =>
                {
                    var hash = GetHash(file);
                    lock (_Lock)
                    {
                        if (this._DupFiles.ContainsKey(hash))
                        {
                            this._DupFiles[hash].Add(file);
                        }
                        else
                        {
                            this._DupFiles.Add(hash, new List<string>() { file });
                        }
                    }
                });
            }

            // Select the hash groups that have more than one file with the same hash
            // and tell the user how many there are.
            var dupFiles = from p in this._DupFiles where p.Value.Count > 1 select p;
            this.WriteMessage($"Duplicate groups {dupFiles.Count()}");

            // Process the files that are duplicated based ont the last list.
            foreach (var g in dupFiles.OrderBy(p => p.Key))
            {
                this.WriteLog($"---- {g.Key} ----");
                var first = true;
                foreach (var file in g.Value)
                {
                    this._DuplicateFiles++;
                    this.WriteLog(file + (this._DoDirectoryCompare && !file.StartsWith(this._CompareDirectory) ? "***" : ""));
                    if (this._DoDirectoryCompare && this._DoMoveFiles)
                    {
                        if(!file.StartsWith(this._CompareDirectory))
                        {
                            this.MoveFile(file);
                        }
                    }
                    else if (!first && this._DoMoveFiles)
                    {
                        this.MoveFile(file);
                    }
                    first = false;
                }
            }

            // Tell the user how many duplicate files would found.
            this.WriteMessage($"Duplicate files found {this._DuplicateFiles}");
        }

        /// <summary>
        /// Compare files to a given directory
        /// </summary>
        private void FileCompareHelper()
        {
            if (!File.Exists(this._CompareFile)) throw new Exception("File to compare does not exist");
            if (!Directory.Exists(this._SourceDirectory)) throw new Exception("Directory to compare to, does not exist");
            var info = new FileInfo(this._CompareFile);
            var hash = GetHash(this._CompareFile);
            this.WriteMessage($"Comparing file {this._CompareFile} size {info.Length}");
            this.SearchFiles(this._SourceDirectory, info.Length, hash);
        }

        /// <summary>
        /// Search for all files with the same size and has as the requested file info
        /// </summary>
        /// <param name="path">Path to search</param>
        /// <param name="length">Length of file</param>
        /// <param name="hash">HASH for source file</param>
        private void SearchFiles(string path, long length, string hash)
        {
            try
            {
                // only recurse directories if the one flag is not set
                if (!this._OneDirectory)
                {
                    // enumerate all the directories in this directory
                    foreach (var dir in System.IO.Directory.EnumerateDirectories(path))
                    {
                        SearchFiles(dir, length, hash);
                    }
                }

                // enumerate all the files in this directory
                foreach (var file in System.IO.Directory.EnumerateFiles(path))
                {
                    var info = new System.IO.FileInfo(file);
                    if (info.Length == length)
                    {
                        if (hash.Equals(GetHash(file))) this.WriteLog(file);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteMessage(ex.Message);
            }
        }

        /// <summary>
        /// Get all the files in the path and load them into the file size dictionary
        /// </summary>
        /// <param name="path">Directory to start the search in</param>
        private void FillFiles(string path)
        {
            // only recurse directories if the one flag is not set
            if (!this._OneDirectory)
            {
                // enumerate all the directories in this directory
                foreach (var dir in System.IO.Directory.EnumerateDirectories(path))
                {
                    this.FillFiles(dir);
                }
            }

            // enumerate all the files in this directory
            foreach (var file in System.IO.Directory.EnumerateFiles(path))
            {
                var info = new System.IO.FileInfo(file);
                if (this._FileSizes.ContainsKey(info.Length))
                {
                    this._FileSizes[info.Length].Add(info.FullName);
                }
                else
                {
                    this._FileSizes.Add(info.Length, new List<string>() { info.FullName });
                }
            }
        }
        
        /// <summary>
        /// Move the given file from the current location to the
        /// move directory name
        /// </summary>
        /// <param name="file"></param>
        private void MoveFile(string file)
        {
            if (this._DoMoveFiles)
            {
                var info = new FileInfo(file);
                var destFile = Path.Combine(this._MoveFilesDirectory, info.Name);
                var i = 0;
                while (File.Exists(destFile))
                {
                    destFile = Path.Combine(this._MoveFilesDirectory, $"{info.Name.Replace(info.Extension, "")}_{++i}{info.Extension}");
                }
                File.Move(info.FullName, destFile);
            }
        }

        /// <summary>
        /// Get the has of a file use MD5
        /// </summary>
        /// <param name="filename">Name of the file to hash</param>
        /// <returns>hash string</returns>
        private static string GetHash(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace(" - ", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Write message to the log file if it is enabled
        /// </summary>
        /// <param name="msg">Message to write</param>
        private void WriteLog(string msg)
        {
            if(this._Writer != null)
            {
                this._Writer.WriteLine(msg);
            }
            else
            {
                this.WriteMessage(msg);
            }
        }

        /// <summary>
        /// Write a message to the screen if quiet mode is not turned on
        /// </summary>
        /// <param name="msg">Messaege to write to the screen</param>
        private void WriteMessage(string msg)
        {
            if (this._QuietMode) return;
            Console.WriteLine(msg);
        }

        /// <summary>
        /// Flush the log writer if it exists and dispose of it.
        /// </summary>
        public void Dispose()
        {
            if(this._Writer != null)
            {
                this._Writer.Flush();
                this._Writer.Close();
                this._Writer.Dispose();
                this._Writer = null;
            }
        }
    }
}
