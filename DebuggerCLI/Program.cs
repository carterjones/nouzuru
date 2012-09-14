namespace DebuggerCLI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using Nouzuru;

    class Program
    {
        static void Main(string[] args)
        {
            DebugMon d = new DebugMon();
            d.LogBreakpointAccesses = true;
            d.LogRegistersOnBreakpoint = true;
            d.BlockOnSecondChanceException = true;
            d.EventsMonitored = DebugMon.EventFilter.All;
            d.ExceptionsMonitored = DebugMon.ExceptionFilter.All;
            d.Open("realplay");
            if (!d.StartDebugging())
            {
                Console.Error.WriteLine();
            }

            d.WaitUntilInitialBreakpointIsHit();
            d.ContinueDebugging();

            // Main command loop.
            string userCommand = string.Empty;
            while (!userCommand.Equals("q") &&
                   !userCommand.Equals("exit") &&
                   !userCommand.Equals("quit"))
            {
                userCommand = Console.ReadLine();
                if (userCommand.Equals("p"))
                {
                    if (d.Pause())
                    {
                        Console.WriteLine("paused");
                    }
                    else
                    {
                        Console.WriteLine("pause unsuccessful");
                    }
                }
                else if (userCommand.Equals("r"))
                {
                    d.Resume();
                    Console.WriteLine("resumed");
                }
            }

            if (!d.StopDebugging())
            {
                Console.Error.WriteLine("Could not stop debugging.");
            }

            Console.WriteLine();
        }
    }
}
