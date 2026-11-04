using System.Collections.Generic;
using System.Linq;

namespace CorelDRAWTool.Core.Models
{
    public sealed class DocumentExportResult
    {
        public DocumentExportResult(
            IEnumerable<string> outputPaths,
            IEnumerable<string> warnings,
            int pageCount)
        {
            OutputPaths = (outputPaths ?? Enumerable.Empty<string>()).ToArray();
            Warnings = (warnings ?? Enumerable.Empty<string>()).ToArray();
            PageCount = pageCount;
        }

        public IReadOnlyList<string> OutputPaths { get; }

        public IReadOnlyList<string> Warnings { get; }

        public int PageCount { get; }
    }
}
