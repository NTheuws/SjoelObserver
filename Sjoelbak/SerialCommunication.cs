using Sjoelbak;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistRS
{
    class SerialCommunication : ICommunication
    {
        private SerialPort serialPort;

        public SerialCommunication()
        {
            serialPort = new SerialPort();
            serialPort.BaudRate = 9600;
        }
        public SerialCommunication(String port)
        {
            serialPort = new SerialPort();
            serialPort.BaudRate = 9600;
            serialPort.PortName = port;
        }

        public void Connect()
        {
            if (!serialPort.IsOpen)
            {
                serialPort.Open();
                if (serialPort.IsOpen)
                {
                    serialPort.DiscardInBuffer();
                    serialPort.DiscardOutBuffer();
                }
            }
        }

        public void Disconnect()
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }

        public string[] GetAvailablePortNames()
        {
            return SerialPort.GetPortNames();
        }

        public bool IsConnected()
        {
            return serialPort.IsOpen;
        }

        public bool SendMessage(string message)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Write(message);
                return true;
            }
            return false;
        }
    }
}
