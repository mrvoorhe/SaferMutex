using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SaferMutex.Tests.Utils
{
    public class CreatedMutexContainer : ISaferMutexMutex
    {
        public ISaferMutexMutex Mutex;
        public bool Owned;
        public bool CreatedNew;
        public UtilsAndExtensions.CreateMutexFunc CreateFunc;
        public string Name;

        public void Dispose()
        {
            Mutex.Dispose();
        }

        public void WaitOne()
        {
            Mutex.WaitOne();
        }

        public bool WaitOne(int millisecondsTimeout)
        {
            return Mutex.WaitOne(millisecondsTimeout);
        }

        public bool WaitOne(WaitHandle breakHandle)
        {
            return Mutex.WaitOne(breakHandle);
        }

        public bool WaitOne(WaitHandle breakHandle, int millisecondsTimeout)
        {
            return Mutex.WaitOne(breakHandle, millisecondsTimeout);
        }

        public bool WaitOne(WaitHandle[] breakHandles, out int index)
        {
            return Mutex.WaitOne(breakHandles, out index);
        }

        public bool WaitOne(WaitHandle[] breakHandles, int millisecondsTimeout, out int index)
        {
            return Mutex.WaitOne(breakHandles, millisecondsTimeout, out index);
        }

        public void ReleaseMutex()
        {
            Mutex.ReleaseMutex();
        }
    }

    public static class UtilsAndExtensions
    {
        public delegate ISaferMutexMutex CreateMutexFunc(bool initiallyOwned, string name, out bool owned);

        public const int AvoidHangTimeout = 30000;

        public static void WaitAndAssertIfHung(this WaitHandle handle)
        {
            if (!handle.WaitOne(AvoidHangTimeout))
                Assert.Fail("Timed out to avoid a hung test");
        }

        public static void WaitAndAssertIfHung(this Task task)
        {
            if (!task.Wait(AvoidHangTimeout))
                Assert.Fail("Timed out to avoid a hung test");
        }

        public static void WaitAndAssertIfHung(this Background thread)
        {
            if (!thread.Wait(AvoidHangTimeout))
                Assert.Fail("Timed out to avoid a hung test");
        }

        public static void AssertOwned(this CreatedMutexContainer creationContainer)
        {
            Assert.IsTrue(creationContainer.Owned, "The mutex was expected to have gained ownership at creation time");
        }

        public static void AssertNotOwned(this CreatedMutexContainer creationContainer)
        {
            Assert.IsFalse(creationContainer.Owned, "The mutex was expected NOT to have gained ownership at creation time");
        }

        public static void AssertCreatedNew(this CreatedMutexContainer creationContainer)
        {
            Assert.IsTrue(creationContainer.CreatedNew, "The mutex was expected to have been newly created at the time if it's creation");
        }

        public static void AssertNotCreatedNew(this CreatedMutexContainer creationContainer)
        {
            Assert.IsFalse(creationContainer.CreatedNew, "The mutex was expected NOT to have been newly created at the time of it's creation");
        }

        public static void AssertOwnershipCanBeObtainedByDifferentThread(this CreatedMutexContainer creationContainer, string additionalMessage = null)
        {
            AssertOwnershipCanBeObtained(creationContainer.Name, creationContainer.CreateFunc, additionalMessage);
        }

        public static void AssertOwnershipCannotBeObtainedByDifferentThread(this CreatedMutexContainer creationContainer, string additionalMessage = null)
        {
            AssertOwnershipCannotBeObtained(creationContainer.Name, creationContainer.CreateFunc, additionalMessage);
        }

        public static void AssertOwnershipCanBeObtained(string name, CreateMutexFunc createMutexFunc, string additionalMessage = null)
        {
            var msg = "Expected to be able to obtain ownership, but could not.";
            if (!string.IsNullOrEmpty(additionalMessage))
                msg = $"{msg}  {additionalMessage}";

            Assert.IsTrue(CanGrabOwnershipOnDifferentThread(name, createMutexFunc), msg);
        }

        public static void AssertOwnershipCannotBeObtained(string name, CreateMutexFunc createMutexFunc, string additionalMessage = null)
        {
            var msg = "Expected to be unable to obtain ownership, but could.";
            if (!string.IsNullOrEmpty(additionalMessage))
                msg = $"{msg}  {additionalMessage}";

            Assert.IsFalse(CanGrabOwnershipOnDifferentThread(name, createMutexFunc), msg);
        }

        public static bool CanGrabOwnershipOnDifferentThread(string name, CreateMutexFunc createMutexFunc)
        {
            return RunOnDifferentThread(() =>
            {
                bool owned;
                using (var mutex = createMutexFunc(true, name, out owned))
                {
                    //mutex.ReleaseMutex();
                    return owned;
                }
            });

        }

        public static T RunOnDifferentThread<T>(Func<T> func)
        {
            T result = default(T);
            var thread = new Thread(() =>
            {
                result = func();
            });
            thread.Start();
            thread.Join();
            return result;
        }
    }
}
