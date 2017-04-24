using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SaferMutex.Tests.MutexGrabber
{
    class Program
    {
        private static string failureLogFilePath;

        static int Main(string[] args)
        {
            var mutexType = args[0];
            var mutexName = args[1];
            var testTemporaryDirectory = args[2];
            var waitFilePath = args[3];

            var mode = args.Length >= 6 ? args[4] : string.Empty;
            var sharedFilePath = args.Length >= 6 ? args[5] : string.Empty;

            failureLogFilePath = Path.Combine(testTemporaryDirectory, $"failure-log-{Process.GetCurrentProcess().Id}.txt");

	        var aliveFilePath = Path.Combine(testTemporaryDirectory, $"alive-{Process.GetCurrentProcess().Id}.txt");
	        using (var aliveWriter = new StreamWriter(aliveFilePath))
	        {
		        aliveWriter.WriteLine("I Started!");

		        try
		        {
			        Action safeAction = null;
			        if (mode == "IncrementCounter")
				        safeAction = () => ImcrementCounter(sharedFilePath);
			        else if (mode == "WriteToCommonFile")
				        safeAction = () => WriteProcessId(sharedFilePath);
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
					        	return 1;
					        }
					        aliveWriter.WriteLine("I need to wait for the mutex");
				        }

				        aliveWriter.WriteLine("Got the mutex");

				        if (safeAction != null)
				        {
					        aliveWriter.WriteLine("Calling my action!");
					        safeAction();
				        }

				        mutex.ReleaseMutex();
			        }

			        aliveWriter.WriteLine("I Ended!");

			        return 0;
		        }
		        catch (Exception e)
		        {
			        LogOutput("MutexGrabber crashed");
			        LogOutput(e.Message);
			        LogOutput(e.StackTrace);
			        return 2;
		        }
	        }
        }

        private static void LogOutput(string message)
        {
            Console.WriteLine(message);
            using (var writer = new StreamWriter(failureLogFilePath))
            {
                writer.WriteLine(message);
            }
        }

        private static ISaferMutexMutex CreateMutex(string mutexType, string temporaryDirectory, bool initiallyOwned, string name, out bool owned)
        {
            bool createdNew;
            switch (mutexType)
            {
                case "FileBasedGlobal":
                    return new SaferMutex.FileBased(initiallyOwned, name, Scope.CurrentUser, out owned, out createdNew, temporaryDirectory);
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
                value = int.Parse(reader.ReadLine());
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
