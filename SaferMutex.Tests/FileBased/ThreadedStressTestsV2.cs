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
    public class ThreadedStressTestsV2 : BaseThreadedStressTests
    {
        private static readonly NPath _tempDirectory = NPath.SystemTemp.Combine("SaferMutex");

        [SetUp]
        public void TestSetup()
        {
            _tempDirectory.DeleteContents();
        }

        protected override ISaferMutexMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            return new SaferMutex.FileBased(initiallyOwned, name, Scope.CurrentProcess, out owned, out createdNew, _tempDirectory.ToString());
        }
    }
}
