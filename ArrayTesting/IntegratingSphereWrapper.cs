using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Make sure we include the InteropServices so we have access to DllImport attribute.
using System.Runtime.InteropServices;

namespace ArrayTesting
{
    class Program
    {
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
        ///private extern static Int32 ILT900_API_GetColor_CCT(Int32 DevIdx, double *CCT);
        ///[DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_GetColor_CRI(Int32 DevIdx, double[] CRI, Int32 len);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_GetColor_cXYZ(Int32 DevIdx, double[] XYZ, Int32 len);
        [DllImport("C:\\Program Files (x86)\\International Light\\SpectrlLight III\\SpectrILightC.dll")]
        private extern static Int32 ILT900_API_GetColor_Duv(Int32 DevIdx, double[] XYZ, Int32 len);

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
