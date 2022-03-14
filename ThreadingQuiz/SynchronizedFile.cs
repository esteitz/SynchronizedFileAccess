using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OlympusQuizES
{
    internal class SynchronizedFile : ThreadSynchronizer
    {
        string fileName = String.Empty;

        public string Filename { get { return fileName; } }

        public SynchronizedFile(string fName, object aLock, ManualResetEvent? startEvent = null, int timeoutMS = Default30Sec) : base(aLock, startEvent, timeoutMS)
        {
            fileName = fName;
        }

        public bool ReadAllLines(out string[] allLines)
        {
            if (TakeOwnership() == false)
            {
                allLines = new string[0];
                return false;
            }

            try
            {
                allLines = File.ReadAllLines(fileName);
            }
            catch
            {
                allLines = new string[0];
                return false;
            }

            return true;
        }

        public bool AppendLine(string line)
        {
            if (TakeOwnership() == false)
                return false;

            try
            {
                using (StreamWriter sw = File.AppendText(fileName))
                {
                    sw.WriteLine(line);
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public bool Create()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    return false;

                if (!File.Exists(fileName))
                {
                    // Create a file to write to.
                    using (StreamWriter sw = File.CreateText(fileName))
                    {
                    }
                }

                return true;
            }
            catch
            {
                return false;   // This could be an invalid path (DirectoryNotFoundException)
            }
        }

        public bool Exists()
        {
            return File.Exists(fileName);
        }
    }

    internal class FileParameter
    {
        public SynchronizedFile file;
        public object parameter;

        public FileParameter(SynchronizedFile f, object p)
        {
            file = f;
            parameter = p;
        }
    }
}
