using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SaferMutex.Tests.Utils;
using NUnit.Framework;

namespace SaferMutex.Tests.BaseSuites
{
    public abstract class BaseThreadedTests
    {
        #region Initial Ownership

        [Test]
        public void InitiallyOwnedAndCreatedNew()
        {
            var name = nameof(InitiallyOwnedAndCreatedNew);
            using (var mutex = CreateMutex(true, name))
            {
                mutex.AssertOwned();
                mutex.AssertCreatedNew();
                mutex.AssertOwnershipCannotBeObtainedByDifferentThread();
            }
        }

        [Test]
        public void NotInitiallyOwnedButStillCreatedNew()
        {
            var name = nameof(NotInitiallyOwnedButStillCreatedNew);
            using (var mutex = CreateMutex(false, name))
            {
                mutex.AssertNotOwned();
                mutex.AssertCreatedNew();
                mutex.AssertOwnershipCanBeObtainedByDifferentThread();
            }
        }

        [Test]
        public void SecondThreadCreatesMutexWithIntiallyOwned()
        {
            var name = nameof(SecondThreadCreatesMutexWithIntiallyOwned);
            using (var mutex = CreateMutex(true, name))
            {
                mutex.AssertOwnershipCannotBeObtainedByDifferentThread("Sanity check");

                Background.Start(() =>
                {
                    using (var mutex2 = CreateMutex(true, name))
                    {
                        mutex2.AssertNotOwned();
                        mutex2.AssertNotCreatedNew();
                        mutex2.AssertOwnershipCannotBeObtainedByDifferentThread();
                    }
                }).Wait();
            }
        }

        [Test]
        public void SecondThreadCreatesMutexButNotIntiallyOwned()
        {
            var name = nameof(SecondThreadCreatesMutexButNotIntiallyOwned);
            using (var mutex = CreateMutex(true, name))
            {
                mutex.AssertOwnershipCannotBeObtainedByDifferentThread("Sanity check");

                Background.Start(() =>
                {
                    using (var mutex2 = CreateMutex(false, name))
                    {
                        mutex2.AssertNotOwned();
                        mutex2.AssertNotCreatedNew();
                        mutex2.AssertOwnershipCannotBeObtainedByDifferentThread();
                    }
                }).Wait();
            }
        }

        [Test]
        public void SecondThreadCreatesMutexWithIntiallyOwned_FirstDidNotOwn()
        {
            var name = nameof(SecondThreadCreatesMutexWithIntiallyOwned_FirstDidNotOwn);
            using (var mutex = CreateMutex(false, name))
            {
                mutex.AssertCreatedNew();
                mutex.AssertOwnershipCanBeObtainedByDifferentThread("Sanity check");

                Background.Start(() =>
                {
                    using (var mutex2 = CreateMutex(true, name))
                    {
                        mutex2.AssertOwned();
                        mutex2.AssertNotCreatedNew();
                        mutex2.AssertOwnershipCannotBeObtainedByDifferentThread();
                    }
                }).Wait();
            }
        }

        [Test]
        public void CreatedNewWhenFirstMutexCreatedDidNotRequestInitialOwnership()
        {
            CreatedMutexContainer mutex1 = null;
            CreatedMutexContainer mutex2 = null;

            try
            {
                mutex1 = CreateMutex(false, nameof(CreatedNewWhenFirstMutexCreatedDidNotRequestInitialOwnership));
                mutex1.AssertNotOwned();
                mutex1.AssertCreatedNew();

                mutex2 = CreateMutex(false, nameof(CreatedNewWhenFirstMutexCreatedDidNotRequestInitialOwnership));
                mutex2.AssertNotOwned();
                mutex2.AssertNotCreatedNew();
            }
            finally
            {
                DisposeOfMutexsAsCleanlyAsPossible(mutex1, mutex2);
            }
        }

