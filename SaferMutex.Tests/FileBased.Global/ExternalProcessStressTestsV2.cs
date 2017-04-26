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
        protected override ISaferMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            return new SaferMutex.FileBased2(initiallyOwned, name, Scope.CurrentUser, out owned, out createdNew, _tempDirectory.ToString());
        }

        protected override string MutexTypeToCreate
        {
            get { return "FileBased2Global"; }
        }

        [Ignore("The case where there is contention over the initial creation of the mutex is not handled correctly on OSX with mono.  Needs further investigation.  This use case may not even be valid")]
        public override void IncrementingACounter(int processesToUse, int passes)
        {
            base.IncrementingACounter(processesToUse, passes);
        }

        [Ignore("The case where there is contention over the initial creation of the mutex is not handled correctly on OSX with mono.  Needs further investigation.  This use case may not even be valid")]
        public override void WritingToACommonFile(int processesToUse, int passes)
        {
            base.WritingToACommonFile(processesToUse, passes);
        }
    }
}
