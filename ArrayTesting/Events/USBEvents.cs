using System;
using System.Windows;
using System.ComponentModel;
using System.Management;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArrayTesting
{
    public partial class MainWindow
    {
        const String pidvid_demo = "USB\\VID_03EB&PID_2404"; // demo
        const String pidvid_spec = "USB\\VID_1992&PID_0667"; // spec
        /**
         *                 string vid = "1992", pid = "0667"; //  VID_1992 & PID_0667
         */
        //const String pidvidBootloader8x8v2 = "USB\\VID_03EB&PID_2FE5";
        //const String pidvidBootloader8x8v2_256kb = "USB\\VID_03EB&PID_2FEC";
        //const String pidvidBootloader16x16m = "USB\\VID_03EB&PID_2FEB";

        public async void DeviceInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            var mbo = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string ComAndType = (string)mbo["Caption"];
            int pos1 = ComAndType.IndexOf("(") + 1;
            int pos2 = ComAndType.IndexOf(")") - pos1;
            //string COMPort = ComAndType.Substring(pos1, pos2);


            switch (((string)mbo["DeviceID"]).Substring(0, 21))

            {
                case pidvid_demo:
                    await Application.Current.Dispatcher.BeginInvoke((Action)(() => InitArray()));
                    break;
                case pidvid_spec:
                    break;
                    await Application.Current.Dispatcher.BeginInvoke(new Func<Task>(async () =>
                    {
                        await SpecShutdown();
                        UpdateDarkScanStatus();
                        await InitSpecAsync();
                        UpdateSpecText();
                    }));

                default:
                    break;

            }
        }


        public async void DeviceRemovedEvent(object sender, EventArrivedEventArgs e)
        {
            var mbo = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string ComAndType = (string)mbo["Caption"];
            int pos1 = ComAndType.IndexOf("(") + 1;
            int pos2 = ComAndType.IndexOf(")") - pos1;
            //string COMPort = ComAndType.Substring(pos1, pos2);


            switch (((string)mbo["DeviceID"]).Substring(0, 21))
            {

                case pidvid_demo:
                    await Application.Current.Dispatcher.BeginInvoke((Action)(() => ArrayShutdown()));
                    break;
                case pidvid_spec:
                    await Application.Current.Dispatcher.BeginInvoke((Action)(() => SpecShutdown()));
                    break;


                default:
                    break;

            }
        }

        public void BackgroundWorkerStart()
        {
            BackgroundWorker bgwDriveDetector = new BackgroundWorker();
            bgwDriveDetector.DoWork += bgwDriveDetector_DoWork;
            bgwDriveDetector.RunWorkerAsync();
        }

        void bgwDriveDetector_DoWork(object sender, DoWorkEventArgs e)
        {
            var insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            var insertWatcher = new ManagementEventWatcher(insertQuery);
            insertWatcher.EventArrived += DeviceInsertedEvent;
            insertWatcher.Start();

            var removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            var removeWatcher = new ManagementEventWatcher(removeQuery);
            removeWatcher.EventArrived += DeviceRemovedEvent;
            removeWatcher.Start();
        }
    }
}
