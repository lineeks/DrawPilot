using System;

namespace DrawPilot.Core.Models
{
    /// <summary>
    /// 当前活动文档的只读快照。由 CorelDRAW 适配层填充，UI 只负责展示。
    /// </summary>
    public sealed class DocumentSummary
    {
        public static readonly DocumentSummary None =
            new DocumentSummary(false, null, null, 0, 0, 0);

        public DocumentSummary(
            bool hasDocument,
            string name,
            string fullFileName,
            int pageCount,
            int layerCount,
            int selectionCount)
        {
            HasDocument = hasDocument;
            Name = name;
            FullFileName = fullFileName;
            PageCount = Math.Max(0, pageCount);
            LayerCount = Math.Max(0, layerCount);
            SelectionCount = Math.Max(0, selectionCount);
        }

        public bool HasDocument { get; }

        public string Name { get; }

        public string FullFileName { get; }

        public int PageCount { get; }

        public int LayerCount { get; }

        public int SelectionCount { get; }

        public string DisplayName
        {
            get
            {
                if (!HasDocument)
                {
                    return "未打开文档";
                }

                return string.IsNullOrWhiteSpace(Name) ? "(未命名文档)" : Name;
            }
        }

        public string FileLocation
        {
            get
            {
                return string.IsNullOrWhiteSpace(FullFileName) ? "(尚未保存)" : FullFileName;
            }
        }
    }
}
