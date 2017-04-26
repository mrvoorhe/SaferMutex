using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SaferMutex.Tests.BaseSuites;
using NUnit.Framework;

namespace SaferMutex.Tests.FrameworkBased
{
    [TestFixture]
    public class ThreadedMutexTests : BaseThreadedTests
    {
        protected override ISaferMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            return new SaferMutex.FrameworkMutexBased(initiallyOwned, name, Scope.CurrentProcess, out owned, out createdNew);
        }
    }
}
