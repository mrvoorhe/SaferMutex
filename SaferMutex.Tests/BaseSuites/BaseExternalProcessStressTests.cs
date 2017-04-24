using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
                workers.Add(StartWorkerProcess(name, waitFilePath));

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

            Console.WriteLine(_tempDirectory);

            var workers = new List<Process>();

            try
            {
                for (var i = 0; i < processesToUse; i++)
                    workers.Add(StartWorkerProcess(name, waitFilePath, "IncrementCounter", counterFilePath));

                waitFilePath.Delete();

                CleanlyJoinAll(workers.ToArray());
            }
            catch (Exception)
            {
                KillRemainingRunningWorkers(workers);
                throw;
            }

            var counter = int.Parse(counterFilePath.ReadAllText());
            Assert.That(counter, Is.EqualTo(processesToUse));
        }

        [TestCase(50, 20)]
        [TestCase(100, 20)]
        [TestCase(500, 20)]
        [TestCase(1000, 20)]
        public void LotsOfProcessesIncrementingACounter_MultiPass(int processesToUse, int passes)
        {
            for (int i = 0; i < passes; i++)
            {
                LotsOfProcessesWritingToACommonFile(processesToUse);

                _tempDirectory.DeleteContents();
            }
        }

        [TestCase(50)]
        [TestCase(100)]
        public void LotsOfProcessesWritingToACommonFile(int processesToUse)
        {
            var name = nameof(LotsOfProcessesWritingToACommonFile);
            var waitFilePath = _tempDirectory.Combine("wait.txt").WriteAllText("A file to block the worker processes until we want them to start");
            var filePath = _tempDirectory.Combine($"{name}.txt");

            Console.WriteLine(_tempDirectory);

            var workers = new List<Process>();

            try
            {
                for (var i = 0; i < processesToUse; i++)
                    workers.Add(StartWorkerProcess(name, waitFilePath, "WriteToCommonFile", filePath));

                waitFilePath.Delete();

                CleanlyJoinAll(workers.ToArray());
            }
            catch (Exception)
            {
                KillRemainingRunningWorkers(workers);
                throw;
            }

            var allLines = filePath.ReadAllLines();
            Assert.That(allLines.Length, Is.EqualTo(processesToUse));

            // Make sure the data we wrote is roughly correct
            foreach (var line in allLines)
                Assert.IsTrue(line.StartsWith("I'm Process "), $"Something went wrong.  A line didn't have the expected output : {line}");
        }

        [TestCase(50, 20)]
        [TestCase(100, 20)]
        public void LotsOfProcessesWritingToACommonFile_MultiPass(int processesToUse, int passes)
        {
            for (int i = 0; i < passes; i++)
            {
                LotsOfProcessesWritingToACommonFile(processesToUse);

                _tempDirectory.DeleteContents();
            }
        }

        private static void KillRemainingRunningWorkers(IEnumerable<Process> workers)
        {
            foreach (var worker in workers)
            {
                try
                {
                    if (worker.HasExited)
                        continue;
                    worker.Kill();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }
            }
        }

        private Process StartWorkerProcess(string mutexName, NPath waitFilePath, string mode = null, NPath sharedDataFilePath = null, string moreData = null)
        {
            var mutexGrabberPath = NPath.CurrentDirectory.Combine("SaferMutex.Tests.MutexGrabber.exe");

            if (!mutexGrabberPath.Exists())
                throw new FileNotFoundException(mutexGrabberPath.ToString());

            StringBuilder argBuilder = new StringBuilder();
            argBuilder.Append($"{MutexTypeToCreate} {mutexName} {_tempDirectory} {waitFilePath}");

            if (!string.IsNullOrEmpty(mode))
            {
                argBuilder.Append($" {mode} {sharedDataFilePath}");

                if (!string.IsNullOrEmpty(moreData))
                    argBuilder.Append($" {moreData}");
            }

            var startInfo = new ProcessStartInfo(mutexGrabberPath.ToString(), argBuilder.ToString());
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
