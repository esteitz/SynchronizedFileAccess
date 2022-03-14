using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OlympusQuizES
{
    internal class ThreadSynchronizer
    {
        public const int Default30Sec = 30000;
        private object theLock = new object();
        private int timeoutMS = Default30Sec;

        Thread? thread = null;
        ManualResetEvent? startEvent = null;

       public ThreadSynchronizer(object alock, ManualResetEvent? sEvent = null, int atimeoutMS = Default30Sec)
        {
            if (alock != null)
                theLock = alock;

            startEvent = sEvent;
        }

        public bool TakeOwnership()
        {
            if (Monitor.IsEntered(theLock) == true)
                return true;

            // Using Monitor (instead of Lock()) so to have timeout 
            bool lockTaken = false;
            Monitor.TryEnter(theLock, timeoutMS, ref lockTaken);

            return lockTaken;
        }
        public void ReleaseOwnership()
        {
            if (Monitor.IsEntered(theLock) == false)
                return;

            Monitor.Exit(theLock);
        }

        public void Start(ParameterizedThreadStart? function, object parameter, bool synchronousStart = false)
        {
            if ((function != null) && (synchronousStart == false))
            {
                thread = new Thread(function);
                thread.Start(parameter);
                return;
            }

            theFunction = function;
            theParameter = parameter;
            Thread synchronousThread = new Thread(SynchronousStart);
            synchronousThread.Start();
        }

        ParameterizedThreadStart? theFunction = null;
        object? theParameter = null;
        private void SynchronousStart()
        {
            if (startEvent != null)
                startEvent.WaitOne();

            if (theFunction != null)
            {
                thread = new Thread(theFunction);
                thread.Start(theParameter);
            }
        }

        public void StartAll()
        {
            if (startEvent != null)
                startEvent.Set();   
        }

        public bool Join(int millisecondsTimeout)
        {
            if (thread == null)
                return true;

            bool running = thread.Join(millisecondsTimeout);
            return running;
        }

        public bool IsStopped()
        {
            if (thread == null)
                return false;

            if (thread.ThreadState == ThreadState.Stopped)
                return true;

            return false;
        }
    }
}
