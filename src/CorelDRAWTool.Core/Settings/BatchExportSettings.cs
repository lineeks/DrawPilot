using System.Runtime.Serialization;
using CorelDRAWTool.Core.Models;

namespace CorelDRAWTool.Core.Settings
{
    [DataContract]
    public sealed class BatchExportSettings
    {
        public BatchExportSettings()
        {
            Format = BatchExportFormat.Pdf;
            PageRange = BatchExportPageRange.AllPages;
            ImageDpi = 300;
        }

        [DataMember]
        public string InputFolder { get; set; }

        [DataMember]
        public string OutputFolder { get; set; }

        [DataMember]
        public BatchExportFormat Format { get; set; }

        [DataMember]
        public BatchExportPageRange PageRange { get; set; }

        [DataMember]
        public int ImageDpi { get; set; }

        [DataMember]
        public bool OverwriteExisting { get; set; }
    }
}
