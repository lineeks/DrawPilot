using System;
using CorelDRAWTool.Core.Models;

namespace CorelDRAWTool.Corel
{
    /// <summary>
    /// Host 与 CorelDRAW 之间的边界。除这里之外，其他项目不应直接引用 Corel 互操作类型。
    /// </summary>
    public interface ICorelDrawSession : IDisposable
    {
        ApplicationSummary GetApplicationSummary();

        DocumentSummary GetActiveDocumentSummary();

        void CreateTestRectangle();

        DocumentExportResult ExportDocument(BatchExportItem item, BatchExportRequest request);
    }
}
