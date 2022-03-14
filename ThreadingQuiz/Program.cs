// See https://aka.ms/new-console-template for more information

/// <summary>
/// Eric Steitz
/// This is my first Core 6 style main (function) application, big change from the Main() we allknow and love
/// 
/// Everything could be done here in main however to demostrate object oriented design, two new classes were created;
///     ThreadSynchronizer - class to sychronize running threads
///     FileSynchronizer - class that inherit from ThreadSynchronizer to sychronize running threads accessing same file
/// 
/// Since we are running on Windows (during development) and Linux (inside Docker) will need to be aware of file name differences
/// As such test for Linux is done inside FileSynchronizer
///     if (OperatingSystem.IsLinux() == true)
///     {
///     }
/// 
/// Note: Assembly name (in build setting) changed from $(MSBuildProjectName) to "log" for log.exe
/// </summary>

using OlympusQuizES;

uint numberOfThreads = 10;
uint numberOfTimeStamps = 10;
string fileName = "/log/out.txt";

string[] arguments = Environment.GetCommandLineArgs();

if (arguments.Length >= 2) // missing minimal 
    fileName = arguments[1];    // If this path is invalid, it will be caught in WriteTimeStampToFile(). 

if (arguments.Length >= 3)
{
    // Allow user to set number of threads
    if (uint.TryParse(arguments[2], out numberOfThreads) == false)
        numberOfThreads = 10;
}

if (arguments.Length >= 4)
{
    // Allow user to set number of timestamps to log
    if (uint.TryParse(arguments[3], out numberOfTimeStamps) == false)
        numberOfTimeStamps = 10;
}

if (File.Exists(fileName) == true)
{
    // Cleanup old log file
    File.Delete(fileName);
}

List<SynchronizedFile>? fileAccessThreads = CreateFileAccessThreads(numberOfThreads, fileName, numberOfTimeStamps);    // Create list of file access threads
if (fileAccessThreads != null)
{
    StartAll();            // Since we set the threads for Synchronous start, calling StartAll() will start all threads
    WaitForAllComplete();  // Wait for all thread to complete or exit
    Cleanup();             // Delete list of file access threads
}

Console.WriteLine("Press any key to exit.");

try
{
    Console.ReadKey();  // End of main program
}
catch
{
    // When console is redirected ReadKey() exceptions.
    // Ok to ignore since end of program
}


/// **********************************************************************************
/// End of main program, below is callback and helper functions.
/// **********************************************************************************

