using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using Enterwell.Clients.Wpf.Notifications;
using System.Threading;

namespace ArrayTesting
{
    public partial class MainWindow
    {
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

        private async void Button_Click_5(object sender, RoutedEventArgs e)
        {

        }

        private async void Button_Click_6(object sender, RoutedEventArgs e)
        {
            if (!DEVICES_READY) return;

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

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            if (!DEVICES_READY) return;

            DoDarkScan();
        }
        private async void Button_Click_8(object sender, RoutedEventArgs e)
        {
            if (!DEVICES_READY) return; 
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

        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            //Process.Start(System.IO.Path.GetDirectoryName(ExportComboBox.Text));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(expPathGen.GeneratePath("TestExport")));
            Process.Start(System.IO.Path.GetDirectoryName(expPathGen.folderPath));
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

                if (mbr == MessageBoxResult.No)
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

        private async void Button_Click_13(object sender, RoutedEventArgs e)
        {
            var tmp = RefButton.BorderBrush;
            string path = RefScanBox.Text;

            if (integratingSphere != null && integratingSphere.LoadReferenceFile(path))
            {
                InfoMessage.Text = "Referenz aktualisiert!";
                RefButton.BorderBrush = Brushes.Lime;
                RefFile.Text = path.Split('\\')[path.Split('\\').Length - 1].Split('.')[0];
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
            await SpecShutdown();
            UpdateDarkScanStatus();
            //Task InitSpecTask = Task.Factory.StartNew(() => InitSpecAsync());
            await InitSpecAsync();
            UpdateSpecText();
        }

        private async void Button_Click_14(object sender, RoutedEventArgs e)
        {
            await SpecShutdown();
        }

        private async void Button_Click_ArrayShutdown(object sender, RoutedEventArgs e)
        {
            await ArrayShutdown();
        }

        private async void Button_Click_ArrayReconnect(object sender, RoutedEventArgs e)
        {
            ReconnectArrayButton.IsEnabled = false;
            await ArrayShutdown();
            int voltage = (int)Voltage_Slider.Value;
            Task InitArrayTask = Task.Factory.StartNew(() => InitArray(voltage));
            await InitArrayTask;
            
        }

        private void StartStop_Click(object sender, RoutedEventArgs e)
        {
            bTimer.Enabled = bTimer.Enabled ? false : true;
        }

        private async void Button_Click_17(object sender, RoutedEventArgs e)
        {
            if (!DEVICES_READY) return;

            int lower = 400;
            int upper = 550;
            lower = (int)ParseDoubleFromString(IntegrationLower.Text);
            upper = (int)ParseDoubleFromString(IntegrationUpper.Text);
            List<string> output = new List<string> { };
            await DarkTests(output, lower, upper);
            Output.Text = String.Join("\n", output);
        }

        private async void Button_Click_18(object sender, RoutedEventArgs e)
        {
            if (sMILEUSBDevice is null) return;

            int? parsed_x_val = (int?)ParseDoubleFromString(Xcol.Text);
            int? parsed_y_val = (int?)ParseDoubleFromString(Ylin.Text);
            int x = parsed_x_val is null || parsed_x_val > sMILEUSBDevice.ColCount - 1 ? sMILEUSBDevice.ColCount - 1 : (int)parsed_x_val;
            int y = parsed_y_val is null || parsed_y_val > sMILEUSBDevice.RowCount - 1 ? sMILEUSBDevice.RowCount - 1 : (int)parsed_y_val;
            Xcol.Text = x.ToString();
            Ylin.Text = y.ToString();
            await SetSinglePixel(x, y);
            InfoMessage.Text = "Einzelnen Pixel (" + x + ", " + y + ") aktiviert.";
        }



        private void TextBox_PreviewTextInputOnlyDigits(object sender, TextCompositionEventArgs e)
        {
            // If the input is not a digit, handle the event to prevent the input from being processed
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!IsTextAllowed(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private static bool IsTextAllowed(string text)
        {
            foreach (char c in text)
            {
                if (!Char.IsDigit(c)) return false;
            }

            return true;
        }
    }
}
