using Intel.RealSense;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Threading;
using CatTrackerApp;
using System.Windows.Media;
using System.Windows.Controls;

namespace Sjoelbak
{
    internal class RealSenseL515 : IDepthSensor
    {
        private Pipeline pipe;
        private Colorizer colorizer;
        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        // Start the sensor.
        public void startDepthSensor(Image depthImg, Image colorImg)
        {
            initializeSensor(depthImg, colorImg);
        }

        // Stop the sensor.
        public void stopDepthSensor()
        {
            tokenSource.Cancel();
        }

        // Read the distance of all points within the 2 given boundries.
        public float[] readDistance(Point topLeft, Point bottomRight)
        {
            return CheckPixels(topLeft, bottomRight);
        }

        // Updating the image of both the depth and the color.
        private static Action<VideoFrame> UpdateImage(System.Windows.Controls.Image img)
        {
            var bmap = img.Source as WriteableBitmap;
            return new Action<VideoFrame>(frame =>
            {
                var rect = new Int32Rect(0, 0, frame.Width, frame.Height);
                bmap.WritePixels(rect, frame.Data, frame.Stride * frame.Height, frame.Stride);
            });
        }

        // Setting up the profile of both the depth and color image. Then start displaying and updating them with the given images.
        private void initializeSensor(Image imgDepth, Image imgColor)
        {
            try
            {
                Action<VideoFrame> updateDepth;
                Action<VideoFrame> updateColor;

                // The colorizer processing block will be used to visualize the depth frames.
                colorizer = new Colorizer();

                // Create and config the pipeline to strem color and depth frames.
                pipe = new Pipeline();

                using (var ctx = new Context())
                {
                    var devices = ctx.QueryDevices();
                    var dev = devices[0];

                    var sensors = dev.QuerySensors();
                    var depthSensor = sensors[0];
                    var colorSensor = sensors[1];

                    var depthProfile = depthSensor.StreamProfiles
                                        .Where(p => p.Stream == Stream.Depth)
                                        .OrderBy(p => p.Framerate)
                                        .Select(p => p.As<VideoStreamProfile>()).First();

                    var colorProfile = colorSensor.StreamProfiles
                                        .Where(p => p.Stream == Stream.Color)
                                        .OrderBy(p => p.Framerate)
                                        .Select(p => p.As<VideoStreamProfile>()).First();

                    var cfg = new Config();
                    cfg.EnableStream(Stream.Depth, 320, 240, depthProfile.Format, depthProfile.Framerate);
                    cfg.EnableStream(Stream.Color, colorProfile.Width, colorProfile.Height, colorProfile.Format, colorProfile.Framerate);


                    var pp = pipe.Start(cfg);

                    SetupWindow(pp, out updateDepth, out updateColor, imgDepth, imgColor);
                }
                Task.Factory.StartNew(() =>
                {
                    while (!tokenSource.Token.IsCancellationRequested)
                    {
                        using (var frames = pipe.WaitForFrames())
                        {
                            var colorFrame = frames.ColorFrame.DisposeWith(frames);
                            var depthFrame = frames.DepthFrame.DisposeWith(frames);

                            var colorizedDepth = colorizer.Process<VideoFrame>(depthFrame).DisposeWith(frames);

                            App.Current.Dispatcher.Invoke(DispatcherPriority.Render, updateDepth, colorizedDepth);
                            App.Current.Dispatcher.Invoke(DispatcherPriority.Render, updateColor, colorFrame);

                            App.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                String depth_dev_sn = depthFrame.Sensor.Info[CameraInfo.SerialNumber];
                            }));
                        }
                    }
                }, tokenSource.Token);
            }
            catch (ObjectDisposedException)
            {
                // Sometimes happens on resetting a throw, this doesn't influence anything.
                // The frames are supposed to be disposed of after being used.
            }
            catch (Exception)
            {
                Application.Current.Shutdown();
            }
        }

        // Creating the map to update the images for both profiles.
        private void SetupWindow(PipelineProfile pipelineProfile, out Action<VideoFrame> depth, out Action<VideoFrame> color, Image imgDepth, Image imgColor)
        {
            using (var vsp = pipelineProfile.GetStream(Stream.Depth).As<VideoStreamProfile>())
                imgDepth.Source = new WriteableBitmap(vsp.Width, vsp.Height, 96d, 96d, PixelFormats.Rgb24, null);
            depth = UpdateImage(imgDepth);

            using (var vsp = pipelineProfile.GetStream(Stream.Color).As<VideoStreamProfile>())
                imgColor.Source = new WriteableBitmap(vsp.Width, vsp.Height, 96d, 96d, PixelFormats.Rgb24, null);
            color = UpdateImage(imgColor);
        }

        // Checking distance all the pixels 1by1
        private float[] CheckPixels(Point topLeft, Point bottomRight)
        {
            int num = -1;
            // Array to store all point data. size = xdif * ydif
            int arrayMax = Convert.ToInt32((bottomRight.X - topLeft.X) * (bottomRight.Y - topLeft.Y));
            float[] distArray = new float[arrayMax];

            using (var frames = pipe.WaitForFrames())
            using (var depth = frames.DepthFrame)
            {
                for (int x = (int)topLeft.X; x < bottomRight.X; x++) // Check Width.
                {
                    for (int y = (int)topLeft.Y; y < bottomRight.Y; y++) // Check Height. 
                    {
                        num++;
                        distArray[num] = depth.GetDistance(x, y);
                    }
                }
                frames.Dispose();
            }
            return distArray;
        }

        // Get the distance on 1 single pixel.
        private float GetDistance(int x, int y)
        {
            float num = 0;
            using (var frames = pipe.WaitForFrames())
            using (var depth = frames.DepthFrame)
            {
                num = depth.GetDistance(x, y);
                depth.Dispose();
                frames.Dispose();
            }
            return num;
        }
    }
}
