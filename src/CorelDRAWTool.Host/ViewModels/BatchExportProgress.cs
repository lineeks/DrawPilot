namespace CorelDRAWTool.Host.ViewModels
{
    public enum BatchExportProgressKind
    {
        Started,
        Succeeded,
        Failed,
        Completed,
        Canceled,
        Fatal
    }

    public sealed class BatchExportProgress
    {
        private BatchExportProgress(
            BatchExportProgressKind kind,
            int itemIndex,
            int completedCount,
            int total,
            string fileName,
            string message)
        {
            Kind = kind;
            ItemIndex = itemIndex;
            CompletedCount = completedCount;
            Total = total;
            FileName = fileName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public BatchExportProgressKind Kind { get; }

        public int ItemIndex { get; }

        public int CompletedCount { get; }

        public int Total { get; }

        public string FileName { get; }

        public string Message { get; }

        public static BatchExportProgress Started(
            int itemIndex,
            int completedCount,
            int total,
            string fileName)
        {
            return new BatchExportProgress(
                BatchExportProgressKind.Started,
                itemIndex,
                completedCount,
                total,
                fileName,
                null);
        }

        public static BatchExportProgress Succeeded(
            int itemIndex,
            int completedCount,
            int total,
            string fileName)
        {
            return new BatchExportProgress(
                BatchExportProgressKind.Succeeded,
                itemIndex,
                completedCount,
                total,
                fileName,
                null);
        }

        public static BatchExportProgress Failed(
            int itemIndex,
            int completedCount,
            int total,
            string fileName,
            string message)
        {
            return new BatchExportProgress(
                BatchExportProgressKind.Failed,
                itemIndex,
                completedCount,
                total,
                fileName,
                message);
        }

        public static BatchExportProgress Completed(int completedCount, int total)
        {
            return new BatchExportProgress(
                BatchExportProgressKind.Completed,
                -1,
                completedCount,
                total,
                null,
                null);
        }

        public static BatchExportProgress Canceled(int completedCount, int total)
        {
            return new BatchExportProgress(
                BatchExportProgressKind.Canceled,
                -1,
                completedCount,
                total,
                null,
                null);
        }

        public static BatchExportProgress Fatal(string message, int completedCount, int total)
        {
            return new BatchExportProgress(
                BatchExportProgressKind.Fatal,
                -1,
                completedCount,
                total,
                null,
                message);
        }
    }
}
