using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OlympusQuizES
{
    internal class SynchronizedFileEntry
    {
        string fileName = string.Empty;
        object fileLock = new object();
        ManualResetEvent startEvent = new ManualResetEvent(false);
        public uint count = 1;  // Use to delete entry when no longer needed
        
        public SynchronizedFileEntry(string fname)
        {
            fileName = fname;
        }

        string Filename { get { return fileName; } }
        public object FileLock { get { return fileLock; } }
        public uint Count { get { return count; } }
        public ManualResetEvent StartEvent { get { return startEvent; } }

    }

    internal class SynchronizedFiles
    {
        static Dictionary<string, SynchronizedFileEntry> files = new Dictionary<string, SynchronizedFileEntry>();
        static private object filesLock = new object();

        static public SynchronizedFile Create(string filename, bool synchronousStart = false)
        {
            if (OperatingSystem.IsLinux() == true)
                filename = filename.Replace('\\', '/');  // For Windows, it doesn't care
            else
                filename = filename.ToUpper();  // For Windows, it's case insensitive so allow any case

            SynchronizedFileEntry? entry = null;

            lock (filesLock)
            {
                if (files.TryGetValue(filename, out entry) == false)
                {
                    entry = new SynchronizedFileEntry(filename);
                    files.Add(filename, entry);
                }
                else
                    entry.count++;
            }

            SynchronizedFile fileSynchronizer = new SynchronizedFile(filename, entry.FileLock, entry.StartEvent); // Create new SynchronizedFile using same lock
            return fileSynchronizer;
        }

        static public void Delete(SynchronizedFile file)
        {
            Delete(file.Filename);
        }

        static public void Delete(string filename)
        {
            lock (filesLock)
            {
                if (OperatingSystem.IsLinux() == false)
                {
                    filename = filename.ToUpper();  // For Windows, it's case insensitive so allow any case
                }

                SynchronizedFileEntry entry;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                if (files.TryGetValue(filename, out entry) == true)
                {
                    entry.count--;
                    if (entry.count == 0)
                        files.Remove(filename);
                }
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            }
        }
    }
}
