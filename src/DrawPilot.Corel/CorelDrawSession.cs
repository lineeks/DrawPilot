using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using DrawPilot.Core.Batch;
using DrawPilot.Core.Models;
using Corel.Interop.VGCore;

namespace DrawPilot.Corel
{
    /// <summary>
    /// 通过 COM 附加到正在运行的 CorelDRAW 实例。
    /// 所有 Corel 互操作类型都限制在本类内部。
    /// </summary>
    public sealed class CorelDrawSession : ICorelDrawSession
    {
        private const string ProgId = "CorelDRAW.Application";
        private const int AttachRetryMilliseconds = 200;

        private IVGApplication _application;

        private CorelDrawSession(IVGApplication application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        /// <summary>
        /// 在超时时间内反复尝试附加到已运行的 CorelDRAW。
        /// Host 通常由 GMS 宏启动，CorelDRAW 一定处于运行状态。
        /// </summary>
        public static CorelDrawSession Attach(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            Exception lastError = null;

            while (DateTime.UtcNow <= deadline)
            {
                try
                {
                    var application = AttachToRunningInstance();
                    if (application != null)
                    {
                        return new CorelDrawSession(application);
                    }
                }
                catch (CorelDrawException)
                {
                    throw;
                }
                catch (COMException ex)
                {
                    lastError = ex;
                }
                catch (InvalidCastException ex)
                {
                    lastError = ex;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                Thread.Sleep(AttachRetryMilliseconds);
            }

            throw new CorelDrawException(
                "未找到正在运行的 CorelDRAW 实例。请先启动 CorelDRAW，再重试。",
                lastError);
        }

        /// <summary>
        /// CorelDRAW 正常启动时不会把 Application 注册到 ROT，GetActiveObject 会返回
        /// MK_E_UNAVAILABLE；但用 ProgID 激活本地服务器时，CorelDRAW 会转发到已运行实例。
        /// 先尝试 ROT（兼容以 /Automation 启动的场景），再回退到 COM 激活。
        /// </summary>
        private static IVGApplication AttachToRunningInstance()
        {
            object registered = null;
            try
            {
                registered = Marshal.GetActiveObject(ProgId);
            }
            catch (COMException)
            {
                // 未注册到 ROT，继续尝试 COM 激活。
            }

            if (registered != null)
            {
                var application = registered as IVGApplication;
                if (application != null)
                {
                    return application;
                }

                string version = DescribeRawComObject(registered);
                ReleaseComObject(registered);
                throw CreateInteropMismatchException(version);
            }

            if (!IsCorelDrawProcessRunning())
            {
                return null;
            }

            Type comType = Type.GetTypeFromProgID(ProgId);
            if (comType == null)
            {
                return null;
            }

            object instance = Activator.CreateInstance(comType);
            var typed = instance as IVGApplication;
            if (typed == null)
            {
                string version = DescribeRawComObject(instance);
                ReleaseComObject(instance);
                throw CreateInteropMismatchException(version);
            }

            return typed;
        }

        private static CorelDrawException CreateInteropMismatchException(string version)
        {
            string suffix = string.IsNullOrWhiteSpace(version)
                ? string.Empty
                : "（版本 " + version + "）";

            return new CorelDrawException(
                "已找到正在运行的 CorelDRAW" + suffix +
                "，但当前 Corel.Interop.VGCore 程序集无法与该版本通信。" +
                "请确认版本在支持范围内（" + CorelDrawVersionPolicy.SupportedRangeLabel + "），" +
                "或使用与该版本匹配的互操作程序集重新构建。");
        }

        private static string DescribeRawComObject(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            try
            {
                object version = instance.GetType().InvokeMember(
                    "Version",
                    BindingFlags.GetProperty,
                    null,
                    instance,
                    null);

                return Convert.ToString(version, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ReleaseComObject(object instance)
        {
            if (instance != null && Marshal.IsComObject(instance))
            {
                Marshal.ReleaseComObject(instance);
            }
        }

        private static bool IsCorelDrawProcessRunning()
        {
            try
            {
                return Process.GetProcessesByName("CorelDRW").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public ApplicationSummary GetApplicationSummary()
        {
            EnsureConnected();

            try
            {
                return new ApplicationSummary(
                    _application.Version,
                    _application.VersionMajor,
                    _application.VersionMinor,
                    _application.ProgramPath);
            }
            catch (COMException ex)
            {
                throw new CorelDrawException("读取 CorelDRAW 版本信息失败。", ex);
            }
        }

        public DocumentSummary GetActiveDocumentSummary()
        {
            EnsureConnected();

            IVGDocument document;
            try
            {
                document = _application.ActiveDocument as IVGDocument;
            }
            catch (COMException)
            {
                return DocumentSummary.None;
            }

            if (document == null)
            {
                return DocumentSummary.None;
            }

            int pageCount = 0;
            try
            {
                var pages = document.Pages as IVGPages;
                if (pages != null)
                {
                    pageCount = pages.Count;
                }
            }
            catch (COMException)
            {
            }

            int layerCount = 0;
            try
            {
                var page = document.ActivePage as IVGPage;
                if (page != null)
                {
                    var layers = page.Layers as IVGLayers;
                    if (layers != null)
                    {
                        layerCount = layers.Count;
                    }
                }
            }
            catch (COMException)
            {
            }

            int selectionCount = 0;
            try
            {
                var selection = _application.ActiveSelectionRange as IVGShapeRange;
                if (selection != null)
                {
                    selectionCount = selection.Count;
                }
            }
            catch (COMException)
            {
            }

            string name = null;
            try
            {
                name = document.Name;
            }
            catch (COMException)
            {
            }

            string fullFileName = null;
            try
            {
                fullFileName = document.FullFileName;
            }
            catch (COMException)
            {
            }

            return new DocumentSummary(
                true,
                name,
                fullFileName,
                pageCount,
                layerCount,
                selectionCount);
        }

        public void CreateTestRectangle()
        {
            EnsureConnected();

            try
            {
                var document = _application.ActiveDocument as IVGDocument;
                if (document == null)
                {
                    throw new CorelDrawException("请先在 CorelDRAW 中打开一个文档。");
                }

                var page = document.ActivePage as IVGPage;

                IVGLayer layer = null;
                try
                {
                    layer = document.ActiveLayer as IVGLayer;
                }
                catch (COMException)
                {
                }

                if (layer == null && page != null)
                {
                    layer = page.ActiveLayer as IVGLayer;
                }

                if (layer == null)
                {
                    throw new CorelDrawException("当前文档没有可用的图层。");
                }

                double width = 100.0;
                double height = 100.0;
                if (page != null)
                {
                    try
                    {
                        width = page.SizeWidth;
                        height = page.SizeHeight;
                    }
                    catch (COMException)
                    {
                    }
                }

                if (width <= 0)
                {
                    width = 100.0;
                }

                if (height <= 0)
                {
                    height = 100.0;
                }

                double size = Math.Min(width, height) * 0.25;
                double left = (width - size) / 2.0;
                double top = (height - size) / 2.0;

                var shape = layer.CreateRectangle(
                    left,
                    top,
                    left + size,
                    top + size,
                    0,
                    0,
                    0,
                    0) as IVGShape;

                if (shape == null)
                {
                    throw new CorelDrawException("CorelDRAW 没有返回新创建的图形。");
                }

                shape.Name = "DrawPilot 测试矩形";
            }
            catch (CorelDrawException)
            {
                throw;
            }
            catch (COMException ex)
            {
                throw new CorelDrawException("创建测试矩形失败。", ex);
            }
        }

        public DocumentExportResult ExportDocument(BatchExportItem item, BatchExportRequest request)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            EnsureConnected();

            IVGDocument document = null;
            try
            {
                StructOpenOptions openOptions = _application.CreateStructOpenOptions();
                document = _application.OpenDocumentAsCopy(item.SourcePath, openOptions) as IVGDocument;
                if (document == null)
                {
                    throw new CorelDrawException("无法打开文档副本：" + item.SourcePath);
                }

                try
                {
                    document.Activate();
                }
                catch (COMException)
                {
                }

                int pageCount = GetPageCount(document);
                var outputPaths = new List<string>();
                var warnings = new List<string>();

                if (request.Format == BatchExportFormat.Pdf)
                {
                    ExportToPdf(document, item, request, outputPaths);
                }
                else
                {
                    ExportToImages(document, item, request, outputPaths, warnings);
                }

                document.Dirty = false;

                if (outputPaths.Count == 0)
                {
                    throw new CorelDrawException(
                        warnings.Count > 0 ? warnings[0] : "没有生成任何输出文件。");
                }

                return new DocumentExportResult(outputPaths, warnings, pageCount);
            }
            catch (CorelDrawException)
            {
                throw;
            }
            catch (COMException ex)
            {
                throw new CorelDrawException("导出失败：" + item.SourcePath, ex);
            }
            finally
            {
                if (document != null)
                {
                    try
                    {
                        document.Dirty = false;
                        document.Close();
                    }
                    catch (COMException)
                    {
                    }

                    if (Marshal.IsComObject(document))
                    {
                        Marshal.ReleaseComObject(document);
                    }
                }
            }
        }

        private static int GetPageCount(IVGDocument document)
        {
            try
            {
                var pages = document.Pages as IVGPages;
                return pages != null ? pages.Count : 0;
            }
            catch (COMException)
            {
                return 0;
            }
        }

        private static void ExportToPdf(
            IVGDocument document,
            BatchExportItem item,
            BatchExportRequest request,
            ICollection<string> outputPaths)
        {
            var pdfSettings = document.PDFSettings as IPDFVBASettings;
            if (pdfSettings != null)
            {
                pdfSettings.PublishRange = request.PageRange == BatchExportPageRange.CurrentPage
                    ? pdfExportRange.pdfCurrentPage
                    : pdfExportRange.pdfWholeDocument;
            }
            else if (request.PageRange == BatchExportPageRange.CurrentPage)
            {
                throw new CorelDrawException("当前 CorelDRAW 版本不支持 PDF 当前页导出。");
            }

            if (item.OverwritesExisting && File.Exists(item.OutputPath))
            {
                File.Delete(item.OutputPath);
            }

            document.PublishToPDF(item.OutputPath);
            outputPaths.Add(item.OutputPath);
        }

        private void ExportToImages(
            IVGDocument document,
            BatchExportItem item,
            BatchExportRequest request,
            ICollection<string> outputPaths,
            ICollection<string> warnings)
        {
            string outputFolder = Path.GetDirectoryName(item.OutputPath);
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new CorelDrawException("图片输出路径无效：" + item.OutputPath);
            }

            Directory.CreateDirectory(outputFolder);

            if (item.OverwritesExisting)
            {
                DeleteExistingImageOutputs(item.SourcePath, outputFolder, request);
            }

            cdrFilter filter = request.Format == BatchExportFormat.Png
                ? cdrFilter.cdrPNG
                : cdrFilter.cdrTIFF;

            cdrCompressionType compression = request.Format == BatchExportFormat.Tiff
                ? cdrCompressionType.cdrCompressionLZW
                : cdrCompressionType.cdrCompressionNone;

            if (request.PageRange == BatchExportPageRange.CurrentPage)
            {
                var page = document.ActivePage as IVGPage;
                if (page == null)
                {
                    throw new CorelDrawException("当前文档没有活动页面。");
                }

                try
                {
                    ExportImagePage(document, page, item.OutputPath, filter, compression, request.ImageDpi);
                    outputPaths.Add(item.OutputPath);
                }
                catch (COMException ex)
                {
                    throw new CorelDrawException(
                        "导出当前页失败（页面可能为空或没有可导出内容）：" + item.SourcePath,
                        ex);
                }
                finally
                {
                    if (Marshal.IsComObject(page))
                    {
                        Marshal.ReleaseComObject(page);
                    }
                }

                return;
            }

            var pages = document.Pages as IVGPages;
            if (pages == null)
            {
                throw new CorelDrawException("无法读取文档页面集合。");
            }

            for (int i = 1; i <= pages.Count; i++)
            {
                IVGPage page = null;
                try
                {
                    page = pages[i] as IVGPage;
                    if (page == null)
                    {
                        warnings.Add(string.Format("第 {0} 页不可用，已跳过。", i));
                        continue;
                    }

                    page.Activate();
                    string outputPath = BatchExportNaming.GetOutputPath(
                        item.SourcePath,
                        outputFolder,
                        request.Format,
                        request.PageRange,
                        i);

                    ExportImagePage(document, page, outputPath, filter, compression, request.ImageDpi);
                    outputPaths.Add(outputPath);
                }
                catch (COMException ex)
                {
                    warnings.Add(string.Format("第 {0} 页导出失败：{1}", i, ex.Message));
                }
                finally
                {
                    if (page != null && Marshal.IsComObject(page))
                    {
                        Marshal.ReleaseComObject(page);
                    }
                }
            }
        }

        private static void DeleteExistingImageOutputs(
            string sourcePath,
            string outputFolder,
            BatchExportRequest request)
        {
            string pattern = BatchExportNaming.GetSearchPattern(
                sourcePath,
                request.Format,
                request.PageRange);

            foreach (string path in Directory.EnumerateFiles(
                outputFolder,
                pattern,
                SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private void ExportImagePage(
            IVGDocument document,
            IVGPage page,
            string outputPath,
            cdrFilter filter,
            cdrCompressionType compression,
            int imageDpi)
        {
            double width = page.SizeWidth;
            double height = page.SizeHeight;
            Rect exportArea = _application.CreateRect(0, 0, width, height);
            StructPaletteOptions palette = _application.CreateStructPaletteOptions();

            ExportFilter exportFilter = document.ExportBitmap(
                outputPath,
                filter,
                cdrExportRange.cdrCurrentPage,
                cdrImageType.cdrRGBColorImage,
                0,
                0,
                imageDpi,
                imageDpi,
                cdrAntiAliasingType.cdrNormalAntiAliasing,
                false,
                false,
                true,
                false,
                compression,
                palette,
                exportArea);

            if (exportFilter == null)
            {
                throw new CorelDrawException("CorelDRAW 没有返回图片导出过滤器。");
            }

            try
            {
                var filterClass = exportFilter as ExportFilterClass;
                if (filterClass == null)
                {
                    throw new CorelDrawException("无法完成图片导出：不支持的导出过滤器类型。");
                }

                filterClass.Finish();
            }
            finally
            {
                if (Marshal.IsComObject(exportFilter))
                {
                    Marshal.ReleaseComObject(exportFilter);
                }
            }
        }

        private void EnsureConnected()
        {
            if (_application == null)
            {
                throw new ObjectDisposedException(nameof(CorelDrawSession));
            }
        }

        public void Dispose()
        {
            IVGApplication application = _application;
            _application = null;

            if (application != null && Marshal.IsComObject(application))
            {
                Marshal.ReleaseComObject(application);
            }
        }
    }
}
