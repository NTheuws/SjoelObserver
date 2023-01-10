using System.Windows;
using System.Windows.Controls;

namespace Sjoelbak
{
    internal interface IDepthSensor
    {
        // Call to start the sensor, Give the image to display both the depth and color view of the sensor.
        void StartDepthSensor(Image depthImg, Image colorImg);

        // Call to stop the sensor.
        void StopDepthSensor();

        // Reads the distances on each point between topleft an bottomright in X and Y axis, then returns the array with the data. 
        float[] ReadDistance(Point topLeft, Point bottomRight);

    }
}
