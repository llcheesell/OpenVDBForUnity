using NUnit.Framework;

namespace OpenVDB.Tests
{
    public class OpenVDBImporterTest
    {
        [SetUp]
        public void SetUp()
        {
            OpenVDBAPI.oiInitialize();
        }

        [TearDown]
        public void TearDown()
        {
            OpenVDBAPI.oiUninitialize();
        }
    }
}
