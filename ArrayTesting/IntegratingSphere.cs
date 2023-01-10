using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Make sure we include the InteropServices so we have access to DllImport attribute.
using System.Runtime.InteropServices;
using System.IO;
using System.Globalization;
using System.Windows;

namespace ArrayTesting
{
    class IntegratingSphere
    {
        Int32 NumPx = 2048; /// number of px of CCD array
        Int32 len = 20; /// Length of DevList; Should be at least 10*N devices (here: N=1)
        Int32 DevIdx = 0; /// Index of device; 0 = First Device
        UInt32 IntTime = 150; /// Value of integration time [ms]
        UInt32 Avg = 1; /// Number of scans to average per acquisition
        bool SpecConnected = false;

        MainWindow mwd { get; set; }
        public bool HasDarkScan = false;
        char[] DevList;
        double[] DataAbsoluteX { get; set; }
        double[] DataAbsoluteY { get; set; }
        double[] DataDarkX;
        double[] DataDarkY;
        double[] DataRawX;
        double[] DataRawY;
        double[] DataRelativeX;
        double[] DataRelativeY;
        double[] CRI;
        double[] XYZ;

        double[] CalibrationX { get; set; }
        double[] CalibrationY { get; set; }

        double[] ReferenceEmtpyY;
        double[] ReferenceLoadedY;
        double[] RefCorrectionY;

        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll", CharSet = CharSet.Unicode)]
        ///[DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILight.dll", CharSet = CharSet.Unicode)]
        private extern static Int32 ILT900_API_Open(char[] DevList, Int32 len);
        ///private extern static Int32 ILT900_API_Open(char[] DevList, Int32 len);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll", CharSet = CharSet.Unicode)]
        private extern static Int32 ILT900_API_DetectDevice(Int32 Ignore900, char[] DevList, Int32 len);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_SetIntTime(Int32 DevIdx, UInt32 IntTime);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_SetScanAvg(Int32 DevIdx, UInt32 Avg);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_DarkScan();
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_GetDarkData(Int32 DevIdx, double[] DataY, double[] DataX, Int32 len1, Int32 len2);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_SetDarkData(Int32 DevIdx, double[] DataY, Int32 len);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_GetRawData(Int32 DevIdx, double[] DataY, double[] DataX, Int32 len1, Int32 len2);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_GetRelativeData(Int32 DevIdx, double[] DataY, double[] DataX, Int32 len1, Int32 len2);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_GetAbsoluteData(Int32 DevIdx, double[] DataY, double[] DataX, Int32 len1, Int32 len2);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_SingleScan();
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_Close();
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        ///private extern static Int32 ILT900_API_GetColor_CCT(Int32 DevIdx, double* CCT);
        ///[DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_GetColor_CRI(Int32 DevIdx, double[] CRI, Int32 len);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_GetColor_cXYZ(Int32 DevIdx, double[] XYZ, Int32 len);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_GetColor_Duv(Int32 DevIdx, double[] XYZ, Int32 len);

        private bool SpectreLightInstalled { get; set; }

        public IntegratingSphere()
        {
            DevList = new char[len];
            DataAbsoluteX = new double[NumPx];
            DataAbsoluteY = new double[NumPx];
            DataDarkX = new double[NumPx];
            DataDarkY = new double[NumPx];
            DataRawX = new double[NumPx];
            DataRawY = new double[NumPx];
            DataRelativeX = new double[NumPx];
            DataRelativeY = new double[NumPx];
            CRI = new double[16];
            XYZ = new double[3];

            try
            {
                if (ILT900_API_Open(DevList, len) == 0)
                {
                    SpecConnected = true;
                }

                ILT900_API_SetIntTime(DevIdx, IntTime);
                ILT900_API_SetScanAvg(DevIdx, Avg);
                SpectreLightInstalled = true;

            }
            catch (System.DllNotFoundException e)
            {
                SpectreLightInstalled = false;

                MessageBox.Show("SpectrlLight III not installed.");
            }    
        }

        public IntegratingSphere(MainWindow _mwd)
        {
            mwd = _mwd;
            DevList = new char[len];
            DataAbsoluteX = new double[NumPx];
            DataAbsoluteY = new double[NumPx];
            DataDarkX = new double[NumPx];
            DataDarkY = new double[NumPx];
            DataRawX = new double[NumPx];
            DataRawY = new double[NumPx];
            DataRelativeX = new double[NumPx];
            DataRelativeY = new double[NumPx];
            CRI = new double[16];
            XYZ = new double[3];

            try
            {
                if (ILT900_API_Open(DevList, len) == 0)
                {
                    SpecConnected = true;
                }

                ILT900_API_SetIntTime(DevIdx, IntTime);
                ILT900_API_SetScanAvg(DevIdx, Avg);
                SpectreLightInstalled = true;

            }
            catch (System.DllNotFoundException)
            {
                SpectreLightInstalled = false;

                mwd.spectreNotInstalledNotification();
            }
        }

