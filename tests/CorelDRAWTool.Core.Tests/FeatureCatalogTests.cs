using System;
using System.Linq;
using CorelDRAWTool.Core.Features;
using Xunit;

namespace CorelDRAWTool.Core.Tests
{
    public sealed class FeatureCatalogTests
    {
        [Fact]
        public void AllIdsAreUnique()
        {
            var duplicateIds = FeatureCatalog.All
                .GroupBy(feature => feature.Id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.Empty(duplicateIds);
        }

        [Fact]
        public void DocumentInfoIsAvailable()
        {
            var feature = FeatureCatalog.Find(FeatureCatalog.DocumentInfoId);

            Assert.NotNull(feature);
            Assert.True(feature.IsAvailable);
            Assert.Equal("文档信息", feature.Title);
        }

        [Fact]
        public void BatchExportIsAvailable()
        {
            var feature = FeatureCatalog.Find("batch-export");

            Assert.NotNull(feature);
            Assert.True(feature.IsAvailable);
        }

        [Fact]
        public void FindIsCaseInsensitive()
        {
            Assert.Same(FeatureCatalog.Default, FeatureCatalog.Find("DOC-INFO"));
        }

        [Fact]
        public void FindReturnsNullForUnknownId()
        {
            Assert.Null(FeatureCatalog.Find("does-not-exist"));
            Assert.Null(FeatureCatalog.Find(null));
        }

        [Fact]
        public void CatalogCoversAllCategories()
        {
            foreach (FeatureCategory category in Enum.GetValues(typeof(FeatureCategory)))
            {
                Assert.Contains(FeatureCatalog.All, feature => feature.Category == category);
            }
        }
    }
}
