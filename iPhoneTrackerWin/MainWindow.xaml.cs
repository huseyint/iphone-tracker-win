namespace iPhoneTrackerWin
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Data;
    using System.Data.SQLite;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Windows;
    using System.Windows.Navigation;
    using Microsoft.Win32;
    using mshtml;

    public partial class MainWindow : INotifyPropertyChanged
    {
        private const double ToUnixOffset = 31 * 365.25 * 24 * 60 * 60;

        private const double Precision = 100;

        private const string CellLocationQuery = "SELECT Timestamp, Latitude, Longitude FROM CellLocation";

        private const string WiFiLocationQuery = "SELECT Timestamp, Latitude, Longitude FROM WifiLocation";

        private const string WhereQuery = " WHERE Latitude != 0 || Longitude != 0";

        private readonly CultureInfo englishCultureInfo;

        private readonly DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        private readonly LogWindow logWindow; 
        
        private IHTMLScriptElement scriptElement;

        private DatabaseFile selectedBackup;

        private bool loadWiFi;

        public MainWindow()
        {
            InitializeComponent();

            this.Backups = new ObservableCollection<DatabaseFile>();

            var attributes = Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(AssemblyFileVersionAttribute), true);

            if (attributes.Length == 1)
            {
                var version = ((AssemblyFileVersionAttribute)attributes[0]).Version;

                this.Title = string.Format("iPhoneTrackerWin v" + version);
            }

            this.englishCultureInfo = CultureInfo.CreateSpecificCulture("en-US");

            this.logWindow = new LogWindow();

            this.Log(this.Title + " started");

            this.Closing += new CancelEventHandler(this.MainWindow_Closing);

            this.DataContext = this;

            this.Browser.LoadCompleted += this.Browser_LoadCompleted;

            this.Browser.NavigateToString(this.ReadHtml());
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<DatabaseFile> Backups { get; set; }

        public DatabaseFile SelectedBackup
        {
            get
            {
                return this.selectedBackup;
            }

            set
            {
                this.selectedBackup = value;

                this.LoadSelectedBackup();

                this.NotifyOfPropertyChanged("SelectedBackup");
            }
        }

        public bool LoadWiFi
        {
            get
            {
                return this.loadWiFi;
            }

            set
            {
                this.loadWiFi = value;

                this.NotifyOfPropertyChanged("LoadWiFi");

                this.LoadSelectedBackup();
            }
        }

        public void Log(object o)
        {
            if (this.logWindow == null || o == null)
            {
                return;
            }

            this.logWindow.LogTextBox.AppendText(o + Environment.NewLine);
            this.logWindow.LogTextBox.ScrollToEnd();
        }

        protected virtual void NotifyOfPropertyChanged(string property)
        {
            var handler = this.PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(property));
            }
        }

        private void LoadBackups()
        {
            this.Log("LoadBackups enter");

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var backupDir = Path.Combine(appData, @"Apple Computer\MobileSync\Backup");

            if (Directory.Exists(backupDir))
            {
                this.Log("Backup directory exists. Enumerating backups...");

                foreach (var dir in Directory.GetDirectories(backupDir))
                {
                    this.Log("Checking backup directory " + dir);

                    MBFileRecord[] records = null;

                    try
                    {
                        var mbdbParseLog = string.Empty;

                        records = MbdbReader.ReadMBDB(dir, false, false, out mbdbParseLog);
                        this.Log("MBDB parsed, log: " + mbdbParseLog);
                    }
                    catch (Exception ex)
                    {
                        this.Log("An exception occured while parsing MBDB: " + ex);
                    }

                    if (records == null)
                    {
                        this.Log("No record found for this backup!");
                    }
                    else
                    {
                        this.Log("Found record count: " + records.Length);

                        var recordKey = records
                            .Where(r => string.Equals(r.Domain, "RootDomain", StringComparison.OrdinalIgnoreCase) && string.Equals(r.Path, "Library/Caches/locationd/consolidated.db", StringComparison.OrdinalIgnoreCase))
                            .Select(r => r.Key)
                            .FirstOrDefault();

                        if (recordKey == null)
                        {
                            this.Log("Domain = RootDomain and Path = Library/Caches/locationd/consolidated.db couldn't be found!");
                        }
                        else
                        {
                            var file = Path.Combine(dir, recordKey);

                            this.Log("Backup with location data found, adding to list: " + file);

                            this.Backups.Add(new BackupDatabaseFile(file, dir));
                        }
                    }
                }
            }
            else
            {
                this.Log("Backup directory does not exist, make sure you have iTunes installed and synchronized with an iPhone/iPad!");
            }

            this.Log("LoadBackups exit");
        }

        private string ReadHtml()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("iPhoneTrackerWin.Html.txt"))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();                
            }
        }

        private void Browser_LoadCompleted(object sender, NavigationEventArgs e)
        {
            this.Log("Browser_LoadCompleted enter");

            try
            {
                this.LoadBackups();
            }
            catch (Exception ex)
            {
                this.Log("An exception occured while loading backups: " + ex);
            }

            if (this.Backups != null)
            {
                this.SelectedBackup = this.Backups.FirstOrDefault();
            }

            this.Log("Browser_LoadCompleted exit");
        }

        private void LoadSelectedBackup()
        {
            this.Log("LoadSelectedBackup enter");

            if (this.SelectedBackup == null)
            {
                this.Log("SelectedBackup is null!");
            }
            else
            {
                this.Log("SelectedBackup is not null");

                var cvsString = default(string);

                try
                {
                    cvsString = this.GetCSVString(this.SelectedBackup.File);
                }
                catch (Exception e)
                {
                    this.Log(e);
                }

                this.Log("cvsString = " + cvsString);

                this.ExecuteJavaScript("storeLocationData('" + cvsString + "');");
            }

            this.Log("LoadSelectedBackup exit");
        }

        private void ExecuteJavaScript(string script)
        {
            this.Log("ExecuteJavaScript enter");

            if (this.scriptElement == null)
            {
                this.Log("scriptElement == null, creating");

                var doc = (HTMLDocument)Browser.Document;
                var head = doc.getElementsByTagName("head").Cast<HTMLHeadElement>().First();
                this.scriptElement = (IHTMLScriptElement)doc.createElement("script");
                head.appendChild((IHTMLDOMNode)this.scriptElement);
            }

            this.scriptElement.text = script;

            this.Log("ExecuteJavaScript exit");
        }

        private string GetCSVString(string backupPath)
        {
            this.Log("GetCSVString enter");

            var sb = new StringBuilder("lat,lon,value,time\\n");

            var buckets = new Dictionary<string, long>();

            try
            {
                using (var con = new SQLiteConnection(@"Data Source=" + backupPath + ";Version=3;"))
                using (var cmd = con.CreateCommand())
                {
                    this.Log("Opening DB connection");

                    con.Open();

                    if (con.State == ConnectionState.Open)
                    {
                        this.Log("Connection opened.");                        
                    }
                    else
                    {
                        this.Log("Connection couldn't be opened!");
                    }

                    var query = CellLocationQuery + WhereQuery;

                    if (this.LoadWiFi)
                    {
                        query += " UNION " + WiFiLocationQuery + WhereQuery;
                    }

                    query += " ORDER BY Timestamp";

                    cmd.CommandText = query;

                    this.Log("Executing query");

                    using (var reader = cmd.ExecuteReader())
                    {
                        this.Log("Query executed successfully.");

                        while (reader.Read())
                        {
                            var timestamp = this.GetSafeDouble(reader, 0);
                            var latitude = this.GetSafeDouble(reader, 1);
                            var longitude = this.GetSafeDouble(reader, 2);

                            if (timestamp == 0d || (latitude == 0d && longitude == 0d))
                            {
                                continue;
                            }

                            var date = this.ConvertFromUnixTimestamp(ToUnixOffset + timestamp);

                            var timeBucketString = date.ToString("yyyy-MM-dd");

                            var latitude_index = Math.Floor(latitude * Precision) / Precision;
                            var longitude_index = Math.Floor(longitude * Precision) / Precision;

                            var allKey = string.Format(this.englishCultureInfo, "{0},{1},All Time", latitude_index, longitude_index);
                            var timeKey = string.Format(this.englishCultureInfo, "{0},{1},{2}", latitude_index, longitude_index, timeBucketString);

                            this.IncrementBuckets(buckets, allKey);
                            this.IncrementBuckets(buckets, timeKey);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e);
            }

            foreach (var key in buckets.Keys)
            {
                var count = buckets[key];

                var parts = key.Split(',');

                var rowString = parts[0] + "," + parts[1] + "," + count + "," + parts[2] + "\\n";

                sb.Append(rowString);
            }

            this.Log("GetCSVString exit");

            return sb.ToString();
        }

        private double GetSafeDouble(SQLiteDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? 0d : reader.GetDouble(index);
        }

        private void IncrementBuckets(Dictionary<string, long> buckets, string key)
        {
            buckets[key] = buckets.ContainsKey(key) ? buckets[key] + 1 : 1;
        }

        private DateTime ConvertFromUnixTimestamp(double timestamp)
        {
            return this.origin.AddSeconds(timestamp);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(WebPage.NavigateUri.AbsoluteUri));
            e.Handled = true;
        }

        private void ShowLog_Click(object sender, RoutedEventArgs e)
        {
            this.logWindow.Show();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            this.logWindow.CloseForce();
        }

        private void OpenCustom_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog()
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                RestoreDirectory = true,
                Title = "Select Location DB File",
            };

            var result = dialog.ShowDialog(this);

            if (result.HasValue && result.Value)
            {
                CustomDatabaseFile databaseFile;

                var existing = this.Backups
                    .OfType<CustomDatabaseFile>()
                    .Where(f => string.Equals(f.File, dialog.FileName))
                    .FirstOrDefault();

                if (existing == null)
                {
                    databaseFile = new CustomDatabaseFile
                    {
                        File = dialog.FileName,
                    };

                    this.Backups.Add(databaseFile);
                }
                else
                {
                    databaseFile = existing;
                }

                this.SelectedBackup = databaseFile;
            }
        }

        private void Donate_Click(object sender, RoutedEventArgs e)
        {
            string donateUrl;

            if (Thread.CurrentThread.CurrentCulture.Parent != null && string.Equals(Thread.CurrentThread.CurrentCulture.Parent.IetfLanguageTag, "de"))
            {
                donateUrl = "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=YE8N3KHU7L434";                
            }
            else
            {
                donateUrl = "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=T2HACTLWYNW3N";
            }

            Process.Start(new ProcessStartInfo(donateUrl));
            e.Handled = true;
        }
    }
}