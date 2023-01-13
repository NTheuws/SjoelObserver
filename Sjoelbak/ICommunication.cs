using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sjoelbak
{
    internal interface ICommunication
    {
        // Connects to the device.
        void Connect();

        // Disconnects from the connected device.
        void Disconnect();

        // Used to get the connection state, bool returns if its connected or not.
        bool IsConnected();

        // Used to send a message, bool returns if it succeeded or not.
        bool SendMessage(string message);
    }
}
