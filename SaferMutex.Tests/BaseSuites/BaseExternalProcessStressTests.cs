using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NiceIO;
using NUnit.Framework;

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

        [TestCase(50, 1)]
        [TestCase(100, 1)]
        //[TestCase(500, 1)]
        //[TestCase(1000, 1)]
        [TestCase(10, 10)]
        [TestCase(20, 10)]
        [TestCase(20, 20)]
        [TestCase(50, 20)]
        public void IncrementingACounter(int processesToUse, int passes)
        {
            Console.WriteLine(_tempDirectory);

            MultiPassHelper(passes,
                () =>
                {
                    var name = nameof(IncrementingACounter);
                    IncrementingACounterHelper(processesToUse, name);
                });
        }

        [TestCase(50, 1)]
        [TestCase(100, 1)]
        //[TestCase(500, 1)]
        //[TestCase(1000, 1)]
        [TestCase(10, 10)]
        [TestCase(20, 10)]
        [TestCase(20, 20)]
        [TestCase(50, 20)]
        public void IncrementingACounterWithParentProcessCreatingMutex(int processesToUse, int passes)
        {
            Console.WriteLine(_tempDirectory);

            MultiPassHelper(passes,
                () =>
                {
                    var name = nameof(IncrementingACounterWithParentProcessCreatingMutex);
                    using (var mutex = CreateMutex(true, name))
                    {
                        mutex.ReleaseMutex();

                        IncrementingACounterHelper(processesToUse, name);
                    }
                });
        }

        [TestCase(50, 1)]
        [TestCase(100, 1)]
        //[TestCase(500, 1)]
        //[TestCase(1000, 1)]
        [TestCase(10, 10)]
        [TestCase(20, 10)]
        [TestCase(20, 20)]
        [TestCase(50, 20)]
        public void IncrementingACounterWithParentProcessCreatingMutexAndReleaseAfterStart(int processesToUse, int passes)
        {
            Console.WriteLine(_tempDirectory);

            MultiPassHelper(passes,
                () =>
                {
                    var name = nameof(IncrementingACounterWithParentProcessCreatingMutexAndReleaseAfterStart);
                    using (var mutex = CreateMutex(true, name))
                    {
                        IncrementingACounterHelper(processesToUse, name, () =>
                        {
                            //System.Threading.Thread.Sleep(1000);
                            mutex.ReleaseMutex();
                        });
                    }
                });
        }

        [TestCase(50, 1)]
        [TestCase(100, 1)]
        [TestCase(50, 20)]
        [TestCase(100, 20)]
        public void WritingToACommonFile(int processesToUse, int passes)
        {
            Console.WriteLine(_tempDirectory);

            MultiPassHelper(passes,
                () =>
                {
                    var name = nameof(WritingToACommonFile);
                    WritingToACommonFileHelper(processesToUse, name);
                });
        }

        [TestCase(20, 1)]
        [TestCase(50, 1)]
        [TestCase(20, 20)]
        [TestCase(50, 20)]
        public void WritingToACommonFileWithParentProcessCreatingMutex(int processesToUse, int passes)
        {
            Console.WriteLine(_tempDirectory);

            MultiPassHelper(passes,
                () =>
                {
                    var name = nameof(WritingToACommonFileWithParentProcessCreatingMutex);
                    using (var mutex = CreateMutex(true, name))
                    {
                        mutex.ReleaseMutex();

                        WritingToACommonFileHelper(processesToUse, name);
                    }
                });
        }

        private void MultiPassHelper(int passes, Action testFunc)
        {
            for (var i = 0; i < passes; i++)
            {
                _tempDirectory.DeleteContents();

                testFunc();

                Console.WriteLine($"Iteration #{i} OK");
            }
        }

        private void WritingToACommonFileHelper(int processesToUse, string mutexName)
        {
            var waitFilePath = _tempDirectory.Combine("wait.txt").WriteAllText("A file to block the worker processes until we want them to start");
            var filePath = _tempDirectory.Combine($"{mutexName}.txt");

            RunWorkers(processesToUse, mutexName, waitFilePath, "WriteToCommonFile", filePath);

            var allLines = filePath.ReadAllLines();
            Assert.That(allLines.Length, Is.EqualTo(processesToUse));

            // Make sure the data we wrote is roughly correct
            foreach (var line in allLines)
                Assert.IsTrue(line.StartsWith("I'm Process "), $"Something went wrong.  A line didn't have the expected output : {line}");
        }

        public void IncrementingACounterHelper(int processesToUse, string mutexName, Action afterStart = null)
        {
            var waitFilePath = _tempDirectory.Combine("wait.txt").WriteAllText("A file to block the worker processes until we want them to start");
            var counterFilePath = _tempDirectory.Combine("counter.txt").WriteAllText("0");

            RunWorkers(processesToUse, mutexName, waitFilePath, "IncrementCounter", counterFilePath, afterStart);

            var counter = int.Parse(counterFilePath.ReadAllText());
            Assert.That(counter, Is.EqualTo(processesToUse));
        }

        private void RunWorkers(int processesToUse, string name, NPath waitFilePath, string grabberMode, NPath dataFile, Action afterStart = null)
        {
            var workers = new List<Process>();
            try
            {
                for (var i = 0; i < processesToUse; i++)
                    workers.Add(StartWorkerProcess(name, waitFilePath, grabberMode, dataFile));

                waitFilePath.Delete();

                if (afterStart != null)
                    afterStart();

                CleanlyJoinAll(workers.ToArray());
            }
            catch (Exception)
            {
                KillRemainingRunningWorkers(workers);
                throw;
            }
        }

        private static void KillRemainingRunningWorkers(IEnumerable<Process> workers)
        {
            foreach (var worker in workers)
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

        private Process StartWorkerProcess(string mutexName, NPath waitFilePath, string mode = null, NPath sharedDataFilePath = null, string moreData = null)
        {
            var mutexGrabberPath = NPath.CurrentDirectory.Combine("SaferMutex.Tests.MutexGrabber.exe");

            if (!mutexGrabberPath.Exists())
                throw new FileNotFoundException(mutexGrabberPath.ToString());

            var argBuilder = new StringBuilder();
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

        private void CleanlyJoinAll(params Process[] processWorkers)
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
                var outputFilePath = _tempDirectory.Combine($"output-{failedProcess.Id}.txt");
                var workerOutput = outputFilePath.Exists()
                    ? outputFilePath.ReadAllText()
                    : $"No output file at : {outputFilePath}";
                throw new Exception($"Worker process {failedProcess.Id} exited with a non-zero exit code of {failedProcess.ExitCode}.  Output was:\n{workerOutput}");
            }
        }
    }
}
