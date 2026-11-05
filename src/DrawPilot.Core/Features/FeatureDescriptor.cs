using System;

namespace DrawPilot.Core.Features
{
    public sealed class FeatureDescriptor
    {
        public FeatureDescriptor(
            string id,
            string title,
            string description,
            FeatureCategory category,
            bool isAvailable)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("功能 ID 不能为空。", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("功能标题不能为空。", nameof(title));
            }

            Id = id;
            Title = title;
            Description = description ?? string.Empty;
            Category = category;
            IsAvailable = isAvailable;
        }

        public string Id { get; }

        public string Title { get; }

        public string Description { get; }

        public FeatureCategory Category { get; }

        public bool IsAvailable { get; }

        public string CategoryLabel
        {
            get
            {
                switch (Category)
                {
                    case FeatureCategory.Document:
                        return "文档";
                    case FeatureCategory.Batch:
                        return "批量处理";
                    case FeatureCategory.Prepress:
                        return "印前 / 制版";
                    case FeatureCategory.Drawing:
                        return "绘图 / 排版";
                    case FeatureCategory.Panels:
                        return "面板 / 对象";
                    default:
                        return Category.ToString();
                }
            }
        }

        public string AvailabilityLabel
        {
            get { return IsAvailable ? "可用" : "规划中"; }
        }
    }
}
