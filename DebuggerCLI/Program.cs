namespace DebuggerCLI
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using Nouzuru;

    class Program
    {
        static void Main(string[] args)
        {
            DebugMon d = new DebugMon();
            d.PauseOnSecondChanceException = true;
            d.EventsMonitored = DebugMon.EventFilter.All;
            d.ExceptionsMonitored = DebugMon.ExceptionFilter.All;
            if (Process.GetProcessesByName("realplay").Length == 0)
            {
                Process.Start(@"C:\Program Files (x86)\Real\RealPlayer\realplay.exe");
            }

            if (!d.Open("realplay"))
            {
                Console.WriteLine("Unknown error opening RealPlayer");
                return;
            }

            if (!d.StartDebugging())
            {
                Console.Error.WriteLine();
            }

            //d.WaitUntilInitialBreakpointIsHit();
            d.ResumeDebugging();

            // Main command loop.
            string userCommand = string.Empty;
            while (!userCommand.Equals("q") &&
                   !userCommand.Equals("exit") &&
                   !userCommand.Equals("quit"))
            {
                userCommand = Console.ReadLine();
                switch (userCommand)
                {
                    // disassemble
                    case "d":
                        d.DisassembleCurrentInstruction();
                        break;

                    // go
                    case "g":
                        d.ResumeDebugging();
                        Console.WriteLine("[+] Resumed.");
                        break;

                    // pause
                    case "p":
                        if (d.PauseDebugging())
                        {
                            Console.WriteLine("[+] Paused successfully.");
                        }
                        else
                        {
                            Console.WriteLine("[-] Pause unsuccessful.");
                        }

                        break;

                    // registers
                    case "r":
                        break;

                    // step into
                    case "si":
                        d.StepInto();
                        break;

                    // step over
                    case "so":
                        d.StepOver();
                        break;

                    default:
                        break;
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