        public void SetIntTime(int IntTime)
        {
            if (!SpectreLightInstalled)
            {
                mwd.spectreNotInstalledNotification();
                return;
            }

            this.IntTime = (UInt32)IntTime;
            ILT900_API_SetIntTime(DevIdx, (UInt32)IntTime);
            HasDarkScan = false;
        }
        
        public void SetScanAvg(int Avg)
        {
            if (!SpectreLightInstalled)
            {
                mwd.spectreNotInstalledNotification();
                return;
            }

            this.Avg = (UInt32)Avg;
            ILT900_API_SetScanAvg(DevIdx, (UInt32)Avg);
            HasDarkScan = false; // unsure if...
                  
        }

        public async Task SingleScan()
        {
            if (!SpectreLightInstalled)
            {
                mwd.spectreNotInstalledNotification();
                return;
            }
            Task ScanTask = Task.Factory.StartNew(() => ILT900_API_SingleScan());
            await ScanTask;
            //ILT900_API_GetAbsoluteData(DevIdx, DataAbsoluteY, DataAbsoluteX, NumPx, NumPx);
            ILT900_API_GetRelativeData(DevIdx, DataRelativeY, DataRelativeX, NumPx, NumPx);
        }

        public async Task DarkScan()
        {
            if (!SpectreLightInstalled)
            {
                mwd.spectreNotInstalledNotification();
                return;
            }

            Task ScanTask = Task.Factory.StartNew(() => ILT900_API_DarkScan());
            await ScanTask;
            HasDarkScan = true;
        }

        // calibration file needs to be of the same wavelength division as sensor
        // needs to be NumPx lines (2048), each: "calX \t calY"
        public bool LoadCalibrationFile(string path)
        {
            if (File.Exists(path))
            {
                string[] calLines = File.ReadAllLines(path);
                CalibrationX = new double[NumPx];
                CalibrationY = new double[NumPx];

                NumberFormatInfo provider = new NumberFormatInfo();
                if (calLines[0].Contains(','))
                    provider.NumberDecimalSeparator = ",";
                else
                    provider.NumberDecimalSeparator = ".";

                for (int i = 0; i < NumPx; i++)
                {
                    CalibrationX[i] = Convert.ToDouble(calLines[i].Split('\t')[0], provider);
                    CalibrationY[i] = Convert.ToDouble(calLines[i].Split('\t')[1], provider);
                }
                return true;
            }
            else
                return false;
        }

        public bool LoadReferenceFile(string path)
        {
            if (File.Exists(path))
            {
                string[] refLines = File.ReadAllLines(path);
                ReferenceEmtpyY = new double[NumPx];

                NumberFormatInfo provider = new NumberFormatInfo();

                if (refLines[0].Contains(','))
                    provider.NumberDecimalSeparator = ",";
                else
                    provider.NumberDecimalSeparator = ".";

                for (int i = 0; i < NumPx; i++)
                {
                    //ReferenceEmptyY[i] = Convert.ToDouble(calLines[i].Split('\t')[0], provider);
                    ReferenceEmtpyY[i] = Convert.ToDouble(refLines[i].Split('\t')[1], provider);
                }
                return true;
            }
            else
                return false;
        }

        public async Task<double[]> CreateReferenceScan()
        {
            if(SpecConnected && HasDarkScan)
            {
                await SingleScan();
                ReferenceLoadedY = (double[])GetCalibratedAbsoluteDataY(false).Clone();
                RefCorrectionY = new double[NumPx];
                double sum = 0;
                for(int i = 0; i< NumPx; i++)
                {
                    RefCorrectionY[i] = ReferenceLoadedY[i] == 0 ? 0 : ReferenceEmtpyY[i] / ReferenceLoadedY[i];
                    sum += RefCorrectionY[i];
                }
                double avgCorr = sum/NumPx;
                return RefCorrectionY;
            }
            return null;
            
        }

        public bool SpecHasDarkScan()
        {
            return HasDarkScan;
        }

        public double[] GetCalibratedAbsoluteDataX()
        {
            return (double[])DataRelativeX.Clone();
        }

