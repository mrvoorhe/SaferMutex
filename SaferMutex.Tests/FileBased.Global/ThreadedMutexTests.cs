using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SaferMutex.Tests.BaseSuites;
using NiceIO;
using NUnit.Framework;

namespace SaferMutex.Tests.FileBased.Global
{
    [TestFixture]
    public class ThreadedMutexTests : BaseThreadedTests
    {
        protected override ISaferMutexMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            return new SaferMutex.FileBased(initiallyOwned, name, Scope.CurrentUser, out owned, out createdNew, _tempDirectory.ToString());
        }
    }
}