        [Test]
        public void CreatedNewWhenFirstMutexCreatedDidNotRequestInitialOwnershipAndSecondDid()
        {
            CreatedMutexContainer mutex1 = null;
            CreatedMutexContainer mutex2 = null;

            try
            {
                mutex1 = CreateMutex(false, nameof(CreatedNewWhenFirstMutexCreatedDidNotRequestInitialOwnershipAndSecondDid));
                mutex1.AssertNotOwned();
                mutex1.AssertCreatedNew();

                mutex2 = CreateMutex(true, nameof(CreatedNewWhenFirstMutexCreatedDidNotRequestInitialOwnershipAndSecondDid));
                mutex2.AssertOwned();
                mutex2.AssertNotCreatedNew();
            }
            finally
            {
                DisposeOfMutexsAsCleanlyAsPossible(mutex1, mutex2);
            }
        }

        #endregion

        #region Simple Cases

        [Test]
        public void ThrowsIfGlobalPrefixIsUsedInName()
        {
            Assert.Throws<ArgumentException>(() => CreateMutex(false, $"Global\\{nameof(ThrowsIfGlobalPrefixIsUsedInName)}"));
        }

        [Test]
        public void ObtainAndExplicitRelease()
        {
            var name = nameof(ObtainAndExplicitRelease);
            using (var mutex = CreateMutex(true, name))
            {
                mutex.AssertOwnershipCannotBeObtainedByDifferentThread("Sanity Check");

                mutex.ReleaseMutex();
                mutex.AssertOwnershipCanBeObtainedByDifferentThread();
            }
        }

        [Test]
        public void DisposeCalledWithoutReleasing()
        {
            var name = nameof(DisposeCalledWithoutReleasing);
            var mutex = CreateMutex(true, name);
            mutex.Dispose();

            mutex.AssertOwnershipCanBeObtainedByDifferentThread();
        }

        [Test]
        public void DisposeCalledWithoutReleasingWhenNotOwned()
        {
            var name = nameof(DisposeCalledWithoutReleasingWhenNotOwned);
            var mutex = CreateMutex(false, name);
            mutex.Dispose();

            mutex.AssertOwnershipCanBeObtainedByDifferentThread();
        }

        [Test]
        public void DisposeCalledWithoutReleasingOwnerShipObtainedAfterWaitOne()
        {
            var name = nameof(DisposeCalledWithoutReleasingOwnerShipObtainedAfterWaitOne);
            var mutex = CreateMutex(false, name);
            var owned = mutex.WaitOne(0);

            Assert.IsTrue(owned, "Sanity check");
            mutex.Dispose();

            mutex.AssertOwnershipCanBeObtainedByDifferentThread();
        }

        [Test]
        public void DisposeCalledWithoutReleasingOwnerShipObtainedAfterWaitOneNoTimeout()
        {
            var name = nameof(DisposeCalledWithoutReleasingOwnerShipObtainedAfterWaitOneNoTimeout);
            var mutex = CreateMutex(false, name);
            mutex.WaitOne();
            mutex.Dispose();

            mutex.AssertOwnershipCanBeObtainedByDifferentThread();
        }

        [Test]
        public void ObtainOwnershipViaWaitOneNoTimeout()
        {
            var name = nameof(ObtainOwnershipViaWaitOneNoTimeout);
            using (var mutex = CreateMutex(false, name))
            {
                mutex.AssertNotOwned();

                mutex.WaitOne();

                mutex.AssertOwnershipCannotBeObtainedByDifferentThread();
            }
        }

        [Test]
        public void ObtainOwnershipViaWaitOne()
        {
            var name = nameof(ObtainOwnershipViaWaitOne);
            bool owned;
            using (var mutex = CreateMutex(false, name))
            {
                mutex.AssertNotOwned();

                // Timeout isn't needed, but use one to avoid a hung test
                owned = mutex.WaitOne(UtilsAndExtensions.AvoidHangTimeout);

                Assert.IsTrue(owned, "Expected to own the mutex");
                mutex.AssertOwnershipCannotBeObtainedByDifferentThread();
            }
        }

        [Test]
        public void ObtainOwnershipViaWaitOneZeroTimeout()
        {
            var name = nameof(ObtainOwnershipViaWaitOne);
            bool owned;
            using (var mutex = CreateMutex(false, name))
            {
                mutex.AssertNotOwned();

                // Timeout isn't needed, but use one to avoid a hung test
                owned = mutex.WaitOne(0);

                Assert.IsTrue(owned, "Expected to own the mutex");
                mutex.AssertOwnershipCannotBeObtainedByDifferentThread();
            }
        }

