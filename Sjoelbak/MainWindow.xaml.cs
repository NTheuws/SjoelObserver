using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Intel.RealSense;
using Stream = Intel.RealSense.Stream;
using System.Windows.Threading;
using System.Diagnostics;
using Sjoelbak;
using System.Collections;
using System.Xml;
using System.Reflection;

namespace DistRS
{
    public partial class MainWindow : Window
    {
        // Values for the pixel size.
        const int height = 240;
        const int width = 320;
        const int arraySize = ((width / 5) * (height / 5));
        float[] distArray = new float[arraySize];
        float[] callibrationArray = new float[arraySize];
        const int pixelDivider = 2; // every x th pixel will be checked when calculating differences.

        // Callibration variables.
        int callibrationClickCount = 0;
        Point callibrationTopLeft = new Point(0f, 0f); // Minimal values possible.
        Point callibrationBottomRight = new Point(width, height); // Maximum values possible.
        bool callibrationCornersSet = false;

        // Lists to keep the past throws saved.
        List<Point> discPoints = new List<Point>();  // Array of the recorded points within 1 throw.
        List<Canvas> trajectories = new List<Canvas>(); // Trajectory of each throw, so it can be shown later.
        List<DiscTrajectory> discTrajectories = new List<DiscTrajectory>();
        DiscTrajectory discTrajectory;

        // Variables for the minimal and maximum size of the lines drawn.
        int xmin = 0;
        int ymin = 0;
        int xmax;
        int ymax;

        // Thread to prevent blocking the screen from being updates while measuring.
        System.Threading.Thread observeThread;

        // States to make sure the loop ends well.
        bool measureLooping = false;
        bool measureLoopEnding = false;
        bool placeFinalDot = false;

        // Communication to arduino.
        private SerialCommunication launcherCom;

        RealSenseL515 depthSensor;
        public MainWindow()
        {
            InitializeComponent();

            // Create an instance of the sensor.
            depthSensor = new RealSenseL515();
            // Initialize
            depthSensor.startDepthSensor(imgDepth, imgColor);
        }

        // Reset current values to be able to start the next throw.
        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            ClearCanvasTrajectory();
        }

