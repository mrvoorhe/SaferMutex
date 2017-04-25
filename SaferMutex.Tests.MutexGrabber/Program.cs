﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SaferMutex.Tests.MutexGrabber
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            var mutexType = args[0];
            var mutexName = args[1];
            var testTemporaryDirectory = args[2];
            var waitFilePath = args[3];

            var mode = args.Length >= 6 ? args[4] : string.Empty;
            var sharedFilePath = args.Length >= 6 ? args[5] : string.Empty;

            var aliveFilePath = Path.Combine(testTemporaryDirectory, $"output-{Process.GetCurrentProcess().Id}.txt");
            using (var outputWriter = new StreamWriter(aliveFilePath))
            {
                var originalStdout = Console.Out;
                Console.SetOut(outputWriter);
                try
                {
                    LogOutput("I Started!");

                    try
                    {
                        Action safeAction = null;
                        if (mode == "IncrementCounter")
                        {
                            safeAction = () => ImcrementCounter(sharedFilePath);
                        }
                        else if (mode == "WriteToCommonFile")
                        {
                            safeAction = () => WriteProcessId(sharedFilePath);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(mode))
                            {
                                LogOutput($"Unknown run mode {mode}");
                                return 1;
                            }
                        }

                        // Block until wait file is deleted
                        while (File.Exists(waitFilePath))
                            Thread.Sleep(1);

                        bool owned;
                        using (var mutex = CreateMutex(mutexType, testTemporaryDirectory, true, mutexName, out owned))
                        {
                            if (!owned)
                            {
                                // Use timeout to avoid a hang if there is a bug
                                if (!mutex.WaitOne(10000))
                                {
                                    LogOutput("Should have been able to obtain ownership of the mutex by now");
                                    return 2;
                                }
                                LogOutput("I need to wait for the mutex");
                            }

                            LogOutput("Got the mutex");

                            if (safeAction != null)
                            {
                                LogOutput("Calling my action!");
                                safeAction();
                            }

                            LogOutput("About to release the mutex!");
                            mutex.ReleaseMutex();
                            LogOutput("Mutex released!");
                        }

                        LogOutput("I Ended!");

                        return 0;
                    }
                    catch (Exception e)
                    {
                        LogOutput("MutexGrabber crashed");
                        LogOutput(e.Message);
                        LogOutput(e.StackTrace);
                        return 3;
                    }
                }
                finally
                {
                    Console.SetOut(originalStdout);
                }
            }
        }

        private static void LogOutput(string message)
        {
            Console.WriteLine(message);
        }

        private static ISaferMutexMutex CreateMutex(string mutexType, string temporaryDirectory, bool initiallyOwned, string name, out bool owned)
        {
            bool createdNew;
            switch (mutexType)
            {
                case "FileBasedGlobal":
                    return new SaferMutex.FileBased(initiallyOwned, name, Scope.CurrentUser, out owned, out createdNew, temporaryDirectory);
                case "FileBased2Global":
                    return new SaferMutex.FileBased2(initiallyOwned, name, Scope.CurrentUser, out owned, out createdNew, temporaryDirectory);
                case "FrameworkBasedGlobal":
                    return new SaferMutex.FrameworkMutexBased(initiallyOwned, name, Scope.CurrentUser, out owned, out createdNew);
                default:
                    throw new InvalidOperationException($"Unknowhed mutexType : {mutexType}");
            }
        }

        private static void ImcrementCounter(string dataFilePath)
        {
            int value;
            using (var reader = new StreamReader(dataFilePath))
            {
                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                    throw new InvalidOperationException("Something went wrong, there is no number in the counter file to increment");
                value = int.Parse(line);
            }

            value++;

            using (var writer = new StreamWriter(dataFilePath))
            {
                writer.WriteLine(value);
            }
        }

        private static void WriteProcessId(string dataFilePath)
        {
            using (var writer = new StreamWriter(dataFilePath, true))
            {
                writer.WriteLine($"I'm Process {Process.GetCurrentProcess().Id}");
            }
        }
    }
}
