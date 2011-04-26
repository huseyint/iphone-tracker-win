namespace iPhoneTrackerWin
{
    using System.Windows;
    using System.Windows.Threading;

    public partial class LogWindow
    {
        private bool closeForce;

        public LogWindow()
        {
            InitializeComponent();

            this.Closing += new System.ComponentModel.CancelEventHandler(this.LogWindow_Closing);
        }

        public void CloseForce()
        {
            this.closeForce = true;
            this.Close();
        }

        private void LogWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.closeForce)
            {
                return;
            }

            DispatcherOperationCallback callback = delegate
            {
                this.Hide();
                return null;
            };

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, callback, null);
            
            e.Cancel = true;
        }
    }
}