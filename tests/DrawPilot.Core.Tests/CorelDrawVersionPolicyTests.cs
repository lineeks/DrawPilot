using DrawPilot.Core.Models;
using Xunit;

namespace DrawPilot.Core.Tests
{
    public sealed class CorelDrawVersionPolicyTests
    {
        [Theory]
        [InlineData(24)]
        [InlineData(25)]
        [InlineData(26)]
        [InlineData(27)]
        public void SupportedVersionsStartAt2022(int majorVersion)
        {
            Assert.True(CorelDrawVersionPolicy.IsSupported(majorVersion));
        }

        [Theory]
        [InlineData(23)]
        [InlineData(22)]
        [InlineData(0)]
        public void OlderVersionsAreNotSupported(int majorVersion)
        {
            Assert.False(CorelDrawVersionPolicy.IsSupported(majorVersion));
        }

        [Fact]
        public void UnsupportedMessageMentionsRangeAndActualVersion()
        {
            string message = CorelDrawVersionPolicy.DescribeUnsupported(23, 5);

            Assert.Contains("23.5", message);
            Assert.Contains(CorelDrawVersionPolicy.SupportedRangeLabel, message);
        }
    }
}
