using System;
using SocialInteractions;

namespace SocialInteractions.Tests.TestHelpers
{
    public abstract class ApiTestBase : IDisposable
    {
        protected ApiTestBase()
        {
            SLog.Logger = new NullLogger();
        }

        public void Dispose()
        {
            SLog.Logger = null;
        }
    }
}
