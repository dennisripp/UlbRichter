using MicroControlLED_API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel; // CancelEventArgs
//using OxyPlot;
using System.IO;
using System.Diagnostics;
using LiveCharts;
using LiveCharts.Defaults;
using System.Windows.Data;
using System.Globalization;
using LiveCharts.Wpf;
using System.Timers;
using System.Windows.Media.Imaging;
using Enterwell.Clients.Wpf.Notifications;


namespace ArrayTesting
{
    public partial class MainWindow
    {

        internal async void deviceInitializedEventHandler(object sender, SMILEUSBDevice.DeviceInitializedEvent e)
        {
            Action Invoke = () =>
            {
                UpdateArrayText();

                int voltage = 3300;

                ReconnectArrayButton.IsEnabled = true;
                if (ArrayConnectStatus)
                {
                    sMILEUSBDevice.StopAnimation();

                    switch (sMILEUSBDevice.Hardware.ToString())
                    {
                        case "Demo8x8": arraySize = 8; break;
                        case "Demo16x16": arraySize = 16; break;
                        default: arraySize = 20; break;
                    }

                    if (arraySize == 8) sMILEUSBDevice.SetVoltage((uint)voltage);

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
                            //EmptyFrame[i, j] = false;  not needed, will be initialized false anyway
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
                            if (VoltagePostfix.Text == null)
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
            
                //(e.callingDevice);
            };
            await System.Windows.Application.Current.Dispatcher.BeginInvoke(Invoke);
        }

        private async void UpdateArrayText()
        {
            Action invokeNotifification = () =>
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
            };
            await Application.Current.Dispatcher.BeginInvoke(invokeNotifification);
        }


        private async Task<bool> ArrayConnectAuto()
        {
            List<string> Comportlist = SMILEUSBDevice.GetComPorts();

            sMILEUSBDevice = new SMILEUSBDevice();

            foreach (string com in Comportlist)
            {
                Action Invoke = () =>
                {
                    ArrayConnectStatus = sMILEUSBDevice.Connect(com);
                };
                await System.Windows.Application.Current.Dispatcher.BeginInvoke(Invoke);
            }
            return true;
        }

        private async void InitArray(int voltage = 3300)
        {
            await ArrayConnectAuto();
        }
    }
}
