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
using System.Windows.Media.Imaging;

namespace ArrayTesting
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SMILEUSBDevice sMILEUSBDevice;
        bool ArrayConnectStatus, arrayIsPulsing = false;
        private IntegratingSphere integratingSphere;
        bool[,] EmptyFrame;
        bool[,] FullFrame;
        int arraySize = 16;
        ExportPathGenerator expPathGen = new ExportPathGenerator();
        CancellationTokenSource cts = new CancellationTokenSource();
        private static System.Timers.Timer aTimer, bTimer;

        //public ChartValues<HeatPoint> Values { get; set; }
        public SeriesCollection SerCol { get; set; }
        public ScatterSeries MyScatter { get; set; }
        public string[] xAxis { get; set; }
        public string[] yAxis { get; set; }
        public Func<double, string> YFormatter { get; set; }

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
            int voltage = (int)Voltage_Slider.Value;
            Task InitArrayTask = Task.Run(() => InitArray(voltage));
            Task InitSpecTask = Task.Run(() => InitSpecAsync());

            // do SLOW work here...

            aTimer = new System.Timers.Timer
            {
                Interval = 2000
            };

            aTimer.Elapsed += OnTimedEvent;

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
            ExportComboBox.SelectedIndex = 1;
            ExportComboBox.SelectedIndex = 0;
            UpdateExportPath();

            await InitArrayTask;
            UpdateArrayText();

            await InitSpecTask;
            aTimer.Enabled = true;
            UpdateSpecText();
        }

        private async void OnTimedEvent2(object sender, ElapsedEventArgs e)
        {
            if (integratingSphere != null)
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

        void UpdateArrayText()
        {
            if (ArrayConnectStatus)
            {
                //this.Dispatcher.Invoke(() =>
                //{
                TextArrayStatus.Text = "Verbunden";
                TextArrayStatus.Foreground = Brushes.Green;
                TextArrayType.Text = sMILEUSBDevice.Hardware.ToString();
                //});
            }
            else
            {
                //this.Dispatcher.Invoke(() =>
                //{
                TextArrayStatus.Text = "Getrennt";
                TextArrayStatus.Foreground = Brushes.Red;
                //});
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
        }

        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            SpecRemovalChecker();
        }

        private void InitArray(int voltage)
        {
            sMILEUSBDevice = new SMILEUSBDevice();
            sMILEUSBDevice.Connect("Quadrat");
            ArrayConnectStatus = sMILEUSBDevice.Connect(null);
            
            if (ArrayConnectStatus)
            {
                sMILEUSBDevice.StopAnimation();
                
                switch (sMILEUSBDevice.Hardware.ToString())
                {
                    case "Demo8x8": arraySize = 8; break;
                    case "Demo16x16": arraySize = 16; break;
                    default: arraySize = 20; break;
                }
                
                if (arraySize == 8) sMILEUSBDevice.SetVoltage((uint) voltage);

                //bool[,] BorderLine = new bool[arraySize, arraySize];
                //for (int i = 0; i < arraySize; i++)
                //{
                //    for (int j = 0; j < arraySize; j++)
                //    {
                //        if (1 <= i && i <= arraySize-2 && 1 <= j && j <= arraySize-2) BorderLine[i, j] = false;
                //        else BorderLine[i, j] = true;
                //    }
                //}
                //sMILEUSBDevice.SendFrame(BorderLine);
                

                EmptyFrame = new bool[arraySize, arraySize];
                FullFrame = new bool[arraySize, arraySize];
                for (int i = 0; i < arraySize; i++)
                {
                    for (int j = 0; j < arraySize; j++)
                    {
                        EmptyFrame[i, j] = false;
                        FullFrame[i, j] = true;
                    }
                }
                sMILEUSBDevice.SendFrame(FullFrame);

                this.Dispatcher.Invoke(() =>
                {
                    var info = sMILEUSBDevice.infoData;

                    if (info != null)
                    {
                        // auto-set info text fields
                        uint? sn = info.SerialNumber;
                        if (sn != uint.MaxValue) // int max value
                            SerialTextBox.Text = sn.ToString();

                        uint? ps = info.PixelSize;
                        if (ps >= 1 && ps < 1E6) // int max value
                            LEDSizeBox.Text = ps.ToString();

                        // pitchbox stays empty
                        //PitchBox.Text = info.SerialNumber.ToString();

                        // set integration range 
                        uint? wl = info.Wavelength;
                        if (wl > 100 && wl < 2000)
                        {
                            WavelengthBox.Text = wl.ToString();
                            IntegrationLower.Text = (wl - 50).ToString();
                            IntegrationUpper.Text = (wl + 100).ToString();
                        }
                    }
                    //uint? readVoltage = sMILEUSBDevice.GetInfoData().Voltage;

                    if (arraySize == 16)
                    {
                        if(VoltagePostfix.Text == null)
                            VoltagePostfix.Text = "";
                        Voltage33.IsEnabled = true;
                        Voltage50.IsEnabled = true;
                        // SPANNUNG WIRD NICHT GESETZT, nur per jumper!
                        //if (readVoltage == 3300)
                        //    Voltage33.IsChecked = true;
                        //else if (readVoltage == 5000)
                        //    Voltage50.IsChecked = true;
                        Voltage_Slider.IsEnabled = false;
                        VoltageTextBox.IsEnabled = false;
                        VoltageScanButton.IsEnabled = false;
                    }
                    else
                    {
                        VoltageScanButton.IsEnabled = true;
                        VoltagePostfix.Text = (voltage / 1000.0).ToString("N3") + "V";
                        Voltage33.IsEnabled = false;
                        Voltage50.IsEnabled = false;
                        Voltage_Slider.IsEnabled = true;
                        VoltageTextBox.IsEnabled = true;
                    }
                });

            }
            // InitGraph each time new array is connected
            this.Dispatcher.Invoke(() =>
            {
                InitGraph();
            });
        }

        private async Task InitSpecAsync()
        {
            Task t = Task.Run(() =>
            {
                integratingSphere = new IntegratingSphere();
            }); 

            string[] config = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + @"\config.ini");

            string calPath = @"C:\Users\admin\PowerFolders\Praktikum MSc\05 Array Testing\ILT1810343U1INS250N.txt";
            string refPath = @"C:\Users\admin\PowerFolders\Praktikum MSc\05 Array Testing\Ref1000avg150msPortZu.txt";

            for (int i = 0; i < config.Length; i++)
            {
                if(!config[i].Contains("\\") && i < config.Length - 1)
                {
                    if (config[i].ToLower().Contains("alib")) // AKA Kalibration | calibration
                    {
                        calPath = config[i + 1];
                    }
                    if (config[i].ToLower().Contains("ref")) // AKA Referenz | reference
                    {
                        refPath = config[i + 1];
                    }
                }
            }

            await t;

            integratingSphere.LoadCalibrationFile(calPath);
            integratingSphere.LoadReferenceFile(refPath);


            IInputElement focusedControl = null;

            this.Dispatcher.Invoke(() =>
            {
                focusedControl = FocusManager.GetFocusedElement(this);
                Console.WriteLine("REFERENCE 1");
                CalFilename.Text = Path.GetFileNameWithoutExtension(calPath);
                CalFilename.Foreground = Brushes.Green;
                CalibCombo.Text = calPath;
                CalibCombo.Focus();
                CalibCombo.Select(CalibCombo.Text.Length, 0);
                Console.WriteLine("REFERENCE 2");
            });
            await Task.Delay(50);
            Console.WriteLine("REFERENCE 3");
            this.Dispatcher.Invoke(() =>
            {
                Console.WriteLine("REFERENCE 4");
                RefFile.Text = Path.GetFileNameWithoutExtension(refPath);
                RefFile.Foreground = Brushes.Green;
                RefScanBox.Text = refPath;
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (ArrayConnectStatus) sMILEUSBDevice.SendFrame(FullFrame);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (ArrayConnectStatus == true)
            {
                var rand = new Random();
                bool[,] SinglePixelFrame;
                SinglePixelFrame = (bool[,])EmptyFrame.Clone();
                SinglePixelFrame[rand.Next(arraySize), rand.Next(arraySize)] = true;
                sMILEUSBDevice.SendFrame(SinglePixelFrame);
            }
        }

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (ArrayConnectStatus == true)
            {
                Task PulseArray = Task.Factory.StartNew(() => PulseLEDArray());
                await PulseArray;
            }
        }

        private async void PulseLEDArray()
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
            TextArrayStatus.Text = "Getrennt";
            TextArrayStatus.Foreground = Brushes.Red;
            TextArrayType.Text = "";
        }

        async Task SpecShutdown()
        {
            if (integratingSphere != null)
                await Task.Factory.StartNew(() => integratingSphere.DisconnectIntegratingSphere());
            UpdateSpecInfo();
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

            if (arraySize == 8) VoltagePostfix.Text = (voltage / 1000.0).ToString("N3") + "V";

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

        private async void Button_Click_5(object sender, RoutedEventArgs e)
        {
            
        }

        // DIRTY CODE
        private async void SpecRemovalChecker()
        {
            //bool connectTabEnabled = true;
            //this.Dispatcher.Invoke(() =>
            //{
            //    connectTabEnabled = connectTab.IsSelected;
            //});
            //if (connectTabEnabled)
            //{

                //string specSer = "6&1402B392&0&4"; // 5&69573AC&0&2
                string vid = "1992", pid = "0667"; //  VID_1992 & PID_0667
                bool specFound = false, arrayFound = false;
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
                            //if (usbDevice.Contains("USB"))
                            //    Console.WriteLine(usbDevice);
                            
                            if (usbDevice.Contains(pid) && usbDevice.Contains(vid))
                            //if (usbDevice.Contains(specSer))
                            {
                                //integratingSphere is connected
                                //this.Dispatcher.Invoke(() =>
                                //{
                                //    Console.WriteLine("Spektrometer ist verbunden.");
                                //});
                                
                                specFound = true;
                            }
                        }
                    }
                }

                if (!specFound)
                {
                    try
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            TextSpecStatus.Text = "Getrennt";
                            TextSpecStatus.Foreground = Brushes.Red;
                            InfoMessage.Text = "Spektrometer ist getrennt.";
                        });
                    }
                    catch(System.Threading.Tasks.TaskCanceledException)
                    {
                    
                    }
                }

                //try
                //{
                //    await Task.Delay(2000, cts.Token);
                //}
                //catch(TaskCanceledException)
                //{
                //    break;
                //}

                //this.Dispatcher.Invoke(() =>
                //{   
                //    connectTabEnabled = connectTab.IsSelected;
                //});
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

        private async void Button_Click_6(object sender, RoutedEventArgs e)
        {
            bool vScanWasEnabled = VoltageScanButton.IsEnabled;
            if (vScanWasEnabled) VoltageScanButton.IsEnabled = false;
            StartScanButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            MessageBoxResult mbr = MessageBoxResult.Yes;
            if (arraySize == 16 && Voltage33.IsChecked != true && Voltage50.IsChecked != true)
                mbr = MessageBox.Show("Keine Spannung gewählt. Fortfahren?",
                                   "16x16 Voltage Selection Jumper", MessageBoxButton.YesNo,
                                   MessageBoxImage.Exclamation);
            if (mbr == MessageBoxResult.Yes)
            {
                int? delay = (int?)ParseDoubleFromString(DelayTextBox.Text);
                if (delay == null) delay = 0;
                bool useReference = (bool)Durchsatzkorrektur.IsChecked;

                cts = new CancellationTokenSource();
                await Task.Run(() => PixelScan((int)delay, useReference, cts.Token));
            }

            if (vScanWasEnabled) VoltageScanButton.IsEnabled = true;
            StartScanButton.IsEnabled = true;
            StopButton.IsEnabled = false;
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
            });

            int pixels = arraySize * arraySize;
            int measuredDataPoints = 0;
            double[] pixelX = new double[pixels];
            double[] pixelY = new double[pixels];
            double[] powerResults = new double[pixels];

            // GET DARK MEASUREMENT CHARACTERISTICS
            List<string> darkSummary = new List<string> { };
            double DIEtotenschwelle = await DarkTests(darkSummary, lower, upper);

            this.Dispatcher.Invoke(() =>
            {
                HeatSeries.Values.Clear();
                Output.Text = String.Join("\n", darkSummary);
            });

            darkSummary.Insert(0, "====== TOTE PIXEL =======");

            
            // cycle through pixels
            for (int i = 0; i < arraySize; i++) // i = y
            {
                for (int j = 0; j < arraySize; j++) // j = x
                {
                    await SetSinglePixel(j, i);
                    await Task.Delay(delay); // WANN KOMMT EIN FRAME AN?!

                    double[][] results = await ScanAndGetAbsData(useReference);
                    DataArrayToFile(results, expPathGen.GeneratePath("Spektrum Pixel("+j+", "+i+")"));
                    
                    // power calculation
                    double sum = Integrate(results[0], results[1], lower, upper);
                    //Console.WriteLine("Leistung: Integral von 400-550" + sum);
                    
                    powerResults[measuredDataPoints] = sum;
                    pixelX[measuredDataPoints] = j;
                    pixelY[measuredDataPoints] = i;

                    this.Dispatcher.Invoke(() =>
                    {
                        HeatSeries.Values.Add(new HeatPoint(j, arraySize - 1 - i, (int)sum));
                    });

                    measuredDataPoints++;
                    this.Dispatcher.Invoke(() =>
                    {
                        //    OutputText.Text = Math.Round(integratingSphere.GetScanDataAbsoluteAt(471), digits: 2).ToString() + " µW/nm/cm²";
                        RoutineProgress.Value = 100.0 * (double)measuredDataPoints / (double)pixels;
                    });
                    if (t.IsCancellationRequested) break;
                }
                if (t.IsCancellationRequested) break;
            }
            if (!t.IsCancellationRequested)
            {
                // rewrite heatmap
                //this.Dispatcher.Invoke(() =>
                //{
                //    int index = 0;
                //    for (int i = 0; i < arraySize; i++)
                //    {
                //        for (int j = 0; j < arraySize; j++)
                //        {
                //            Values.Add(new HeatPoint(j, arraySize - 1 - i, (int)powerResults[index]));
                //            index++;
                //        }
                //    }
                //});

                // Write heatmap file
                double[][] heatmap = new double[][] { (double[])pixelX, pixelY, powerResults };
                DataArrayToFile(heatmap, expPathGen.GeneratePath("B Heatmap"));

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

                StringsToFile(summary, expPathGen.GeneratePath("A Zusammenfassung"));

                this.Dispatcher.Invoke(() =>
                {
                    Output.Text = String.Join("\n", summary);
                    HeatmapToFile(expPathGen.GeneratePath("B Heatmap", ".png"));
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

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            DoDarkScan();
        }

        private async Task DoDarkScan()
        {
            if (integratingSphere != null)
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
            if (integratingSphere != null)
            {
                await integratingSphere.SingleScan();
                return new double[][] { integratingSphere.GetCalibratedAbsoluteDataX(), integratingSphere.GetCalibratedAbsoluteDataY(useRef) };
            }
            return null;
        }

        private async void Button_Click_8(object sender, RoutedEventArgs e)
        {
            //dataArrayToFile(new double[][] { new double[] { 1, 2 }, new double[] { 3, 4 } }, @"C:\Users\admin\PowerFolders\Praktikum MSc\05 Array Testing\ArrayTesting\" + DateTime.Now.Ticks + "test123.txt");
            //dataArrayToFile(new double[][] { new double[] { 1, 2 }, new double[] { 3, 4 } }, expPathGen.GeneratePath("Test"));
            //await integratingSphere.SingleScan();
            //double[][] results = { integratingSphere.GetScanDataAbsoluteX(), integratingSphere.GetScanDataAbsoluteY()};
            double[][] data = await ScanAndGetAbsData((bool)Durchsatzkorrektur.IsChecked);
            if (data != null)
            {
                this.Dispatcher.Invoke(() =>
                {
                    PlotData(data[0], data[1]);
                });
                DataArrayToFile(data, expPathGen.GeneratePath("Einzelspektrum"));
            }
        }

        private void Button_Click9(object sender, RoutedEventArgs e)
        {
            if (ArrayConnectStatus) sMILEUSBDevice.SendFrame(EmptyFrame);
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

        private void UpdateExportPath()
        {
            string voltageComment = String.Empty;
            if (VoltagePostfix.Text != null)
                voltageComment = VoltagePostfix.Text;
            string ser = String.Empty;
            if (SerialTextBox.Text != null)
                ser = SerialTextBox.Text;

            string sbPathShow = ExportComboBox.Text;

            
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

        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            //Process.Start(System.IO.Path.GetDirectoryName(ExportComboBox.Text));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(expPathGen.GeneratePath("TestExport")));
            Process.Start(System.IO.Path.GetDirectoryName(expPathGen.folderPath));
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

        private void AveragingTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                UpdateSpecConfig((int?)ParseDoubleFromString(IntegrationTimeTextBox.Text), (int?)ParseDoubleFromString(AveragingTextBox.Text));
            }
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

        private async void Button_Click_10(object sender, RoutedEventArgs e)
        {
            //int intTmp = integratingSphere.GetIntTime();
            //int avgTmp = integratingSphere.GetAvg();
            //int[] intTimes = { 1, 2 , 3 ,4,5,6,7,8,9,10,15,20,25,30,35,40,45,50,100,150,200,300,400,500,1000};
            //int avg = 100;

            //double[][] data = { new double[intTimes.Length], new double[intTimes.Length] };
            //for (int i = 0;  i< intTimes.Length; i++)
            //{
            //    UpdateSpecConfig(intTimes[i], avg, forceDarkScan: true);
            //    double[][] results = await ScanAndGetAbsData(false);
            //    // power calculation
            //    double sum = 0.0;
            //    // 386 -- 555 nm integration
            //    for (int k = 386; k <= 555; k++)
            //    {
            //        sum += results[1][k] * (results[0][k + 1] - results[0][k - 1]) / 2;
            //    }
            //    data[0][i] = intTimes[i];
            //    data[1][i] = sum;

            //    DataArrayToFile(results, expPathGen.GeneratePath(intTimes[i] + " ms"));
            //}
            //DataArrayToFile(data, expPathGen.GeneratePath("Integration"));
            //UpdateSpecConfig(intTmp, avgTmp, forceDarkScan: true);
        }
        

        private void DarkSubButton_Copy_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BitchButton_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void Button_Click_11(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
        }

        private async void Button_Click_12(object sender, RoutedEventArgs e)
        {
            var tmp = CalibButton.BorderBrush;
            string path = CalibCombo.Text;

            if (integratingSphere != null && integratingSphere.LoadCalibrationFile(path))
            {
                InfoMessage.Text = "Kalibration aktualisiert!";
                CalibButton.BorderBrush = Brushes.Lime;
                CalibCombo.Foreground = Brushes.Green;
            }
            else
            {
                InfoMessage.Text = "Kalibration nicht aktualisiert!";
                if (File.Exists(path))
                    InfoMessage.Text += " Datei existiert nicht!";

                CalibButton.BorderBrush = Brushes.Red;
            }
            await Task.Delay(1500);
            
            CalibButton.BorderBrush = tmp;
            
        }

        private async void SphereMultiButton_Click(object sender, RoutedEventArgs e)
        {
            int lower = (int)ParseDoubleFromString(IntegrationLower.Text);
            int upper = (int)ParseDoubleFromString(IntegrationUpper.Text);

            if (integratingSphere != null && integratingSphere.IsConnected())
            {
                bool hashadDark = integratingSphere.HasDarkScan;
                int intTemp = integratingSphere.GetIntTime();
                int avgTemp = integratingSphere.GetAvg();

                //integratingSphere.SetIntTime(149);
                //integratingSphere.SetScanAvg(20);
                await UpdateSpecConfig(150, 20, forceDarkScan: true);

                // turn off array (again) == blink once
                await Task.Delay(50);
                if (ArrayConnectStatus) sMILEUSBDevice.SendFrame(EmptyFrame);

                MessageBoxResult mbr = MessageBox.Show("Refenzquelle eingeschaltet?", "Referenzabgleich", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                
                if(mbr == MessageBoxResult.No)
                {
                    InfoMessage.Text = "Referenzaufnahme abgerochen.";
                }
                else
                {
                    double[] ref2 = (double[])(await integratingSphere.CreateReferenceScan()).Clone();

                    double corr = Integrate(integratingSphere.GetCalibratedAbsoluteDataX(), ref2, lower, upper) / (upper - lower);

                    PlotData(integratingSphere.GetCalibratedAbsoluteDataX(), ref2);

                    InfoMessage.Text = "Durschnittlicher Korrekturfaktor von " + lower + " nm bis " + upper + " nm: " + Math.Round(corr, 3).ToString("N3", new NumberFormatInfo() { NumberDecimalSeparator = "," }) + ".";

                    CreateReference.Background = Brushes.PaleGreen;
                    CreateReference.BorderBrush = Brushes.Green;

                    Durchsatzkorrektur.IsEnabled = true;
                    Durchsatzkorrektur.IsChecked = true; 
                }
                mbr = MessageBox.Show("Referenzquelle für Dunkelabzug ausschalten!", "Referenzscan", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                await UpdateSpecConfig(intTemp, avgTemp, forceDarkScan: true);
                // turn on array
                if (ArrayConnectStatus) sMILEUSBDevice.SendFrame(FullFrame);
            }
            else
            {
                InfoMessage.Text = "Spektrometer nicht bereit.";
            }
        }

        void PlotData(double[] dataX, double[] dataY, string title = "Spektrum")
        {

            ChartValues<ObservablePoint> cv = new ChartValues<ObservablePoint>();
            

            for (int i = 0; i < dataX.Length; i++)
                if (dataX[i] > 300 && dataX[i] < 600) // plot area
                    cv.Add(new ObservablePoint(Math.Round(dataX[i], 2), Math.Round(dataY[i], 2)));
           
            this.Dispatcher.Invoke(() =>
            {
                MyScatter.Title = title;
                MyScatter.Values = cv;
                if (SerCol == null)
                    SerCol = new SeriesCollection();
                else
                {
                    SerCol.Clear();
                    SerCol.Add(MyScatter);
                }
                SpectrumPlot.Series = SerCol;
            //SerCol.
            ////SerCol.Clear();
            //SerCol.Insert(0, new LineSeries()
            //{
            //    Title = title,
            //    Values = cv,
            //    PointGeometry = null,
            //});
            //Thread.Sleep(10);
            //if(SerCol.Count > 1)
            //    SerCol.RemoveAt(1);
            });
        }

        private void CalibCombo_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalibCombo.Foreground = Brushes.Black;
        }

        private async void Button_Click_13(object sender, RoutedEventArgs e)
        {
            var tmp = RefButton.BorderBrush;
            string path = RefScanBox.Text;

            if (integratingSphere != null && integratingSphere.LoadReferenceFile(path))
            {
                InfoMessage.Text = "Referenz aktualisiert!";
                RefButton.BorderBrush = Brushes.Lime;
                RefFile.Text = path.Split('\\')[path.Split('\\').Length-1].Split('.')[0];
                RefFile.Foreground = Brushes.Green;
            }
            else
            {
                InfoMessage.Text = "Referenz nicht aktualisiert!";
                if (File.Exists(path))
                    InfoMessage.Text += " Datei existiert nicht!";

                RefButton.BorderBrush = Brushes.Red;
            }
            await Task.Delay(1500);

            RefButton.BorderBrush = tmp;

        }
        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            aTimer.Enabled = false;
            await SpecShutdown();
            UpdateDarkScanStatus();
            //Task InitSpecTask = Task.Factory.StartNew(() => InitSpecAsync());
            await InitSpecAsync();
            aTimer.Enabled = true;
            UpdateSpecText();
        }

        private async void Button_Click_14(object sender, RoutedEventArgs e)
        {
            await SpecShutdown();
        }

        private async void Button_Click_15(object sender, RoutedEventArgs e)
        {
            await ArrayShutdown();
        }

        private async void Button_Click_16(object sender, RoutedEventArgs e)
        {
            ReconnectArrayButton.IsEnabled = false;
            await ArrayShutdown();
            int voltage = (int)Voltage_Slider.Value;
            Task InitArrayTask = Task.Factory.StartNew(() => InitArray(voltage));
            await InitArrayTask;
            UpdateArrayText();
            ReconnectArrayButton.IsEnabled = true;
        }

        private void StartStop_Click(object sender, RoutedEventArgs e)
        {
            bTimer.Enabled = bTimer.Enabled? false:true;
        }

        private async void Button_Click_17(object sender, RoutedEventArgs e)
        {
            int lower = 400;
            int upper = 550;
            lower = (int)ParseDoubleFromString(IntegrationLower.Text);
            upper = (int)ParseDoubleFromString(IntegrationUpper.Text);
            List<string> output = new List<string> { };
            await DarkTests(output, lower, upper);
            Output.Text = String.Join("\n", output);
        }

        async Task<double> DarkTests(List<string> darkSummary, int lower, int upper)
        {
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
        private async void Button_Click_18(object sender, RoutedEventArgs e)
        {
            //UpdateSpecConfig((int?)ParseDoubleFromString(IntegrationTimeTextBox.Text), (int?)ParseDoubleFromString(AveragingTextBox.Text));
            int? x = (int?)ParseDoubleFromString(Xcol.Text), y = (int?)ParseDoubleFromString(Ylin.Text);
            if (x != null && y != null)
            {
                await SetSinglePixel((int)x, (int)y);
                InfoMessage.Text = "Einzelnen Pixel (" + x + ", " + y + ") aktiviert.";
            }
            else
                InfoMessage.Text = "Frame nicht aktualisiert. Fehlerhafte Eingabe.";
        }

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

                await Task.Run(() => PixelScan((int)delay, useReference, cts.Token));
                
                setVoltage += stepVoltage;

                if (cts.IsCancellationRequested) break;
            }

            Voltage_Slider.Value = voltageTmp;

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