using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using SaferMutex.Tests.Utils;

namespace SaferMutex.Tests.BaseSuites
{
    public abstract class BaseThreadedStressTests : BaseTests
    {
        [Test]
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