        private void ComparePixels()
        {
            // 768 pixels 32x24.
            // Y-axis first followed by the X-axis. Top left to bottom left then moving one to the right.
            int x = 0; // Keep track of the x coordinate of the current pixel.
            int y = 0;  // Keep track of the y coordinate of the current pixel.

            float noiseSupression = 0.01f; // Variable to prevent the noise of the sensor.

            Rectangle[,] rectangles = new Rectangle[(int)(callibrationBottomRight.X - callibrationTopLeft.X), (int)(callibrationBottomRight.Y - callibrationTopLeft.Y)];

            // Initial point is 1 out of the range of the callibration.
            // When a new point is visible this one will always be taken over.
            Point tempDiscPoint = new Point(-1, -1);

            // Start by clearing the last canvas if not looping.
            if (!measureLooping && !measureLoopEnding)
            {
                CanvasMap.Children.Clear();
            }

            for (int i = 0; i < callibrationArray.Length; i++)
            {
                // Count amount of pixels that are closer now compared to before.
                if (callibrationArray[i] != 0
                    && distArray[i] != 0
                    && distArray[i] + noiseSupression < callibrationArray[i]) 
                {
                    // Find a point for the trajectory.
                    // Always take the same point of the disc for each state.
                    // The top-most(priority), right-most(secondary) will be used.
                    // Y axis is flipped.
                    if (measureLooping)
                    {
                        if (y > tempDiscPoint.Y
                            || (y == tempDiscPoint.Y && x > tempDiscPoint.X))
                        {
                            // Take into account that this starts with 0,0.
                            // Add or substract the starting points to get an accurate point.

                            // Starts at callibrationTopLeft.X (Xmin) and adds the amount of pixels on the x-axis. 
                            tempDiscPoint.X = x;
                            // Starts at callibrationTopLeft.Y (Ymax) and substracts the amount of pixels on the y-axis.
                            tempDiscPoint.Y = y;
                        }
                    }
                    // Only when the throw has been completed and the disc has come to a stand still.
                    if (placeFinalDot)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            // Save for the drawing.
                            rectangles[x, y] =
                                    new Rectangle()
                                    {
                                        Width = 10,
                                        Height = 10,
                                        Fill = Brushes.Gold,
                                        RenderTransform = new TranslateTransform(x * pixelDivider, y * pixelDivider)
                                    };
                            // Draw the map.
                            CanvasMap.Children.Add(rectangles[x, y]); // Add to the canvas.
                            discTrajectory.AddDot(rectangles[x, y]); // Add to the trajectory.
                        });
                    }
                }
                y++;
                // Go through it row to row
                if ((i + 1) % (callibrationBottomRight.Y - callibrationTopLeft.Y) == 0)
                {
                    // Next row.
                    y = 0;
                    x++;
                }
            }
            // Only when there has been a new point add it to the array.
            if (tempDiscPoint.X != -1
                && tempDiscPoint.Y != -1
                && tempDiscPoint != null)
            {
                // Only add it when the coordinates are different then the ones before.
                // Slight variations happen so this is to prevent spam in points.
                Point lastPoint;
                int lastItem = discPoints.Count;

                if (lastItem > 0)
                {
                    lastPoint = discPoints.ElementAt(lastItem - 1);
                }
                else
                {
                    lastPoint = new Point(0, 0);
                }
                // Only create a new point when the distance is a bit different.
                if (lastPoint != null
                    || lastPoint.X + 5 < tempDiscPoint.X
                    || lastPoint.X - 5 > tempDiscPoint.X
                    || lastPoint.Y + 5 < tempDiscPoint.Y
                    || lastPoint.Y - 5 > tempDiscPoint.Y)
                {
                    // Add the point to the list.
                    discPoints.Add(tempDiscPoint);

                    // Draw a line between the points
                    if (lastItem > 0)
                    {
                        Application.Current.Dispatcher.Invoke((Action)delegate
                        {
                            Line tempLine = new Line()
                            {
                                Stroke = System.Windows.Media.Brushes.Black,
                                // Every second dot is counted, therefore to map it itll need to be multiplied by 2.
                                X1 = lastPoint.X * pixelDivider,
                                Y1 = lastPoint.Y * pixelDivider,
                                X2 = tempDiscPoint.X * pixelDivider,
                                Y2 = tempDiscPoint.Y * pixelDivider,
                                StrokeThickness = 5
                            };

                            // When its the first line trace it to the beginning of the field.
                            if (lastItem == 1)
                            {
                                try
                                {
                                    //rc = Δy ⁄ Δx
                                    double rc = (tempLine.Y1 - tempLine.Y2) / (tempLine.X1 - tempLine.X2);

                                    //Calculate the point where the disc comes from to prevent a larger gap from being created.
                                    double xDif = xmax - tempLine.X2;
                                    double yIncrease = xDif * rc;
                                    tempLine.X1 = xmax;
                                    tempLine.Y1 = tempLine.Y2 + yIncrease;
                                }
                                // Sometimes it will think the new point is placed at 'infinity' which is caused by a bad calibration or someone walking through the field.
                                catch (ArgumentException)
                                {
                                    MessageBox.Show("It wasn't calibrated correctly, please try again.");
                                }
                            }
                            CanvasMap.Children.Add(tempLine); // Add to the currently shown canvas.
                            discTrajectory.AddLine(tempLine); // Add line to the trajectory.
                        });
                    }
                }
            }
        }

        // Connect to the arduino and start playing.
        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            SerialCommunication com = new SerialCommunication();
            string[] ports = com.GetAvailablePortNames();
            launcherCom = new SerialCommunication(ports[2]);
            launcherCom.Connect();
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Half of it to check on every other pixel instead of all of them.
            Point p = Mouse.GetPosition(CanvasMap);
            p.X = Math.Round(p.X / pixelDivider);
            p.Y = Math.Round(p.Y / pixelDivider);

            switch (callibrationClickCount)
            {
                case 0: // First click.
                    callibrationTopLeft = p;
                    tbText.Text = "Click on the bottom right most point.";

                    break;
                case 1: // Second click.
                    callibrationBottomRight = p;
                    callibrationCornersSet = true;

                    int newArraySize = (int)((callibrationBottomRight.X - callibrationTopLeft.X) * (callibrationBottomRight.Y - callibrationTopLeft.Y));
                    try
                    {
                        distArray = new float[newArraySize];
                        callibrationArray = new float[newArraySize];
                    }
                    catch(OverflowException)
                    {
                        MessageBox.Show("Make sure to make a calibration before measuring.");
                    }

                    translate.X = callibrationTopLeft.X * pixelDivider;
                    translate.Y = callibrationTopLeft.Y * pixelDivider;

                    // Max values are multiplied by 2 since the pixelcount is divided by 2.
                    xmax = Convert.ToInt32(callibrationBottomRight.X - callibrationTopLeft.X) * pixelDivider;
                    ymax = Convert.ToInt32(callibrationBottomRight.Y - callibrationTopLeft.Y) * pixelDivider;

                    // Create a callibration frame.
                    callibrationArray = depthSensor.readDistance(callibrationTopLeft, callibrationBottomRight);
                    tbText.Text = "Callibration done.";
                    break;
            }
            callibrationClickCount++;
        }

        // Button to loop through the meassure mode.
        private void ButtonMeassureLoop_Click(object sender, RoutedEventArgs e)
        {
            if (!measureLooping)
            {
                discTrajectory = new DiscTrajectory(); // new trajectory
                measureLooping = true;
                placeFinalDot = false;
                // Start the loop in another thread.
                observeThread = new System.Threading.Thread(MeassureLoop);
                observeThread.IsBackground = true;
                observeThread.Start();
                tbText.Text = "Started Measuring";
            }
            else
            {
                // Loop one final time and place a final dot stamp.
                placeFinalDot = true;
                measureLooping = false;
                measureLoopEnding = true;
                tbText.Text = "Stopped Measuring";

                SaveTrajectory();
            }
        }
        // Thread to continiously loop scanning and comparing distances.
        private void MeassureLoop()
        {
            while (measureLooping)
            {
                // Get the last frames values from the sensor.
                distArray = depthSensor.readDistance(callibrationTopLeft, callibrationBottomRight);
                // Compare the pixels to the callibration frame.
                ComparePixels();
            }
            observeThread.Abort();
            measureLoopEnding = false;
            ComparePixels(); 
        }

        private void SaveTrajectory()
        {
            // Save the canvas for later.
            trajectories.Add(CanvasMap);
            // Save the trajectory to the list for later use.
            discTrajectories.Add(discTrajectory);

            // Update the slider
            this.Dispatcher.Invoke(() =>
            {
                lbShownIndex.Content = "Trajectory 0 / " + trajectories.Count;
                indexSlider.Maximum = trajectories.Count;
                indexSlider.Value = 0;
            });
        }

        // Clear the current canvas of all the drawn items.
        private void ClearCanvasTrajectory()
        {
            CanvasMap.Children.Clear();
            discPoints.Clear();
            tbText.Text = "Canvas has been reset.";
            indexSlider.Value = 0;
        }

        // When the slider value has been moved.
        private void indexSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int index = Convert.ToInt32(Math.Round(indexSlider.Value - 1)); // -1 To use the index within the list.
            lbShownIndex.Content = "Trajectory " + (index + 1) + " / " + trajectories.Count; // Show currently selected trajectory.

            // When meassuring is in process go back to 0;
            if (measureLooping)
            {
                indexSlider.Value = 0;
            }
            // Show the selected trajectory.
            else
            {
                CanvasMap.Children.Clear();
                if (index >= 0)
                {
                    //Show the hovered index trajectory.
                    foreach (Line item in discTrajectories[index].GetTrajectoryLines())
                    {
                        CanvasMap.Children.Add(item);
                    }
                    foreach (Rectangle item in discTrajectories[index].GetFinalDot())
                    {
                        CanvasMap.Children.Add(item);
                    }
                }
            }
        }
    }
}