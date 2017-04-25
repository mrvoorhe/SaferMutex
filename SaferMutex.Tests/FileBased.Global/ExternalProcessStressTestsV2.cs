using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SaferMutex.Tests.BaseSuites;

namespace SaferMutex.Tests.FileBased.Global
{
    [TestFixture]
    public class ExternalProcessStressTestsV2 : BaseExternalProcessStressTests
    {
        protected override ISaferMutexMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            return new SaferMutex.FileBased2(initiallyOwned, name, Scope.CurrentUser, out owned, out createdNew, _tempDirectory.ToString());
        }

        protected override string MutexTypeToCreate
        {
            get { return "FileBased2Global"; }
        }
    }
}