        [Test]
        [Ignore("Do I really want this behavior?  .NET doesn't work like this.  It would require additional logic to deal with this")]
        public void OwnershipCanBeObtainedReleasedAndObtainedAgain_StartedWithOwned()
        {
            var name = nameof(OwnershipCanBeObtainedReleasedAndObtainedAgain_StartedWithOwned);
            using (var mutex = CreateMutex(false, name))
            {
                for (int i = 0; i < 3; i++)
                {
                    var owned = mutex.WaitOne(0);

                    Assert.IsTrue(owned, $"Expected to own the mutex on pass {i}");
                    mutex.AssertOwnershipCannotBeObtainedByDifferentThread();

                    mutex.ReleaseMutex();

                    mutex.AssertOwnershipCanBeObtainedByDifferentThread($"On pass {i}");
                }
            }
        }

        [Test]
        public void DifferentNamesCanBeOwnedAtTheSameTime()
        {
            ISaferMutexMutex mutex1 = null;
            ISaferMutexMutex mutex2 = null;

            try
            {
                bool owned1;
                mutex1 = CreateMutex(true, $"{nameof(DifferentNamesCanBeOwnedAtTheSameTime)}_1", out owned1);

                Assert.IsTrue(owned1, "Expected to own the mutex after creation");

                bool owned2;
                mutex2 = CreateMutex(true, $"{nameof(DifferentNamesCanBeOwnedAtTheSameTime)}_2", out owned2);

                Assert.IsTrue(owned2, "Expected to own the mutex after creation");
            }
            finally
            {
                DisposeOfMutexsAsCleanlyAsPossible(mutex1, mutex2);
            }
        }

        [Test]
        public void DoubleDisposedWhenNotOwned()
        {
            var name = nameof(DoubleDisposedWhenNotOwned);
            var mutex = CreateMutex(false, name);

            mutex.AssertNotOwned();

            mutex.Dispose();
            mutex.Dispose();
        }

        [Test]
        public void DoubleDisposedWhenOwned()
        {
            var name = nameof(DoubleDisposedWhenOwned);
            var mutex = CreateMutex(true, name);

            mutex.AssertOwned();

            mutex.Dispose();
            mutex.Dispose();
        }

        #endregion

        #region Multi Thread Cases


        [Test]
        public void SecondThreadTriesToTakeOwnershipOfMutexThatIsAlreadyOwnedViaWaitOne()
        {
            var name = nameof(SecondThreadTriesToTakeOwnershipOfMutexThatIsAlreadyOwnedViaWaitOne);
            using (var mutex = CreateMutex(true, name))
            {
                Background.Start(() =>
                {
                    using (var mutex2 = CreateMutex(false, name))
                    {
                        mutex2.AssertNotOwned();

                        var owned2 = mutex2.WaitOne(0);

                        Assert.IsFalse(owned2, "Expected to be unable to obtain ownership of the mutex");
                    }
                }).Wait();
            }
        }

        [Test]
        public void SecondThreadTriesToTakeOwnershipOfMutexThatIsAlreadyOwnedViaWaitOne_NonZeroWaitTime()
        {
            var name = nameof(SecondThreadTriesToTakeOwnershipOfMutexThatIsAlreadyOwnedViaWaitOne_NonZeroWaitTime);
            using (var mutex = CreateMutex(true, name))
            {
                Background.Start(() =>
                {
                    using (var mutex2 = CreateMutex(false, name))
                    {
                        mutex2.AssertNotOwned();

                        var owned2 = mutex2.WaitOne(10);

                        Assert.IsFalse(owned2, "Expected to be unable to obtain ownership of the mutex");
                    }
                }).Wait();
            }
        }

