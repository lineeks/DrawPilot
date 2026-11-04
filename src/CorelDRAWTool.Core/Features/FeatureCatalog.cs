using System;
using System.Collections.Generic;
using System.Linq;

namespace CorelDRAWTool.Core.Features
{
    /// <summary>
    /// 功能清单。新增功能时先在这里登记，再在 Host 中实现对应视图。
    /// </summary>
    public static class FeatureCatalog
    {
        public const string DocumentInfoId = "doc-info";

        private static readonly FeatureDescriptor[] Items =
        {
            new FeatureDescriptor(
                DocumentInfoId,
                "文档信息",
                "查看当前 CorelDRAW 文档、页面、图层与选中对象概况。",
                FeatureCategory.Document,
                true),

            new FeatureDescriptor("batch-export", "批量导出", "选择一个文件夹，把其中的 .cdr 批量导出为 PDF、PNG 或 TIFF。", FeatureCategory.Batch, true),
            new FeatureDescriptor("batch-convert", "批量转换 / 转曲", "批量转换文件格式、转曲与统一处理。", FeatureCategory.Batch, false),
            new FeatureDescriptor("batch-rename", "批量重命名", "按规则重命名文档、页面或导出文件。", FeatureCategory.Batch, false),
            new FeatureDescriptor("batch-preflight", "批量预检", "批量检查字体、颜色、出血与图片分辨率。", FeatureCategory.Batch, false),

            new FeatureDescriptor("prepress-bleed", "出血检查", "检查页面与对象出血是否满足印刷要求。", FeatureCategory.Prepress, false),
            new FeatureDescriptor("prepress-cutline", "刀线生成", "根据对象边界生成刀线 / 模切线。", FeatureCategory.Prepress, false),
            new FeatureDescriptor("prepress-imposition", "拼版辅助", "按纸张与成品尺寸生成拼版。", FeatureCategory.Prepress, false),
            new FeatureDescriptor("prepress-dimension", "尺寸标注", "为对象或页面添加尺寸标注。", FeatureCategory.Prepress, false),

            new FeatureDescriptor("draw-align", "对齐与分布", "批量对齐、分布、等间距排列对象。", FeatureCategory.Drawing, false),
            new FeatureDescriptor("draw-path", "沿路径排列", "让对象沿曲线路径排列。", FeatureCategory.Drawing, false),
            new FeatureDescriptor("draw-parametric", "参数化图形", "按参数生成常见图形与版式。", FeatureCategory.Drawing, false),
            new FeatureDescriptor("draw-text", "文字辅助", "批量处理文字、替换与排版。", FeatureCategory.Drawing, false),

            new FeatureDescriptor("panel-objects", "对象浏览器", "浏览、定位和选择文档中的对象。", FeatureCategory.Panels, false),
            new FeatureDescriptor("panel-layers", "图层管理", "集中管理页面与图层结构。", FeatureCategory.Panels, false),
            new FeatureDescriptor("panel-assets", "素材面板", "常用素材、色板与预设的集中入口。", FeatureCategory.Panels, false)
        };

        public static IReadOnlyList<FeatureDescriptor> All
        {
            get { return Items; }
        }

        public static FeatureDescriptor Default
        {
            get { return Find(DocumentInfoId); }
        }

        public static FeatureDescriptor Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return Items.FirstOrDefault(
                item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
