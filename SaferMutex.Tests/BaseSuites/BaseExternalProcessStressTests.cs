using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NiceIO;
using NUnit.Framework;
using SaferMutex.Tests.Utils;

namespace SaferMutex.Tests.BaseSuites
{
    public abstract class BaseExternalProcessStressTests : BaseTests
    {
        protected abstract string MutexTypeToCreate { get; }

        [Test]
        public void LotsOfProcessesGoingForTheMutex()
        {
            var name = nameof(LotsOfProcessesGoingForTheMutex);

            var waitFilePath = _tempDirectory.Combine("wait.txt").WriteAllText("A file to block the worker processes until we want them to start");

            var workers = new List<Process>();
            for (var i = 0; i < 50; i++)
            {
                workers.Add(StartWorkerProcess(name, waitFilePath));
            }

            waitFilePath.Delete();

            Assert.IsFalse(waitFilePath.Exists(), "The wait file should have been deleted by now.  The test is going to hang");

            CleanlyJoinAll(workers.ToArray());
        }

        [TestCase(50)]
        [TestCase(100)]
        [TestCase(500)]
        [TestCase(1000)]
        public void LotsOfProcessesIncrementingACounter(int processesToUse)
        {
            var name = nameof(LotsOfProcessesIncrementingACounter);
            var waitFilePath = _tempDirectory.Combine("wait.txt").WriteAllText("A file to block the worker processes until we want them to start");

            var counterFilePath = _tempDirectory.Combine("counter.txt").WriteAllText("0");

            List<Process> workers = new List<Process>();
            for (int i = 0; i < processesToUse; i++)
            {
                workers.Add(StartWorkerProcess(name, waitFilePath, "IncrementCounter", counterFilePath));
            }

            waitFilePath.Delete();

            CleanlyJoinAll(workers.ToArray());

            int counter = int.Parse(counterFilePath.ReadAllText());
            Assert.That(counter, Is.EqualTo(processesToUse));
        }

        private Process StartWorkerProcess(string mutexName, NPath waitFilePath, string mode = null, NPath sharedDataFilePath = null)
        {
            var mutexGrabberPath = NPath.CurrentDirectory.Combine("SaferMutex.Tests.MutexGrabber.exe");

            if (!mutexGrabberPath.Exists())
                throw new FileNotFoundException(mutexGrabberPath.ToString());

            string args = $"{MutexTypeToCreate} {mutexName} {_tempDirectory} {waitFilePath}";

            if (!string.IsNullOrEmpty(mode))
                args = $"{args} {mode} {sharedDataFilePath}";

            var startInfo = new ProcessStartInfo(mutexGrabberPath.ToString(), args);
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            var process = Process.Start(startInfo);
            return process;
        }

        private static void CleanlyJoinAll(params Process[] processWorkers)
        {
            Process failedProcess = null;
            foreach (var worker in processWorkers)
            {
                worker.WaitForExit();

                if (worker.ExitCode != 0 && failedProcess == null)
                    failedProcess = worker;
            }

            if (failedProcess != null)
            {
                var stdout = failedProcess.StandardOutput.ReadToEnd();
                Console.WriteLine(stdout);
                throw new Exception(stdout);
            }
        }
    }
}