        // uses global variables
        public double[] GetCalibratedAbsoluteDataY(bool useReferenceCorr)
        {

            //if (CalibrationY is null)
            //{
            //    mwd.customErrorNotification("fatal error", "CalibrationY not initialized");
            //    return new double[0];
            //}

            double[] AbsoluteY = new double[NumPx];

            AbsoluteY = new double[NumPx];

            if(useReferenceCorr && RefCorrectionY != null)
            {
                for (int i = 0; i < NumPx; i++)
                {
                    AbsoluteY[i] = DataRelativeY[i] * CalibrationY[i] * 1000 / (double)IntTime * RefCorrectionY[i];
                }
            }
            else
            {
                for (int i = 0; i < NumPx; i++)
                {
                    AbsoluteY[i] = DataRelativeY[i] * CalibrationY[i] * 1000 / (double)IntTime;
                }
            }
            
            //// update DataAbsolute:

            //DataAbsoluteX = new double[len];
            //DataAbsoluteY = new double[len];
            //for (int i = 0; i < len; i++)
            //{
            //    DataAbsoluteX[i] = abs[0][i];
            //    DataAbsoluteX[i] = abs[0][i];
            //}
            
            return AbsoluteY;
        }

        // < outdated ----
        // do not use: it is unclear, which calibration file is internally loaded inside the spectrometer!
        public double[] GetScanDataAbsoluteY()
        {
            return (double[])DataAbsoluteY.Clone();
        }

        public double[] GetScanDataAbsoluteX()
        {
            return (double[])DataAbsoluteX.Clone();
        }

        // ---- outdated />

        public int GetAvg()
        {
            return (int) Avg;
        }
        public int GetIntTime()
        {
            return (int) IntTime;
        }

        public double GetScanDataAbsoluteAt(int index)
        {
            if (!SpectreLightInstalled)
            {
                mwd.spectreNotInstalledNotification();
                return -1;
            }

            ILT900_API_GetAbsoluteData(DevIdx, DataAbsoluteY, DataAbsoluteX, NumPx, NumPx);
            return DataAbsoluteY[index];
        }

        public int DisconnectIntegratingSphere()
        {
            if (!SpectreLightInstalled)
            {
                mwd.spectreNotInstalledNotification();
                return -1;
            }

            HasDarkScan = false;
            if (ILT900_API_Close() == 0)
            {
                SpecConnected = false;
                return 0;
            }
            return -1;
        }

        public bool IsConnected()
        {
            return SpecConnected;
        }

        //static void Main(string[] args)
        //{
        //    Console.WriteLine("Hello World!");
        //    Int32 NumPx = 2048; /// number of px of CCD array
        //    Int32 len = 100; /// Length of DevList; Should be at least 10*N devices (here: N=1)
        //    Int32 DevIdx = 0; /// Index of device; 0 = First Device
        //    UInt32 IntTime = 20; /// Value of integration time [ms]
        //    UInt32 Avg = 5; /// Number of scans to average per acquisition
        //    char[] DevList = new char[len];
        //    double[] DataAbsoluteX = new double[NumPx];
        //    double[] DataAbsoluteY = new double[NumPx];
        //    double[] DataDarkX = new double[NumPx];
        //    double[] DataDarkY = new double[NumPx];
        //    double[] DataRawX = new double[NumPx];
        //    double[] DataRawY = new double[NumPx];
        //    double[] DataRelativeX = new double[NumPx];
        //    double[] DataRelativeY = new double[NumPx];
        //    double[] CRI = new double[16];
        //    double[] XYZ = new double[3];
        //    ///ILT900_API_DetectDevice(0, DevList, 100);
        //    ILT900_API_Open(DevList, len);
        //    ILT900_API_SetIntTime(DevIdx, IntTime);
        //    ILT900_API_SetScanAvg(DevIdx, Avg);
        //    ILT900_API_DarkScan();
        //    ILT900_API_GetDarkData(DevIdx, DataDarkY, DataDarkX, NumPx, NumPx);
        //    ILT900_API_SetDarkData(DevIdx, DataDarkY, NumPx);
        //    Console.WriteLine("\n Press any key to exit the process...");
        //    Console.ReadKey();
        //    ILT900_API_SingleScan();
        //    ILT900_API_GetAbsoluteData(DevIdx, DataAbsoluteY, DataAbsoluteX, NumPx, NumPx);
        //    ILT900_API_GetRelativeData(DevIdx, DataRelativeY, DataRelativeX, NumPx, NumPx);
        //    ILT900_API_GetRawData(DevIdx, DataRawY, DataRawX, NumPx, NumPx);
        //    ///            ILT900_API_GetColor_CCT(DevIdx, CCT);
        //    ILT900_API_GetColor_CRI(DevIdx, CRI, 16);
        //    ILT900_API_GetColor_cXYZ(DevIdx, XYZ, 3);
        //    ILT900_API_Close();
        //}
    }
}
