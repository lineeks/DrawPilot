using DrawPilot.Core.Models;
using Xunit;

namespace DrawPilot.Core.Tests
{
    public sealed class DocumentSummaryTests
    {
        [Fact]
        public void NoneIsNotADocument()
        {
            Assert.False(DocumentSummary.None.HasDocument);
            Assert.Equal("未打开文档", DocumentSummary.None.DisplayName);
            Assert.Equal("(尚未保存)", DocumentSummary.None.FileLocation);
        }

        [Fact]
        public void NegativeCountsAreClamped()
        {
            var summary = new DocumentSummary(true, "Doc1", @"C:\work\Doc1.cdr", -1, -2, -3);

            Assert.Equal(0, summary.PageCount);
            Assert.Equal(0, summary.LayerCount);
            Assert.Equal(0, summary.SelectionCount);
        }

        [Fact]
        public void DisplayNameFallsBackToUntitled()
        {
            var summary = new DocumentSummary(true, "  ", null, 1, 1, 0);

            Assert.Equal("(未命名文档)", summary.DisplayName);
            Assert.Equal("(尚未保存)", summary.FileLocation);
        }
    }
}
