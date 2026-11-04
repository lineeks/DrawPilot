using System.Collections.Generic;
using System.Linq;

namespace CorelDRAWTool.Core.Models
{
    public sealed class BatchExportPlan
    {
        public BatchExportPlan(
            IEnumerable<BatchExportItem> items,
            IEnumerable<string> skippedSourcePaths)
        {
            Items = (items ?? Enumerable.Empty<BatchExportItem>()).ToArray();
            SkippedSourcePaths = (skippedSourcePaths ?? Enumerable.Empty<string>()).ToArray();
        }

        public IReadOnlyList<BatchExportItem> Items { get; }

        public IReadOnlyList<string> SkippedSourcePaths { get; }

        public int SourceFileCount
        {
            get { return Items.Count + SkippedSourcePaths.Count; }
        }

        public int SkippedCount
        {
            get { return SkippedSourcePaths.Count; }
        }

        public bool HasWork
        {
            get { return Items.Count > 0; }
        }
    }
}
