using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SaferMutex.Tests.BaseSuites;

namespace SaferMutex.Tests.FrameworkBased.Global
{
    [TestFixture]
    [Platform(Include = "Win")]
    public class ExternalProcessStressTests : BaseExternalProcessStressTests
    {
        protected override ISaferMutexMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            throw new NotSupportedException();
        }

        protected override string MutexTypeToCreate
        {
            get { return "FrameworkBasedGlobal"; }
        }
    }
}
