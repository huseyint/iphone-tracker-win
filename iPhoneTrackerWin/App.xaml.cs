namespace iPhoneTrackerWin
{
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Windows;
    using System.Windows.Markup;
    using System.Windows.Threading;

    public partial class App : Application
    {
        public App()
        {
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(Thread.CurrentThread.CurrentCulture.IetfLanguageTag)));

            AppDomain.CurrentDomain.AssemblyResolve += this.CurrentDomain_AssemblyResolve;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var resourceName = "iPhoneTrackerWin." + new AssemblyName(args.Name).Name + ".dll";

            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

            if (stream != null)
            {
                using (stream)
                {
                    var assemblyData = new byte[stream.Length];

                    stream.Read(assemblyData, 0, assemblyData.Length);

                    return Assembly.Load(assemblyData);
                }
            }

            return null;
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("An unhandled exception occured: " + e.Exception, "Unhandled Exception");
        }
    }
}