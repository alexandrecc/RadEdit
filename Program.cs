namespace RadEdit
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
                RadEditDebugLog.Write("UNHANDLED_UI_EXCEPTION " + e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                RadEditDebugLog.Write("UNHANDLED_APPDOMAIN_EXCEPTION IsTerminating=" + e.IsTerminating + " Exception=" + e.ExceptionObject);
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                RadEditDebugLog.Write("UNOBSERVED_TASK_EXCEPTION " + e.Exception);
                e.SetObserved();
            };
            RadEditDebugLog.Write("PROCESS_START Version=" + Application.ProductVersion + " ProcessId=" + Environment.ProcessId);
            Application.Run(new Form1());
            RadEditDebugLog.Write("PROCESS_EXIT ProcessId=" + Environment.ProcessId);
        }
    }
}
