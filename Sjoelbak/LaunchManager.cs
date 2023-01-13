using DistRS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Sjoelbak
{
    internal class LaunchManager
    {
        // Communication to arduino.
        private ICommunication launcherCom;
        // Commands for the arduino.
        private const string aimLauncherToZone1 = "#1%";
        private const string aimLauncherToZone2 = "#2%";
        private const string aimLauncherToZone3 = "#3%";
        private const string aimLauncherToZone4 = "#4%";
        private const string prepareLauncher = "#5%";
        private const string fireLauncher = "#6%";

        private int lastAngle = 0;

        public LaunchManager()
        {
            launcherCom = new SerialCommunication();
            ConnectSerial();
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
                // Prepare the shooting mechanism.
                launcherCom.SendMessage(prepareLauncher);
                // Aim the mechanism towards the right goal.
                launcherCom.SendMessage(DetermineNextAngle());
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
        public void ConnectSerial()
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

        private string DetermineNextAngle()
        {
            // Aim to the next one, when reached the last start with the first again.
            lastAngle++;
            if (lastAngle > 4)
            {
                lastAngle = 1;
            }
            // If it fails to find a next angle, always go for the highest point count.
            string val = aimLauncherToZone4;

            // Match the new angle with the right command.
            switch (lastAngle)
            {
                case 1:
                    val = aimLauncherToZone1;
                    break;
                case 2:
                    val = aimLauncherToZone2;
                    break;
                case 3:
                    val = aimLauncherToZone3;
                    break;
                case 4:
                    val = aimLauncherToZone4;
                    break;
            }
            return val;
        }
    }
}
