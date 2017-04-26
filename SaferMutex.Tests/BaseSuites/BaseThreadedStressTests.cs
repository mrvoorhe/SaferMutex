using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using SaferMutex.Tests.Utils;

namespace SaferMutex.Tests.BaseSuites
{
    public abstract class BaseThreadedStressTests : BaseTests
    {
        [Test]
        [Ignore("The case where there is contention over the initial creation of the mutex there are problems on OSX with mono.  Needs further investigation.  This use case may not even be valid")]
        public void LotsOfThreadsGoingForTheMutex()
        {
            var name = nameof(LotsOfThreadsGoingForTheMutex);
            using (var go = new ManualResetEvent(false))
            {
                List<Background> workers = new List<Background>();
                for(int i = 0; i < 50; i++)
                {
                    var bg = Background.Start(() =>
                    {
                        go.WaitAndAssertIfHung();

                        bool owned;
                        using (var mutex = CreateMutex(true, name, out owned))
                        {
                            if (!owned)
                            {
                                // Use timeout to avoid a hang if there is a bug
                                if(!mutex.WaitOne(UtilsAndExtensions.AvoidHangTimeout))
                                    Assert.Fail("Should have been able to obtain ownership of the mutex by now");
                            }
                        }
                    });

                    workers.Add(bg);
                }

                go.Set();

                CleanlyJoinAll(workers.ToArray());
            }
        }

        [Ignore("The case where there is contention over the initial creation of the mutex there are problems on OSX with mono.  Needs further investigation.  This use case may not even be valid")]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(500)]
        [TestCase(1000)]
        public void LotsOfThreadsIncrementingACounter(int threadsToUse)
        {
            int counter = 0;
            var name = nameof(LotsOfThreadsIncrementingACounter);
            using (var go = new ManualResetEvent(false))
            {
                List<Background> workers = new List<Background>();
                for (int i = 0; i < threadsToUse; i++)
                {
                    var bg = Background.Start(() =>
                    {
                        go.WaitAndAssertIfHung();

                        bool owned;
                        using (var mutex = CreateMutex(true, name, out owned))
                        {
                            if (!owned)
                            {
                                // Use timeout to avoid a hang if there is a bug
                                if (!mutex.WaitOne(UtilsAndExtensions.AvoidHangTimeout))
                                    Assert.Fail("Should have been able to obtain ownership of the mutex by now");
                            }

                            counter++;
                        }
                    });

                    workers.Add(bg);
                }

                go.Set();

                CleanlyJoinAll(workers.ToArray());
            }

            Assert.That(counter, Is.EqualTo(threadsToUse));
        }

        [Ignore("The case where there is contention over the initial creation of the mutex there are problems on OSX with mono.  Needs further investigation.  This use case may not even be valid")]
        [TestCase(50)]
        [TestCase(100)]
        public void WritingToACommonFile(int threadsToUse)
        {
            var name = nameof(WritingToACommonFile);
            var filePath = _tempDirectory.Combine($"{name}.txt");
            using (var go = new ManualResetEvent(false))
            {
                List<Background> workers = new List<Background>();
                for (int i = 0; i < threadsToUse; i++)
                {
                    var bg = Background.Start(index =>
                    {
                        go.WaitAndAssertIfHung();

                        bool owned;
                        using (var mutex = CreateMutex(true, name, out owned))
                        {
                            if (!owned)
                            {
                                // Use timeout to avoid a hang if there is a bug
                                if (!mutex.WaitOne(UtilsAndExtensions.AvoidHangTimeout))
                                    Assert.Fail("Should have been able to obtain ownership of the mutex by now");
                            }

                            using (var writer = new StreamWriter(filePath.ToString(), true))
                            {
                                writer.WriteLine($"I'm thread {index}");
                            }
                        }
                    },
                    i);

                    workers.Add(bg);
                }

                go.Set();

                CleanlyJoinAll(workers.ToArray());
            }

            var allLines = filePath.ReadAllLines();
            Assert.That(allLines.Length, Is.EqualTo(threadsToUse));

            // Make sure the data we wrote is roughly correct
            foreach(var line in allLines)
                Assert.IsTrue(line.StartsWith("I'm thread "), $"Something went wrong.  A line didn't have the expected output : {line}");
        }

        [TestCase(50)]
        [TestCase(100)]
        public void WritingToACommonFileWhenParentThreadCreatesMutex(int threadsToUse)
        {
            var name = nameof(WritingToACommonFileWhenParentThreadCreatesMutex);
            var filePath = _tempDirectory.Combine($"{name}.txt");
            using (var go = new ManualResetEvent(false))
            {
                using (var initiallyCreatedMutex = CreateMutex(true, name))
                {
                    initiallyCreatedMutex.ReleaseMutex();

                    List<Background> workers = new List<Background>();
                    for (int i = 0; i < threadsToUse; i++)
                    {
                        var bg = Background.Start(index =>
                            {
                                go.WaitAndAssertIfHung();

                                bool owned;
                                using (var mutex = CreateMutex(true, name, out owned))
                                {
                                    if (!owned)
                                    {
                                        // Use timeout to avoid a hang if there is a bug
                                        if (!mutex.WaitOne(UtilsAndExtensions.AvoidHangTimeout))
                                            Assert.Fail(
                                                "Should have been able to obtain ownership of the mutex by now");
                                    }

                                    using (var writer = new StreamWriter(filePath.ToString(), true))
                                    {
                                        writer.WriteLine($"I'm thread {index}");
                                    }
                                }
                            },
                            i);

                        workers.Add(bg);
                    }

                    go.Set();

                    CleanlyJoinAll(workers.ToArray());
                }
            }

            var allLines = filePath.ReadAllLines();
            Assert.That(allLines.Length, Is.EqualTo(threadsToUse));

            // Make sure the data we wrote is roughly correct
            foreach(var line in allLines)
                Assert.IsTrue(line.StartsWith("I'm thread "), $"Something went wrong.  A line didn't have the expected output : {line}");
        }

        private static void CleanlyJoinAll(params Background[] backgroundWorkers)
        {
            Exception backgroundException = null;
            foreach (var worker in backgroundWorkers)
            {
                try
                {
                    worker.Wait();
                }
                catch (Exception ex)
                {
                    if (backgroundException == null)
                        backgroundException = ex;
                }
            }

            if (backgroundException != null)
                throw backgroundException;
        }
    }
}