/// <summary>
/// Function to write count & stampstamp to file
/// a.	Open the file
/// b. Read the first number on the last line of the file. This is your counter.
/// c.	Increment this counter 
/// d.	Append to the next line in the file “<incremented_counter_value>, <thread_id>, <current_time_stamp>” where thread_id is the current thread id. 
/// e.	Close the file 
/// </summary>
void WriteTimeStampToFile(object? theParameters)
{
    int thread_id = Thread.CurrentThread.ManagedThreadId;
    //Console.WriteLine("Thread: {0} - Started", thread_id);

    if (theParameters == null)
    {
        Console.WriteLine("Thread: {0} - WriteTimeStampToFile exited due to no parameters", thread_id);
        return;
    }

    FileParameter parameter = (FileParameter)theParameters;
    SynchronizedFile file = parameter.file;
    uint numberOfTimeStamp = (uint)parameter.parameter;

    // First take exclusive ownership so noone else can change file 
    if (file.TakeOwnership() == false)
    {
        Console.WriteLine("Thread: {0} - couldn't take ownership of file \"{1}\", thread will be stopped", thread_id, file.Filename);
        return;  // Function call exit
    }

    if (file.Exists() == false)
    {
        try
        {
            string? path = Path.GetDirectoryName(file.Filename);
            if ((path != null) && (Directory.Exists(path) == false))
                Directory.CreateDirectory(path);
        }
        catch
        {
            // No directory in path
            Console.WriteLine("Thread: {0} Failed attempt to create directory using \"{1}\"", thread_id, file.Filename);
        }

        // If file doesn't exist create it
        if (file.Create() == false)
        {
            // This could be due to invalid path
            Console.WriteLine("Thread: {0} - error creating file \"{1}\", thread will be stopped", thread_id, file.Filename);
            return;  // Function call exit
        }
    }

    file.ReleaseOwnership();

    uint counter = 0;
    string logEntry = CreateLogEntry(counter, thread_id);

    while (numberOfTimeStamp > 0)
    {
        try
        {
            // First take exclusive ownership so noone else can change file 
            if (file.TakeOwnership() == false)
            {
                Console.WriteLine("Thread: {0} - couldn't take ownership of file \"{1}\", stopped at counter = {2}", thread_id, file.Filename, counter);
                break;  // Function call exit
            }

            //Console.WriteLine("Thread: {0} - Took ownership", thread_id);

            // Read file to get last counter value
            string[] lines;
            if (file.ReadAllLines(out lines) == false)
            {
                Console.WriteLine("Thread: {0} - error reading file \"{1}\", stopped at counter = {2}", thread_id, file.Filename, counter);
                break;  // Function call exit
            }

            if (lines.Length > 0) // File is not empty (contains log entries)
            {
                uint lastCounter = 0;
                string lastLine = lines[lines.Length - 1];
                string[] elements = lastLine.Split(',', StringSplitOptions.TrimEntries);
                if (elements.Length < 3)
                {
                    // For this error (invalid line), let report it but continue since the error is not from this thread
                    Console.WriteLine("Thread: {0} - File contained an invalid log entry {1}", thread_id, lastLine);
                }
                else
                {
                    if (uint.TryParse(elements[0], out lastCounter) == false)
                    {
                        // For this error (invalid counter), let report it but continue since the error is not from this thread
                        Console.WriteLine("Thread: {0} - File contained an invalid counter value {1} in log entry {2}", thread_id, lines[0], lastLine);
                    }
                    else
                        counter = lastCounter + 1;
                }
            }

            // Write new counter value
            logEntry = CreateLogEntry(counter, thread_id);
            if (file.AppendLine(logEntry) == false)
            {
                Console.WriteLine("Thread: {0} - couldn't take ownership of file \"{1}\", stopped at counter = {2}", thread_id, file.Filename, counter);
                break;  // Function call exit
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Thread: {0} - terminated due to exception {1}", thread_id, ex.Message);
            break;  // Function call exit
        }
        finally
        {
            file.ReleaseOwnership();    // In case it was not released due an error
            numberOfTimeStamp--;
            //Console.WriteLine("Thread: {0} - Released ownership", thread_id);
        }
    }

    file.ReleaseOwnership();    // In case it was not released due an error
    //Console.WriteLine("Thread: {0} - Completed", thread_id);
}

/// <summary>
/// Create list of file access threads
/// Note, I passed in variablies only to may function more clear
/// </summary>
List<SynchronizedFile>? CreateFileAccessThreads(uint numOfThreads, string fName, uint numOfTimeStamps)
{
    try
    {
        List<SynchronizedFile> fileAccessThreads = new List<SynchronizedFile>();
        SynchronizedFile? file = null;
        FileParameter? parameter = null;
        for (int i = 0; i < numOfThreads; i++)
        {
            file = SynchronizedFiles.Create(fName, true);
            parameter = new FileParameter(file, numOfTimeStamps);
            file.Start(WriteTimeStampToFile, parameter, true);
            fileAccessThreads.Add(file);
        }

        return fileAccessThreads;
    }
    catch (Exception ex)
    {
        Console.WriteLine("Creating file access threads failed due to exception {0}", ex.Message);
        return null;
    }
}


/// <summary>
/// Create formatted log entry string 
/// </summary>
string CreateLogEntry(uint counter, int thread_id)
{
    string timestamp = DateTime.Now.ToString("hh:mm:ss.fff");
    return string.Format("{0}, {1}, {2}", counter, thread_id, timestamp);
}


/// <summary>
/// Start all threads when set for Synchronous start
/// </summary>
void StartAll()
{
    if (fileAccessThreads.Count > 0)
        fileAccessThreads[0].StartAll();
}


/// <summary>
/// Help to wait for all thread to exist or complete
/// </summary>
void WaitForAllComplete()
{
    bool aThreadIsRunning = true;
    do
    {
        aThreadIsRunning = true;
        for (int i = 0; i < numberOfThreads; i++)
        {
            aThreadIsRunning &= fileAccessThreads[i].IsStopped(); // Test if thread has ended
        }

    } while (aThreadIsRunning == false);
}


/// <summary>
/// This is allow the file name to be removed for list
/// </summary>
void Cleanup()  // Delete list of file access threads
{
    for (int i = 0; i < numberOfThreads; i++)
    {
        SynchronizedFiles.Delete(fileAccessThreads[i]); // This is reducing count of threads accessing file
    }

    fileAccessThreads.Clear();
}

