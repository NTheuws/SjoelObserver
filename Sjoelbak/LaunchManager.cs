using DistRS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Sjoelbak
{
    internal class LaunchManager
    {
        // Communication to arduino.
        private SerialCommunication launcherCom;
        // Commands for the arduino.
        private const string aimLauncherToZone1 = "#1%";
        private const string aimLauncherToZone2 = "#2%";
        private const string aimLauncherToZone3 = "#3%";
        private const string aimLauncherToZone4 = "#4%";
        private const string prepareLauncher = "#5%";
        private const string fireLauncher = "#6%";

        public LaunchManager()
        {
            launcherCom = new SerialCommunication();
            ConnectArduino();
        }

        // Return if the arduino is connected or not.
        public bool GetConnectionState() 
        {
            return launcherCom.IsConnected();
        }

        // Send the prepare command to the arduino.
        public void PrepareLauncher()
        {
            if (GetConnectionState())
            {
                launcherCom.SendMessage(prepareLauncher);
            }
        }

        // Send the fire command to the arduino.
        public void FireLauncher()
        {
            if (GetConnectionState())
            {
                launcherCom.SendMessage(fireLauncher);
            }
        }

        // Connect the arduino.
        public void ConnectArduino()
        {
            SerialCommunication com = new SerialCommunication();
            string[] ports = com.GetAvailablePortNames();
            try
            {
                launcherCom = new SerialCommunication(ports[0]);
                launcherCom.Connect();
            }
            catch (IndexOutOfRangeException)
            {
                MessageBox.Show("Make sure the arduino is connected.");
            }
        }
    }
}
