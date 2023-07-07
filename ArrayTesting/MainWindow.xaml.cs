using System;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MicroControlLED_API;
using System.ComponentModel; // CancelEventArgs
//using OxyPlot;
using System.IO;
using System.Diagnostics;
using LiveCharts;
using LiveCharts.Defaults;
using System.Collections.Generic;
using System.Windows.Data;
using System.Globalization;
using LiveCharts.Wpf;
using System.Timers;
using System.Linq;
using System.Media;
using System.Windows.Media.Imaging;
using Enterwell.Clients.Wpf.Notifications;
using System.Reflection;
using System.Windows.Threading;

namespace ArrayTesting
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SMILEUSBDevice sMILEUSBDevice;
        public static bool ArrayConnectStatus, arrayIsPulsing = false;
        private IntegratingSphere integratingSphere;
        bool[,] EmptyFrame;
        bool[,] FullFrame;
        int arraySize = 8;
        ExportPathGenerator expPathGen = new ExportPathGenerator();
        CancellationTokenSource cts = new CancellationTokenSource();
        private static System.Timers.Timer bTimer;
        public INotificationMessageManager Manager { get; } = new NotificationMessageManager();

        //public ChartValues<HeatPoint> Values { get; set; }
        public static SeriesCollection SerCol { get; set; }
        public ScatterSeries MyScatter { get; set; }
        public string[] xAxis { get; set; }
        public string[] yAxis { get; set; }
        public Func<double, string> YFormatter { get; set; }

        public bool DEVICES_READY => ArrayConnectStatus && !(integratingSphere is null) && integratingSphere.IsConnected();

        const string folderName = "calib";

        static string currentLoc = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        static Dictionary<string, string> fileNames = new Dictionary<string, string>
            {
                { "calibration", "ILT1810343U1INS250N.txt" },
                { "reference", "Ref1000avg150msPortZu.txt" }
            };

        static Dictionary<string, string> filePaths = new Dictionary<string, string>
            {
                { "calibration", $@"{currentLoc}\{folderName}\{fileNames["calibration"]}" },
                { "reference", $@"{currentLoc}\{folderName}\{fileNames["reference"]}" }
            };

        public MainWindow()
        {
            InitializeComponent();

            Initialize();
        }

        private void InitGraph()
        {
            this.Dispatcher.Invoke(() =>
            {
                HeatSeries.Values = new ChartValues<HeatPoint> { };

                MyScatter = new ScatterSeries();

                SerCol = new SeriesCollection();
                SerCol.Add(MyScatter);

                xAxis = new string[arraySize];
                yAxis = new string[arraySize];
                for (int i = 0; i < arraySize; i++)
                {
                    xAxis[i] = i.ToString();
                    yAxis[i] = (arraySize-i-1).ToString();
                }
                YFormatter = val => Math.Round(val,1).ToString();

                HeatmapX.Labels = xAxis;
                HeatmapY.Labels = yAxis;

                HeatmapX.MaxValue = arraySize;
                HeatmapY.MaxValue = arraySize;
            
                DataContext = this;
            });
        }

       

        private async void Initialize()
        {
            BackgroundWorkerStart();

            SMILEUSBDevice.deviceInitializedFired += deviceInitializedEventHandler;

            int voltage = (int)Voltage_Slider.Value;
            Task InitArrayTask = Task.Run(() => InitArray(voltage));
            Task InitSpecTask = Task.Run(() => InitSpecAsync());

            // do SLOW work here...

            bTimer = new System.Timers.Timer
            {
                Interval = 1000
            };
            bTimer.Elapsed += OnTimedEvent2;

            string[] exportLocations = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + @"\ExportLocations.txt");
            for (int i = 0; i < exportLocations.Length; i++)
            {
                exportLocations[i] = Environment.ExpandEnvironmentVariables(exportLocations[i]);
            }
            ExportComboBox.ItemsSource = exportLocations;
            ExportComboBox.SelectedIndex = 0;
            UpdateExportPath();

            await InitArrayTask;
            UpdateArrayText();

            await InitSpecTask;
            UpdateSpecText();
        }

        private async void OnTimedEvent2(object sender, ElapsedEventArgs e)
        {
            if (integratingSphere != null && integratingSphere.IsConnected())
            {
                bool b = false;
                this.Dispatcher.Invoke(() =>
                {
                    b = (bool)Durchsatzkorrektur.IsChecked;
                });

                //Console.WriteLine("Times evt 2 fired.");

                double[][] data = await ScanAndGetAbsData(b);

                PlotData(data[0], data[1]);
            }
            
        }


        async void UpdateSpecText()
        {

            if (integratingSphere != null) // is null during init
            {
                if (integratingSphere.IsConnected())
                {
                    //this.Dispatcher.Invoke(() =>
                    //{
                    
                    if (AutoScanSpecCheckBox.IsChecked ?? false)
                    {
                        await DoDarkScan();
                    }
                    IntTimeText.Text = integratingSphere.GetIntTime().ToString();
                    AvgValueText.Text = integratingSphere.GetAvg().ToString();
                    //});
                }
                UpdateSpecInfo();
            }
        }

        void UpdateSpecInfo()
        {

            Action invokeNotifification = () =>
            {

                if (integratingSphere != null)
                {
                    // connection status
                    if (integratingSphere.IsConnected())
                    {
                        TextSpecStatus.Text = "Verbunden";
                        TextSpecStatus.Foreground = Brushes.Green;
                    }
                    else
                    {
                        Durchsatzkorrektur.IsChecked = false;
                        Durchsatzkorrektur.IsEnabled = false;
                        CreateReference.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFFBDBD"));
                        CreateReference.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0000"));

                        TextSpecStatus.Text = "Getrennt";
                        TextSpecStatus.Foreground = Brushes.Red;
                    }

                    UpdateDarkScanStatus();

                }
            };
            Application.Current.Dispatcher.BeginInvoke(invokeNotifification);
         
        }

        private async Task InitSpecAsync()
        {
            Task t = Task.Run(() =>
            {
                integratingSphere = new IntegratingSphere(this);
            });

            string[] config = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + @"\config.ini");



            //for (int i = 0; i < config.Length; i++)
            //{
            //    if(!config[i].Contains("\\") && i < config.Length - 1)
            //    {
            //        if (config[i].ToLower().Contains("alib")) // AKA Kalibration | calibration
            //        {
            //            filePaths["calibration"] = config[i + 1];
            //        }
            //        if (config[i].ToLower().Contains("ref")) // AKA Referenz | reference
            //        {
            //            filePaths["reference"] = config[i + 1];
            //        }
            //    }
            //}

            await t;

            integratingSphere.LoadCalibrationFile(filePaths["calibration"]);
            integratingSphere.LoadReferenceFile(filePaths["reference"]);

            IInputElement focusedControl = null;

            this.Dispatcher.Invoke(() =>
            {
                focusedControl = FocusManager.GetFocusedElement(this);
                Console.WriteLine("REFERENCE 1");
                CalFilename.Text = Path.GetFileNameWithoutExtension(filePaths["calibration"]);
                CalFilename.Foreground = Brushes.Green;
                CalibCombo.Text = filePaths["calibration"];
                CalibCombo.Focus();
                CalibCombo.Select(CalibCombo.Text.Length, 0);
                Console.WriteLine("REFERENCE 2");
            });
            await Task.Delay(50);
            Console.WriteLine("REFERENCE 3");
            this.Dispatcher.Invoke(() =>
            {
                Console.WriteLine("REFERENCE 4");
                RefFile.Text = Path.GetFileNameWithoutExtension(filePaths["reference"]);
                RefFile.Foreground = Brushes.Green;
                RefScanBox.Text = filePaths["reference"];
                RefScanBox.Focus();
                RefScanBox.Select(CalibCombo.Text.Length, 0);
                Console.WriteLine("REFERENCE 5");
            });
            await Task.Delay(50);
            this.Dispatcher.Invoke(() =>
            {
                Console.WriteLine("REFERENCE 6");
                if (focusedControl != null)
                    focusedControl.Focus();
            });
        }
        
        private async void ChangeMapping(object sender, RoutedEventArgs e)
        {
            ChangeMapping();
        }


        public void ChangeMapping()
        {
            bool[,] frame = (bool[,])EmptyFrame.Clone();

            //for (int k = 0; k < sMILEUSBDevice.ColCount; k++)
            //    for (int j = 0; j < sMILEUSBDevice.RowCount; j++)
            //        frame[j, k] = true;                

            for (int k = 0; k < sMILEUSBDevice.ColCount; k++)
                frame[k, 0] = true;

            sMILEUSBDevice.SwitchPixelMapping(true);
            sMILEUSBDevice.SendFrame(frame);
        }

        private async void PulseLEDArray()
        {
            await Task.Run(() =>
            {
                sMILEUSBDevice.SwitchPixelMapping(true);
            });

            if (!arrayIsPulsing)
            {
                arrayIsPulsing = true;
                //for (int i = 0; i < 180 * 6; i += 2)
                //{
                //    sMILEUSBDevice.SetVoltage(1300 + (uint)(2000 * Math.Pow(Math.Abs(Math.Cos((double)i * Math.PI / 180)), 0.2)));
                //    await Task.Delay(10);
                //}
                //sMILEUSBDevice.SetVoltage(3300);

                // turn all pixel on
                bool[,] frame = (bool[,])EmptyFrame.Clone();

                //for (int k = 0; k < sMILEUSBDevice.ColCount; k++)
                //    for (int j = 0; j < sMILEUSBDevice.RowCount; j++)
                //        frame[j, k] = true;                

                for (int k = 0; k < sMILEUSBDevice.ColCount; k++)
                        frame[k, 0] = true;

                await Task.Run(() =>
                {
                    sMILEUSBDevice.SendFrame(frame);
                });

                arrayIsPulsing = false;
            }
        }

        private async void AlternatingLines(object sender, RoutedEventArgs e)
        {
            sMILEUSBDevice.SwitchPixelMapping(false);
            if (!arrayIsPulsing)
            {
                arrayIsPulsing = true;

                bool[,] frameEven = (bool[,])EmptyFrame.Clone();
                bool[,] frameOdd = (bool[,])EmptyFrame.Clone();

                for (int i = 0; i < sMILEUSBDevice.RowCount; i++)
                    for (int j = 0; j < sMILEUSBDevice.ColCount; j++)
                        if (i % 2 == 0)
                            frameEven[j, i] = true;
                        else
                            frameOdd[j, i] = true;

                for (int k = 0; k < 10; k++)
                {
                    await Task.Run(() =>
                    {
                        sMILEUSBDevice.SendFrame(frameEven);
                    });

                    await Task.Delay(500);

                    await Task.Run(() =>
                    {
                        sMILEUSBDevice.SendFrame(frameOdd);
                    });

                    await Task.Delay(500);
                }

                arrayIsPulsing = false;
            }
        }
        private async void Snake(object sender, RoutedEventArgs e)
        {
            if (!arrayIsPulsing)
            {
                arrayIsPulsing = true;

                bool[,] frame = (bool[,])EmptyFrame.Clone();
                bool direction = true; // true for left to right, false for right to left

                for (int i = 0; i < sMILEUSBDevice.RowCount; i++)
                {
                    for (int j = 0; j < sMILEUSBDevice.ColCount; j++)
                    {
                        int col = direction ? j : sMILEUSBDevice.ColCount - 1 - j;
                        frame[i, col] = true;

                        await Task.Run(() =>
                        {
                            sMILEUSBDevice.SendFrame((bool[,])frame.Clone());
                        });

                        await Task.Delay(200); // delay to control speed

                        frame[i, col] = false;
                    }

                    direction = !direction;
                }

                arrayIsPulsing = false;
            }
        }


        private async void PulseLEDArrays()
        {
            if (!arrayIsPulsing)
            {
                arrayIsPulsing = true;
                for (int i = 0; i < 180 * 6; i += 2)
                {
                    sMILEUSBDevice.SetVoltage(1300 + (uint)(2000 * Math.Pow(Math.Abs(Math.Cos((double)i * Math.PI / 180)), 0.2)));
                    await Task.Delay(10);
                }
                sMILEUSBDevice.SetVoltage(3300);
                arrayIsPulsing = false;
            }
            
        }

        async void DataWindow_Closing(object sender, CancelEventArgs e)
        {
            string msg = "Wirklich schließen? Geräte werden getrennt.";
            MessageBoxResult result =
                MessageBox.Show(
                msg,
                "Geräte trennen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                // If user doesn't want to close, cancel prompt
                e.Cancel = true;
            }
            else
            {
                cts.Cancel();
                await DeviceShutdown();
            }
        }

        async Task DeviceShutdown()
        {
            await ArrayShutdown();
            await SpecShutdown();
        }

        async Task ArrayShutdown()
        {
            if (ArrayConnectStatus == true)
            {
                await Task.Factory.StartNew(() =>
                {
                    sMILEUSBDevice.SendFrame(EmptyFrame);
                    sMILEUSBDevice.Disconnect();
                    sMILEUSBDevice.Dispose();
                });
            }
            ArrayConnectStatus = false;
            TextArrayStatus.Text = "Getrennt";
            TextArrayStatus.Foreground = Brushes.Red;
            TextArrayType.Text = "";
        }

        async Task SpecShutdown()
        {
            if (integratingSphere != null && integratingSphere.IsConnected())
                await Task.Factory.StartNew(() => integratingSphere.DisconnectIntegratingSphere());
            UpdateSpecInfo();
            UpdateSpecText();
        }




        //         if (!spectrometerRunning)
        //            {
        //                spectrometerRunning = true;
        //                int counter = 0;
        //                while(spectrometerRunning)
        //                {
        //                    Task ScanTask = Task.Factory.StartNew(() => integratingSphere.SingleScan());
        //        await ScanTask;
        //        //await integratingSphere.SingleScan();
        //        connectButton.Content = (Math.Round(integratingSphere.GetScanDataAbsoluteAt(471) * 100) / 100).ToString() + " µW/nm/cm²";
        //                    counter++;
        //                    //await Task.Delay(50);
        //                    if (counter >= 10) spectrometerRunning = false;
        //                    ///connectButton.Content = GetNewScanDataAbsoluteAt(471).ToString() + " µW/nm/cm²";
        //                }
        //}
        //            else spectrometerRunning = false;

        private void Spannung_TextChanged(object sender, TextChangedEventArgs e)
        {
            
        }

        //private bool[] _modeArray = new bool[] { true, false, false };
        //public bool[] ModeArray
        //{
        //    get { return _modeArray; }
        //}

        //public int SelectedMode
        //{
        //    get { return Array.IndexOf(_modeArray, true); }
        //}

        private void Voltage_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //Console.WriteLine(Voltage_Slider.Value);
            
            uint voltage = (uint)(Voltage_Slider.Value);

            if (arraySize == 8 && VoltagePostfix != null) VoltagePostfix.Text = (voltage / 1000.0).ToString("N3") + "V";

            //Console.WriteLine(myInt);
            if (sMILEUSBDevice != null)
                sMILEUSBDevice.SetVoltage(_Voltage: voltage);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            //public bool IsUsbDeviceConnected(string pid, string vid)
            //{
                using (var searcher =
                  new ManagementObjectSearcher(@"Select * From Win32_USBControllerDevice"))
                {
                    using (var collection = searcher.Get())
                    {
                        foreach (var device in collection)
                        {
                            var usbDevice = Convert.ToString(device);
                        Console.WriteLine(usbDevice);
                            //if (usbDevice.Contains(pid) && usbDevice.Contains(vid))
                  //              return true;
                        }
                    }
                }
                //return false;
            //}
        }


        private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //bool connectTabEnabled = false;
            //this.Dispatcher.Invoke(() =>
            //{
            //    connectTabEnabled = connectTab.IsSelected;
            //});
            //if (connectTabEnabled)
            //{
            //    Task DeviceRemovalTask = Task.Factory.StartNew(() => DeviceRemovalListener());
            //    await DeviceRemovalTask;
            //}
        }

        

        private async Task PixelScan(int delay, bool useReference, CancellationToken t)
        {
            int lower = 400; 
            int upper = 550;
            int voltage = 0;
            int avg = 0;
            int intTime = 0;
            string serial = "";
            string ledSize = "";
            string ledpitch = "";
            string ledwl = "";
            // GET DARK MEASUREMENT CHARACTERISTICS
            List<string> darkSummary = new List<string> { };
            double DIEtotenschwelle = await DarkTests(darkSummary, lower, upper);

            this.Dispatcher.Invoke(() =>
            {
                RoutineProgress.Value = 0;
                lower = (int)ParseDoubleFromString(IntegrationLower.Text);
                upper = (int)ParseDoubleFromString(IntegrationUpper.Text);
                avg = integratingSphere.GetAvg();
                intTime = integratingSphere.GetIntTime();
                if (arraySize == 8)
                    voltage = (int)Voltage_Slider.Value;
                else if (Voltage33.IsChecked != null && (bool)Voltage33.IsChecked)
                    voltage = 3300;
                else if (Voltage50.IsChecked != null && (bool)Voltage50.IsChecked)
                    voltage = 5000;
                serial = SerialTextBox.Text;
                ledSize = LEDSizeBox.Text;
                ledpitch = PitchBox.Text;
                ledwl = WavelengthBox.Text;
                HeatSeries.Values.Clear();
                Output.Text = String.Join("\n", darkSummary);
            });

            int pixels = arraySize * arraySize;
            int measuredDataPoints = 0;
            double[] pixelX = new double[pixels];
            double[] pixelY = new double[pixels];
            double[] powerResults = new double[pixels];

            darkSummary.Insert(0, "====== TOTE PIXEL =======");


            List<HeatPoint> heatPointsBatch = new List<HeatPoint>();
            int updateInterval = arraySize * arraySize / 4; // update the UI every quarter of the full grid
            for (int i = 0; i < arraySize; i++) // i = y
            {
                for (int j = 0; j < arraySize; j++) // j = x
                {
                    await SetSinglePixel(j, i);
                    await Task.Delay(delay);

                    double[][] results = await ScanAndGetAbsData(useReference);
                    DataArrayToFile(results, expPathGen.GeneratePath("Spektrum Pixel(" + j + ", " + i + ")"));

                    // power calculation
                    double sum = Integrate(results[0], results[1], lower, upper);

                    powerResults[measuredDataPoints] = sum;
                    pixelX[measuredDataPoints] = j;
                    pixelY[measuredDataPoints] = i;
                    measuredDataPoints++;

                    // Save the current point's data for later heatmap update
                    heatPointsBatch.Add(new HeatPoint(j, arraySize - 1 - i, (int)sum));

                    // Only update the UI every updateInterval measurements
                    if (true || measuredDataPoints % updateInterval == 0)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            // Add all heat points from the batch to the heatmap
                            foreach (HeatPoint point in heatPointsBatch)
                            {
                                HeatSeries.Values.Add(point);
                            }
                            // Clear the batch
                            heatPointsBatch.Clear();

                            // Update the progress
                            RoutineProgress.Value = 100.0 * (double)measuredDataPoints / (double)pixels;
                        });
                    }

                    if (t.IsCancellationRequested) break;
                }
                if (t.IsCancellationRequested) break;
            }
            double[][] heatmap = new double[][] { (double[])pixelX, pixelY, powerResults };

            this.Dispatcher.Invoke(() =>
            {
                // Add all heat points from the batch to the heatmap
                foreach (HeatPoint point in heatPointsBatch)
                {
                    HeatSeries.Values.Add(point);
                }

                // Clear the batch
                heatPointsBatch.Clear();

                // Update the progress
                RoutineProgress.Value = 100.0 * (double)measuredDataPoints / (double)pixels;
                // Write heatmap file
                DataArrayToFile(heatmap, expPathGen.GeneratePath("B Heatmap"));
            });
 

            if (!t.IsCancellationRequested)
            {

                // AUSWERTUNG 1
                double powerMax = 0;
                double powerAliveCount = 0;
                int powerMaxIndex = -1;
                int deadPixelCount = 0;
                List<double> alivePixelPowers = new List<double> { };
                bool[,] alivePixelFrame = (bool[,])FullFrame.Clone();

                darkSummary.Add("-------------------------");

                string colNaming = String.Empty, colBarrier = String.Empty;
                for (int i = 0; i < arraySize; i++)
                {
                    if (i != 0)
                    {
                        colNaming += " ";
                        colBarrier += " ";
                    }

                    colNaming += Convert.ToString(i, 16).ToUpper();
                    colBarrier += "-";
                }

                string asciiMap = colNaming + "\n" + colBarrier + "\n";

                for (int i = 0; i < heatmap[2].Length; i++)
                {
                    
                    if (heatmap[2][i] < DIEtotenschwelle)
                    {
                        asciiMap += ".";
                        deadPixelCount++;
                        darkSummary.Add("Toter Pixel bei: (" + heatmap[0][i] + "," + heatmap[1][i] + ")");
                        alivePixelFrame[(int)heatmap[0][i], (int)heatmap[1][i]] = false;
                    }
                    else
                    {
                        asciiMap += "O";
                        alivePixelPowers.Add(heatmap[2][i]);
                        powerAliveCount += heatmap[2][i];

                        if (heatmap[2][i] > powerMax)
                        {
                            powerMaxIndex = i;
                            powerMax = heatmap[2][i];
                        }
                    }
                    // one line is complete, add newline
                    if ((i + 1) % arraySize == 0) asciiMap += " | " + Convert.ToString(i /arraySize, 16).ToUpper() + "\n";
                    else asciiMap += " ";
                }

                darkSummary.Add("-------------------------");
                darkSummary.Add(asciiMap);


                int alivePixelCount = arraySize * arraySize - deadPixelCount;
                double avgAlivePower = powerAliveCount / alivePixelCount;
                double stdDevAlivePower = StdDev(alivePixelPowers.ToArray(), empiric: false);


                // Full Array Scan + to file
                sMILEUSBDevice.SendFrame(FullFrame);
                await Task.Delay(delay); // WANN KOMMT EIN FRAME AN?!
                double[][] resultFull = await ScanAndGetAbsData(useReference);
                DataArrayToFile(resultFull, expPathGen.GeneratePath("C Spektrum Alle Pixel"));
                double sumFull = Integrate(resultFull[0], resultFull[1], lower, upper);

                // Alive Array Scan + to file
                sMILEUSBDevice.SendFrame(alivePixelFrame);
                await Task.Delay(delay); // WANN KOMMT EIN FRAME AN?!
                double[][] resultFullAlive = await ScanAndGetAbsData(useReference);
                DataArrayToFile(resultFull, expPathGen.GeneratePath("D Spektrum Alle lebendigen Pixel "));
                double sumFullAlive = Integrate(resultFullAlive[0], resultFullAlive[1], lower, upper);


                List<string> summary = new List<string> { };
                summary.Add("-------------------------");
                summary.Add("Automatisierte Vermessung von µLED-Arrays an der Ulbricht-Kugel");
                summary.Add("Seriennummer:\tSN " + serial);
                summary.Add("Array-Typ:\t" + arraySize + "x" + arraySize);
                summary.Add("LED-Größe:\t" + ledSize + " µm\tPitch:\t" + ledpitch + ((ledpitch == String.Empty) ? "" : " µm"));
                summary.Add("Wellenlänge:\t" + ledwl + " nm");
                summary.Add("=== AUFNAHMEPARAMETER ===");
                summary.Add("Integrationszeit:\t" + intTime + " ms");
                summary.Add("Mittelwertbildung:\t" + avg);
                summary.Add("Spannung:\t\t" + (voltage / 1000.0).ToString("N3") + " V");
                // summary.Add("Spannung:\t\t" + (Math.Round(((double)voltage) / 1000.0).ToString(),1) + " V");  BUGGED CODE RUNS!!!
                summary.Add("Spektrale Integration von " + lower + " nm bis " + upper + " nm");
                summary.Add("Leistungen in µW (MIKROWATT), spektral in µW/nm");

                double? ledSizeDoubleNull = ParseDoubleFromString(ledSize);
                double ledSizeDouble = 0;
                if (ledSizeDoubleNull != null)
                    ledSizeDouble = (double)ledSizeDoubleNull;


                try { 
                    summary.Add("====== AUSWERTUNG =======");
                    summary.Add("Lebendige Pixel:\t" + alivePixelCount);
                    summary.Add("Ausgefallene Pixel:\t" + deadPixelCount);
                    summary.Add("---- nur Leuchtpixel ----");
                    summary.Add("Durchschnittliche Leistung je Pixel:\t" + Math.Round(avgAlivePower, 3) + " ± " + Math.Round(stdDevAlivePower, 3) + " µW");
                    if(ledSizeDoubleNull != null)
                        summary.Add("Spezifische Ausstrahlung je Pixel:\t" + Math.Round(avgAlivePower/ledSizeDouble/ ledSizeDouble *1000, 3) + " nW/(µm)²");

                    summary.Add("Maximale Leistung bei Pixel (" + heatmap[0][powerMaxIndex] + "," + heatmap[1][powerMaxIndex] + "):\t" + Math.Round(heatmap[2][powerMaxIndex], 3) + " µW");
                    if (ledSizeDoubleNull != null)
                        summary.Add("Maximale Spezifische Ausstrahlung:\t" + Math.Round(heatmap[2][powerMaxIndex] / ledSizeDouble / ledSizeDouble * 1000, 3) + " nW/(µm)²");

                    summary.Add("---- Gesamtleistung -----");
                    summary.Add("Alle Pixel simultan:\t\t\t" + Math.Round(sumFull, 3) + " µW");
                    if (ledSizeDoubleNull != null)
                        summary.Add("Spezifische Ausstrahlung (unsicher):\t" + Math.Round(sumFull / ledSizeDouble / ledSizeDouble * 1000 / alivePixelCount, 3) + " nW/(µm)²");

                    summary.Add("Alle lebendigen simultan:\t\t" + Math.Round(sumFullAlive, 3) + " µW");
                    if (ledSizeDoubleNull != null)
                        summary.Add("Spezifische Ausstrahlung (unsicher):\t" + Math.Round(sumFullAlive / ledSizeDouble / ledSizeDouble * 1000 / alivePixelCount, 3) + " nW/(µm)²");


                    summary.AddRange(darkSummary);
                } catch(IndexOutOfRangeException e)
                {
                    summary.Add($"\n\n EXCEPTION MSG: {e} \n\n");
                }
                StringsToFile(summary, expPathGen.GeneratePath("A Zusammenfassung"));

                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Output.Text = String.Join("\n", summary);

                    int Height = (int)this.HeatmapGrid.ActualHeight;
                    int Width = (int)this.HeatmapGrid.ActualWidth;
                    double resolutionScale = 300 / 96;

                    RenderTargetBitmap bitmap = new RenderTargetBitmap((int)(resolutionScale * (Width + 1)),
                    (int)(resolutionScale * (Height + 1)),
                    resolutionScale * 96,
                    resolutionScale * 96, PixelFormats.Default);

                    bitmap.Render(this.HeatmapGrid);

                    string file = expPathGen.GeneratePath("B Heatmap", ".png");
                    string Extension = System.IO.Path.GetExtension(file).ToLower();

                    BitmapEncoder encoder;
                    if (Extension == ".gif")
                        encoder = new GifBitmapEncoder();
                    else if (Extension == ".png")
                        encoder = new PngBitmapEncoder();
                    else if (Extension == ".jpg")
                        encoder = new JpegBitmapEncoder();
                    else
                        return;

                    encoder.Frames.Add(BitmapFrame.Create(bitmap));

                    using (Stream stm = File.Create(file))
                    {
                        encoder.Save(stm);
                    }

                });
            }
        }

        private double Integrate(double[] X, double[] Y, double from, double to)
        {
            if(from >= 250 && to <= 1050)
            {
                double sum = 0;
                int index = 0;
                while (X[index] < from)
                    index++;
                while(X[index] < to)
                {
                    sum += Y[index] * (X[index + 1] - X[index - 1]) / 2;
                    index++;
                }
                return sum;
            }
            return 0;
        }

   
        private async Task DoDarkScan()
        {
            if (integratingSphere != null && integratingSphere.IsConnected())
            {
                if(ArrayOffAtDarkScanCheckBox.IsChecked ?? false) if (ArrayConnectStatus) sMILEUSBDevice.SendFrame(EmptyFrame);
                Task d = Task.Delay(20);
                await integratingSphere.DarkScan();
                await d;
                UpdateDarkScanStatus();
                
                if (ArrayOffAtDarkScanCheckBox.IsChecked ?? false) if (ArrayConnectStatus) sMILEUSBDevice.SendFrame(FullFrame);
            }
        }

        private async Task<double[][]> ScanAndGetAbsData(bool useRef)
        {
            if (integratingSphere != null && integratingSphere.IsConnected())
            {
                await integratingSphere.SingleScan();
                return new double[][] { integratingSphere.GetCalibratedAbsoluteDataX(), integratingSphere.GetCalibratedAbsoluteDataY(useRef) };
            }
            return null;
        }



        //private void DataArrayToFile(double[][] jaggedData, string filePath) { }

        private void DataArrayToFile(double[][] jaggedData, string filePath)
        {
            bool toFile = true;
            this.Dispatcher.Invoke(() =>
            {
                toFile = (bool)FileExportCheckBox.IsChecked;
            });

            if(toFile)
            {
                var builder = new StringBuilder();

                for (int i = 0; i < jaggedData[0].Length; i++)
                {
                    double[] line = new double[jaggedData.Length];
                    for (int j = 0; j < jaggedData.Length; j++)
                    {
                        line[j] = Math.Round(jaggedData[j][i], 3);
                    }

                    builder.AppendLine(String.Join("\t", line));
                }
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
                //Console.WriteLine(System.IO.Path.GetDirectoryName(filePath));

                File.WriteAllText(filePath, builder.ToString());
            }
            
        }

        private void StringsToFile(List<string> input, string filePath)
        {
            bool toFile = true;
            this.Dispatcher.Invoke(() =>
            {
                toFile = (bool)FileExportCheckBox.IsChecked;
            });

            if(toFile)
            {
                var builder = new StringBuilder();

                builder.AppendLine(String.Join("\n", input));

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, builder.ToString());
            }
        }

        //private void DataArrayToFile(string[][] jaggedData, string filePath)
        //{
        //    bool toFile = true;
        //    this.Dispatcher.Invoke(() =>
        //    {
        //        toFile = (bool)FileExportCheckBox.IsChecked;
        //    });

        //    if (toFile)
        //    {
        //        var builder = new StringBuilder();

        //        for (int i = 0; i < jaggedData[0].Length; i++)
        //        {
        //            string[] line = new string[jaggedData.Length];
        //            for (int j = 0; j < jaggedData.Length; j++)
        //            {
        //                line[j] = jaggedData[j][i];
        //            }

        //            builder.AppendLine(String.Join("\t", line));
        //        }
        //        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
        //        //Console.WriteLine(System.IO.Path.GetDirectoryName(filePath));

        //        File.WriteAllText(filePath, builder.ToString());
        //    }
        //}

        private void ExportComboBox_TextChanged(object sender, EventArgs e)
        {
            UpdateExportPath();
        }

        private void SerialTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateExportPath();
        }

        private void UpdateExportPath(String filedialogpath = null)
        {
            string voltageComment = String.Empty;
            if (VoltagePostfix.Text != null)
                voltageComment = VoltagePostfix.Text;
            string ser = String.Empty;
            if (SerialTextBox.Text != null)
                ser = SerialTextBox.Text;

            string sbPathShow = filedialogpath is null ? ExportComboBox.Text : filedialogpath;
          

            
            sbPathShow += (ser == String.Empty) ? String.Empty : (@"\SN " + ser);

            if (ser != null && ser != string.Empty && voltageComment != string.Empty) sbPathShow += " ";

            sbPathShow += (voltageComment == String.Empty) ? String.Empty : (voltageComment);

            sbPathShow += "\\";

            //remove multiple ''\''
            while(sbPathShow.Contains(@"\\"))
            {
                sbPathShow = sbPathShow.Replace(@"\\", @"\");
            }

            StringBuilder sbFileName = new StringBuilder();
            
            sbFileName.Append(" <MESSUNG>");
            sbFileName.Append((bool)TimeFilenameCheckBox.IsChecked ? " <ZEITSTEMPEL>" : String.Empty);
            sbFileName.Append(".txt");

            // remove leading spaces, also removes serial number whitespace
            while (sbFileName[0] == ' ')
            {
                sbFileName.Remove(0, 1);
            }

            expPathGen.folderPath = sbPathShow;
            expPathGen.fileNamePost = ".txt";
            expPathGen.timeStamp = (bool)TimeFilenameCheckBox.IsChecked;

            string filePath = sbPathShow + sbFileName.ToString();

            if (ExportComboBox != null) ExportPathText.Text = filePath; //ExportComboBox contains ExportPathText-Object
        }

        internal class ExportPathGenerator
        {
            public string folderPath = String.Empty, fileNamePost = String.Empty;
            public bool timeStamp = false;

            public ExportPathGenerator() { }

            public string GeneratePath(string input, string fileType = ".txt")
            {
                StringBuilder sbFileName = new StringBuilder();
                sbFileName.Append((input == String.Empty || input == null ? String.Empty : (" " + input)));
                sbFileName.Append( ((bool)timeStamp ? DateTime.Now.ToString(" yyyy-MM-dd HH-mm-ss") : "") + fileType);

                // remove leading spaces
                while (sbFileName[0] == ' ')
                {
                    sbFileName.Remove(0, 1);
                }

                return folderPath + sbFileName.ToString();
            }
        }

 

        private void VoltageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                int? millivolts = (int?) ParseDoubleFromString(VoltageTextBox.Text);
                if (millivolts != null)
                {
                    Voltage_Slider.Value = (int) millivolts;
                }
            }
        }

        private double? ParseDoubleFromString(string input)
        {
            double? result = null;
            try
            {
                result = Int32.Parse(input);
                Console.WriteLine(result);
            }
            catch (FormatException)
            {
                Console.WriteLine($"Unable to parse '{input}'");
            }            
            catch (System.OverflowException)
            {
                Console.WriteLine($"Unable to parse '{input}'");
            }
            return result;
        }

        private void TimeFilenameCheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateExportPath();
        }

        private void IntegrationTimeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                UpdateSpecConfig((int?)ParseDoubleFromString(IntegrationTimeTextBox.Text), (int?)ParseDoubleFromString(AveragingTextBox.Text));
            }
        }

        private void IntegrationTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IntegrationTimeTextBox is null || AveragingTextBox is null) return;

            UpdateSpecConfig((int?)ParseDoubleFromString(IntegrationTimeTextBox.Text), (int?)ParseDoubleFromString(AveragingTextBox.Text));
        }

        private void AveragingTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                UpdateSpecConfig((int?)ParseDoubleFromString(IntegrationTimeTextBox.Text), (int?)ParseDoubleFromString(AveragingTextBox.Text));
            }
        }

        private void AveragingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IntegrationTimeTextBox is null || AveragingTextBox is null) return;
            UpdateSpecConfig((int?)ParseDoubleFromString(IntegrationTimeTextBox.Text), (int?)ParseDoubleFromString(AveragingTextBox.Text));
        }

        private async Task UpdateSpecConfig(int? intTime, int? avgValue, bool forceDarkScan = false)
        {
            if (integratingSphere != null)
            {
                if (intTime != null)
                {
                    if (intTime < 1) intTime = 1; // DLL only works with intTime >= 1, 0 creates infinite absolute values
                    integratingSphere.SetIntTime((int)intTime);
                    IntTimeText.Text = intTime.ToString();
                }
                if (avgValue != null)
                {
                    integratingSphere.SetScanAvg((int)avgValue);
                    AvgValueText.Text = avgValue.ToString();
                }
                if (AutoScanSpecCheckBox.IsChecked ?? false || forceDarkScan)
                    await DoDarkScan();
                UpdateDarkScanStatus();
            }
        }

        private void UpdateDarkScanStatus()
        {
            if(integratingSphere != null)
            {
                if (integratingSphere.SpecHasDarkScan())
                { 
                    DarkSubButton.BorderBrush = Brushes.Green;
                    DarkSubButton.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFC0FFBD")); // "#FFC0FFBD"
                }
                else
                {
                    DarkSubButton.BorderBrush = Brushes.Red;
                    DarkSubButton.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFFBDBD")); // "#FFC0FFBD"
                }
            }
        }



        void PlotData(double[] dataX, double[] dataY, string title = "Spektrum")
        {

            ChartValues<ObservablePoint> cv = new ChartValues<ObservablePoint>();
            

            for (int i = 0; i < dataX.Length; i++)
                if (dataX[i] > 300 && dataX[i] < 600) // plot area
                    cv.Add(new ObservablePoint(Math.Round(dataX[i], 2), Math.Round(dataY[i], 2)));

            try { 
                Action invokeNotifification = () =>
                {

                    MyScatter.Title = title;
                    MyScatter.Values = cv;
                    if (SerCol is null)
                        SerCol = new SeriesCollection();
                    else
                    {
                        //SerCol.Clear();
                        SerCol.Add(MyScatter);
                    }
                    SpectrumPlot.Series = SerCol;

                    // Filter dataY values where corresponding dataX values are between 300 and 600
                    List<double> filteredDataY = new List<double>();
                    for (int i = 0; i < dataX.Length; i++)
                    {
                        if (dataX[i] >= 300 && dataX[i] <= 600)
                        {
                            filteredDataY.Add(dataY[i]);
                        }
                    }

                    // Now get the maximum from the filtered data
                    int maxValueIndex = Array.IndexOf(dataY, filteredDataY.Max());
                    double xValueAtMax = dataX[maxValueIndex];
                    TextBlockSpektrumMax.Text = $"max={xValueAtMax} nm";
                };
                Application.Current.Dispatcher.BeginInvoke(invokeNotifification);
            } catch(NullReferenceException ne)
            {
                Console.WriteLine("Error PlotData: " + ne);
            }
         }

        private void CalibCombo_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalibCombo.Foreground = Brushes.Black;
        }



        async Task<double> DarkTests(List<string> darkSummary, int lower, int upper)
        {
            if (!DEVICES_READY) return -1.0;

            // NOISE PROPERTIES
            // 16x dark scan + integration
            if (sMILEUSBDevice != null && ArrayConnectStatus) sMILEUSBDevice.SendFrame(EmptyFrame);

            int scans = 16;
            double[] darkSums = new double[scans];
            double[] darkSumsAbs = new double[scans];
            double darkAvg = 0;
            double darkAvgAbs = 0;
            double darkMax = 0;
            for (int i = 0; i < scans; i++)
            {
                double[][] results = await ScanAndGetAbsData(false);
                darkSums[i] = Integrate(results[0], results[1], lower, upper);
                darkAvg += darkSums[i];
                darkSumsAbs[i] = Math.Abs(darkSums[i]);
                darkAvgAbs += darkSumsAbs[i];
                
                // find maximum absolute value for dark noise (in dark corrected spectrum!)
                darkMax = darkSumsAbs[i] > darkMax ? darkSumsAbs[i] : darkMax;
            }
            darkAvg /= scans;
            darkAvgAbs /= scans;

            double stdDevDark = StdDev(darkSums, empiric: true);
            double stdDevDarkAbs = StdDev(darkSumsAbs, empiric: true);


            List<string> outstr = new List<string> { };

            outstr.Add(scans + " Dunkelscans ergaben:");
            outstr.Add("(spektrale Integration von " + lower + " nm bis " + upper + " nm)");
            outstr.Add("Maximale Leistung:\t\t\t" + Math.Round(darkMax, 3) + " µW");
            outstr.Add("Durchschnittliche Leistung:\t\t" + Math.Round(darkAvg, 3) + " ± " + Math.Round(stdDevDark, 3) + " µW");
            outstr.Add("Durchschnittliche abs. Leistung:\t" + Math.Round(darkAvgAbs, 3) + " ± " + Math.Round(stdDevDarkAbs, 3) + " µW");
            outstr.Add("Schwelle für tote Pixel (3σ):\t\t" + Math.Round(darkAvg + 3*stdDevDark, 3) + " µW");
            
            darkSummary.Add(String.Join("\n", outstr));

            return darkAvg + 3 * stdDevDark; // max value for dead px
        }

        private double StdDev(double[] values, bool empiric = false)
        {
            double[] valCopy = (double[])values.Clone();
            if (valCopy != null && valCopy.Length > 0)
            {
                double average = valCopy.Average();
                double sumOfSquaresOfDifferences = valCopy.Select(val => (val - average) * (val - average)).Sum();
                return !empiric ? Math.Sqrt(sumOfSquaresOfDifferences / valCopy.Length) :
                    Math.Sqrt(sumOfSquaresOfDifferences / (valCopy.Length - 1));
            }
            return Double.NaN;
        }

        private async void SinglePixelKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                //UpdateSpecConfig((int?)ParseDoubleFromString(IntegrationTimeTextBox.Text), (int?)ParseDoubleFromString(AveragingTextBox.Text));
                int? x = (int?)ParseDoubleFromString(Xcol.Text), y = (int?)ParseDoubleFromString(Ylin.Text);
                if(x != null && y != null)
                {
                    await SetSinglePixel((int)x, (int)y);
                    InfoMessage.Text = "Einzelnen Pixel (" + x + ", " + y + ") aktiviert.";
                }
                else
                    InfoMessage.Text = "Frame nicht aktualisiert. Fehlerhafte Eingabe.";
            }
        }
        // copy of SinglePixelKeyDown()


        private void Voltage33_Checked(object sender, RoutedEventArgs e)
        {
            VoltagePostfix.Text = "3,3V";
        }

        private void Voltage50_Checked(object sender, RoutedEventArgs e)
        {
            VoltagePostfix.Text = "5,0V";
        }

        private void VoltagePostfix_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateExportPath();
        }

        private void Button_Click_19(object sender, RoutedEventArgs e)
        {
            HeatmapToFile(expPathGen.GeneratePath("B Test Heatmap", ".png"));
        }

        private void HeatmapToFile(string file)
        {

            int Height = (int)this.HeatmapGrid.ActualHeight;
            int Width = (int)this.HeatmapGrid.ActualWidth;

            double resolutionScale = 300 / 96;

            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)(resolutionScale * (Width + 1)),
            (int)(resolutionScale * (Height + 1)),
            resolutionScale * 96,
            resolutionScale * 96, PixelFormats.Default);

            //RenderTargetBitmap bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this.HeatmapGrid);

            string Extension = System.IO.Path.GetExtension(file).ToLower();

            BitmapEncoder encoder;
            if (Extension == ".gif")
                encoder = new GifBitmapEncoder();
            else if (Extension == ".png")
                encoder = new PngBitmapEncoder();
            else if (Extension == ".jpg")
                encoder = new JpegBitmapEncoder();
            else
                return;

            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (Stream stm = File.Create(file))
            {
                encoder.Save(stm);
            }
        }

        private async void Button_Click_20(object sender, RoutedEventArgs e)
        {
            if (!DEVICES_READY) return;

            VoltageScanButton.IsEnabled = false;
            StartScanButton.IsEnabled = false;
            StopButton.IsEnabled = true;

            bool useReference = (bool)Durchsatzkorrektur.IsChecked;
            int delay = 100;
            int voltageTmp = (int)Voltage_Slider.Value;
            int fromVoltage = 2500; // hard coded
            int toVoltage = 4000; // hard coded
            int stepVoltage = 100;
            if (ParseDoubleFromString(DelayTextBox.Text) != null) delay = (int)ParseDoubleFromString(DelayTextBox.Text);
            if (ParseDoubleFromString(FromVoltage.Text) != null) fromVoltage = VoltageLimiter((int)ParseDoubleFromString(FromVoltage.Text));
            if (ParseDoubleFromString(ToVoltage.Text) != null) toVoltage = VoltageLimiter((int)ParseDoubleFromString(ToVoltage.Text));
            if (ParseDoubleFromString(StepVoltage.Text) != null) stepVoltage = (int)ParseDoubleFromString(StepVoltage.Text);

            if (toVoltage - fromVoltage < 0) stepVoltage = -Math.Abs(stepVoltage);

            int setVoltage = fromVoltage;
            cts = new CancellationTokenSource();

            while(((setVoltage <= fromVoltage) && (setVoltage >= toVoltage)) || ((setVoltage >= fromVoltage) && (setVoltage <= toVoltage))) // set voltage is in between From->To
            {
                // set voltage of array/slider to setVoltage
                Voltage_Slider.Value = setVoltage;
                await Task.Delay(delay);

                await PixelScan((int)delay, useReference, cts.Token);
               
                setVoltage += stepVoltage;

                if (cts.IsCancellationRequested) break;
            }

            Voltage_Slider.Value = voltageTmp;

            SystemSounds.Beep.Play(); // This will play the system beep sound
            await Task.Delay(1000); // Wait for 1 second
            VoltageScanButton.IsEnabled = true;
            StartScanButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private int VoltageLimiter(int voltage) // TODO values from .ini for max/min voltage
        {
            if (voltage > 4000)
                return 4000;
            else if (voltage < 2500) return 2500;
            else return voltage;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void IntTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(bTimer != null)
            {
                bTimer.Interval = IntTimeSlider.Value;
                Console.WriteLine("timer val set to " + IntTimeSlider.Value);
            }
        }

        private void FileDialogButton_Click(object sender, RoutedEventArgs e)
        {
            String filedialogpath = GetFolderDialog();
            if (filedialogpath is null) return;

            UpdateExportPath(filedialogpath);
        }

        public static string GetFolderDialog()
        {
            System.Windows.Forms.OpenFileDialog folderBrowser = new System.Windows.Forms.OpenFileDialog();
            folderBrowser.ValidateNames = false;
            folderBrowser.CheckFileExists = false;
            folderBrowser.CheckPathExists = true;
            folderBrowser.FileName = "Select a Folder";
            folderBrowser.InitialDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string folderPath = Path.GetDirectoryName(folderBrowser.FileName);
                return folderPath;

            }
            return null;
        }


        private async Task SetSinglePixel(int x, int y)
        {
            if (ArrayConnectStatus)
            {
                bool[,] frame = (bool[,])EmptyFrame.Clone();
                if (x < frame.GetLength(0) && y < frame.GetLength(1))
                    frame[x, y] = true;

                await Task.Run(() =>
                {
                    sMILEUSBDevice.SendFrame(frame);
                });
            }
        }

    }
}