        [Test]
        public void WaitingThreadCanObtainOwnershipWhenTheMutexIsReleased()
        {
            var name = nameof(WaitingThreadCanObtainOwnershipWhenTheMutexIsReleased);
            bool owned;
            using (var mutex = CreateMutex(true, name, out owned))
            {
                using (var pastCreation = new ManualResetEvent(false))
                {
                    var task = Background.Start(() =>
                    {
                        using (var mutex2 = CreateMutex(false, name))
                        {
                            mutex2.AssertNotOwned();

                            pastCreation.Set();

                            var owned2 = mutex2.WaitOne(UtilsAndExtensions.AvoidHangTimeout);

                            Assert.IsTrue(owned2, "Expected to obtain the mutex because the first owner was expected to release it");
                        }
                    });

                    pastCreation.WaitAndAssertIfHung();

                    mutex.ReleaseMutex();

                    task.WaitAndAssertIfHung();
                }
            }
        }

        #endregion

        #region With Break Handle Cases

        //[Test]
        //public void VerifyWaitOneWithBreakHandles_FailureDueToTimeout()
        //{
        //    var name = $"Global\\{nameof(VerifyWaitOneWithBreakHandles_FailureDueToTimeout)}";
        //    bool createdNew;
        //    using (var mutex = CreateMutex(true, name, out createdNew))
        //    {
        //        Assert.IsTrue(createdNew);

        //        Task.Run(() =>
        //        {
        //            using (var otherHandle = new ManualResetEvent(false))
        //            {
        //                bool wasCreated2;
        //                using (var mutex2 = CreateMutex(true, name, out wasCreated2))
        //                {
        //                    Assert.IsFalse(wasCreated2);

        //                    var resultOwned = mutex2.WaitOne(otherHandle, 1);

        //                    Assert.IsFalse(resultOwned);
        //                }
        //            }
        //        }).Wait();
        //    }
        //}

        //[Test]
        //public void VerifyWaitOneWithBreakHandles_BreakIsSetFirst()
        //{
        //    var name = $"Global\\{nameof(VerifyWaitOneWithBreakHandles_BreakIsSetFirst)}";
        //    bool createdNew;
        //    using (var mutex = CreateMutex(true, name, out createdNew))
        //    {
        //        Assert.IsTrue(createdNew);

        //        Task.Run(() =>
        //        {
        //            using (var otherHandle = new ManualResetEvent(true))
        //            {
        //                bool wasCreated2;
        //                using (var mutex2 = CreateMutex(true, name, out wasCreated2))
        //                {
        //                    Assert.IsFalse(wasCreated2);


        //                    var resultOwned = mutex2.WaitOne(otherHandle);

        //                    // We won't get ownership because the mutex is already owned.
        //                    Assert.IsFalse(resultOwned);
        //                }
        //            }
        //        }).Wait();
        //    }
        //}

        #endregion

        #region Helpers

        private static void DisposeOfMutexsAsCleanlyAsPossible(params ISaferMutexMutex[] mutexCollection)
        {
            Exception exceptionDuringCleanup = null;
            foreach (var mutex in mutexCollection)
            {
                try
                {
                    if (mutex != null)
                        mutex.Dispose();
                }
                catch (Exception ex)
                {
                    if (exceptionDuringCleanup == null)
                        exceptionDuringCleanup = ex;
                }
            }

            if (exceptionDuringCleanup != null)
                throw exceptionDuringCleanup;
        }

        #endregion

        private CreatedMutexContainer CreateMutex(bool initiallyOwned, string name)
        {
            bool owned;
            bool createdNew;
            var mutex = CreateMutexImplementation(initiallyOwned, name, out owned, out createdNew);
            return new CreatedMutexContainer { Mutex = mutex, Owned = owned, CreatedNew = createdNew, CreateFunc = CreateMutex, Name = name};
        }

        private ISaferMutexMutex CreateMutex(bool initiallyOwned, string name, out bool owned)
        {
            bool createdNew;
            return CreateMutex(initiallyOwned, name, out owned, out createdNew);
        }

        private ISaferMutexMutex CreateMutex(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            return CreateMutexImplementation(initiallyOwned, name, out owned, out createdNew);
        }

        protected abstract ISaferMutexMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned,  out bool createdNew);

        //protected abstract ISaferMutexMutex EnterImplementation(string name);
    }
}
