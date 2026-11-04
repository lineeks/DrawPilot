using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using CorelDRAWTool.Core.Models;
using Corel.Interop.VGCore;

namespace CorelDRAWTool.Corel
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
            try
            {
                var registered = Marshal.GetActiveObject(ProgId) as IVGApplication;
                if (registered != null)
                {
                    return registered;
                }
            }
            catch (COMException)
            {
                // 未注册到 ROT，继续尝试 COM 激活。
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

            return Activator.CreateInstance(comType) as IVGApplication;
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

                shape.Name = "CorelDRAWTool 测试矩形";
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

        public void ExportDocumentToPdf(string sourceFile, string outputFile)
        {
            if (string.IsNullOrWhiteSpace(sourceFile))
            {
                throw new ArgumentException("源文件路径不能为空。", nameof(sourceFile));
            }

            if (string.IsNullOrWhiteSpace(outputFile))
            {
                throw new ArgumentException("输出文件路径不能为空。", nameof(outputFile));
            }

            EnsureConnected();

            IVGDocument document = null;
            try
            {
                StructOpenOptions openOptions = _application.CreateStructOpenOptions();
                document = _application.OpenDocumentAsCopy(sourceFile, openOptions) as IVGDocument;
                if (document == null)
                {
                    throw new CorelDrawException("无法打开文档副本：" + sourceFile);
                }

                document.PublishToPDF(outputFile);
                document.Dirty = false;
            }
            catch (CorelDrawException)
            {
                throw;
            }
            catch (COMException ex)
            {
                throw new CorelDrawException("导出 PDF 失败：" + sourceFile, ex);
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
