using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SaferMutex.Tests.Utils;

namespace SaferMutex.Tests.BaseSuites
{
    public abstract class BaseTests
    {
        protected static void DisposeOfMutexsAsCleanlyAsPossible(params ISaferMutexMutex[] mutexCollection)
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


        protected CreatedMutexContainer CreateMutex(bool initiallyOwned, string name)
        {
            bool owned;
            bool createdNew;
            var mutex = CreateMutexImplementation(initiallyOwned, name, out owned, out createdNew);
            return new CreatedMutexContainer { Mutex = mutex, Owned = owned, CreatedNew = createdNew, CreateFunc = CreateMutex, Name = name };
        }

        protected ISaferMutexMutex CreateMutex(bool initiallyOwned, string name, out bool owned)
        {
            bool createdNew;
            return CreateMutex(initiallyOwned, name, out owned, out createdNew);
        }

        protected ISaferMutexMutex CreateMutex(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            return CreateMutexImplementation(initiallyOwned, name, out owned, out createdNew);
        }

        protected abstract ISaferMutexMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned, out bool createdNew);
    }
}
