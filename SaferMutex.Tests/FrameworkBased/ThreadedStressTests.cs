using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SaferMutex.Tests.BaseSuites;

namespace SaferMutex.Tests.FrameworkBased
{
    [TestFixture]
    public class ThreadedStressTests : BaseThreadedStressTests
    {
        protected override ISaferMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            return new SaferMutex.FrameworkMutexBased(initiallyOwned, name, Scope.CurrentProcess, out owned, out createdNew);
        }
    }
}
