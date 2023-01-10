using Enterwell.Clients.Wpf.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ArrayTesting
{
    public partial class MainWindow
    {
        public void spectreNotInstalledNotification()
        {
            Action invokeNotifification = () =>
            {
                Manager.CreateMessage()
                              //  .Accent(new Brush());
                              .Dismiss().WithDelay(3000)
                              .Background("#1f1f1f")
                              .HasHeader($"SpectrlLight III not installed.")
                              .HasBadge("Error")
                              .Animates(true)
                              .AnimationInDuration(1)
                              .AnimationOutDuration(1)
                              .HasMessage($"Application not working")
                              .Dismiss().WithButton("Close", button => { })
                              .Queue();
            };
            Application.Current.Dispatcher.BeginInvoke(invokeNotifification);
        }

        public void customErrorNotification(string header, string message)
        {
            Action invokeNotifification = () =>
            {
                Manager.CreateMessage()
                              //  .Accent(new Brush());
                              .Dismiss().WithDelay(3000)
                              .Background("#1f1f1f")
                              .HasHeader(header)
                              .HasBadge("Error")
                              .Animates(true)
                              .AnimationInDuration(1)
                              .AnimationOutDuration(1)
                              .HasMessage(message)
                              .Dismiss().WithButton("Close", button => { })
                              .Queue();
            };
            Application.Current.Dispatcher.BeginInvoke(invokeNotifification);
        }
    }
}
