using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NiceIO;
using NUnit.Framework;
using SaferMutex.Tests.BaseSuites;

namespace SaferMutex.Tests.FileBased
{
    [TestFixture]
    public class ThreadedMutexTestsV2 : BaseThreadedTests
    {
        protected override ISaferMutexMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            return new SaferMutex.FileBased2(initiallyOwned, name, Scope.CurrentProcess, out owned, out createdNew, _tempDirectory.ToString());
        }
    }
}
