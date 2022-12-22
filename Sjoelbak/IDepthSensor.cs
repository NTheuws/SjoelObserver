using Intel.RealSense;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Sjoelbak
{
    internal interface IDepthSensor
    {
        // Call to start the sensor, Give the image to display both the depth and color view of the sensor.
        void startDepthSensor(Image depthImg, Image colorImg);

        // Call to stop the sensor.
        void stopDepthSensor();

        // Reads the distances on each point between topleft an bottomright in X and Y axis, then returns the array with the data. 
        float[] readDistance(Point topLeft, Point bottomRight);

    }
}
