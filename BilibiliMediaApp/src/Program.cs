using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace BilibiliMediaApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                Run(args);
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex);
                MessageBox.Show(
                    "应用启动失败：\n\n" + ex.Message,
                    "B站跟视频助手",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void Run(string[] args)
        {
            string dataDirectory = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--data-dir=", StringComparison.OrdinalIgnoreCase))
                {
                    dataDirectory = args[i].Substring("--data-dir=".Length);
                }
            }

            if (string.IsNullOrEmpty(dataDirectory))
            {
                dataDirectory = AppSettings.ResolveDefaultDataDirectory();
            }
            AppSettings.SetDataDirectory(dataDirectory);

            bool createdNew;
            using (var mutex = new Mutex(true, "BilibiliMediaApp_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "B站跟视频助手已经在运行，请查看任务栏。",
                        "B站跟视频助手",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    if (!NativeMethods.SetProcessDpiAwarenessContext(
                        NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                    {
                        NativeMethods.SetProcessDPIAware();
                    }
                }
                catch
                {
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    WriteErrorLog(e.Exception);
                };

                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    WriteErrorLog(e.ExceptionObject as Exception);
                };

                Application.Run(new MainForm(args));
            }
        }

        private static void WriteErrorLog(Exception exception)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.DataDirectory);
                File.AppendAllText(
                    Path.Combine(AppSettings.DataDirectory, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " +
                    (exception == null ? "unknown error" : exception.ToString()) +
                    Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
