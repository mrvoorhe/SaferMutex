﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SaferMutex.Tests.BaseSuites;
using NUnit.Framework;

namespace SaferMutex.Tests.FrameworkBased.Global
{
    [TestFixture]
    [Platform(Include = "Win")]
    public class ThreadedMutexTets : BaseThreadedTests
    {
        protected override ISaferMutex CreateMutexImplementation(bool initiallyOwned, string name, out bool owned, out bool createdNew)
        {
            return new SaferMutex.FrameworkMutexBased(initiallyOwned, name, Scope.CurrentUser, out owned, out createdNew);
        }
    }
}
