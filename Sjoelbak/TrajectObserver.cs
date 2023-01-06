﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sjoelbak
{
    internal class TrajectObserver
    {
        // Variables for the minimal and maximum size of the lines drawn.
        int xmin = 0;
        int ymin = 0;
        int xmax;
        int ymax;
        int pixelDivider;

        private Line tempLine;
        private int difCounter = 0;
        public bool TrajectoryDone = false;

        bool measureLooping = false; // True when actively tracking trajectory.
        bool placeFinalDot = false; // True when the final dot will be spotted.

        IDepthSensor sensor;
        Point callibrationTopLeft;
        Point callibrationBottomRight;
        float[] callibrationArray;
        float[] distArray;

        int sinceLastLine; // Counts the amount of frames where there hasnt been a new line for the trajectory.

        List<Point> discPoints = new List<Point>();  // Array of the recorded points within 1 throw.
        List<DiscTrajectory> discTrajectories = new List<DiscTrajectory>();
        DiscTrajectory discTrajectory = new DiscTrajectory();

        public TrajectObserver(int pixelDiv, Image depth, Image color) 
        {
            pixelDivider = pixelDiv;

            sensor = new RealSenseL515();
            sensor.startDepthSensor(depth, color);
        }

        // Measuring loop is on/off.
        public void MeasureLoopToggle(bool state)
        {
            measureLooping= state;
        }

        // FinalDot placement is on/off.
        public void FinalDotToggle(bool state)
        {
            placeFinalDot = state;
        }

        // Get the current trajectory.
        public DiscTrajectory GetTrajectory()
        {
            return discTrajectory;
        }

        // Get the trajectory of a certain throw.
        public DiscTrajectory GetTrajectory(int index)
        {
            return discTrajectories[index];
        }

        // Get the final dot of the current trajectory
        public List<Rectangle> GetFinalDot()
        {
            return discTrajectory.GetFinalDot();
        }

        // Get measureLoop.
        public bool GetMeasureLoopState()
        {
            return measureLooping;
        }

        // Return the total amount of trajectories.
        public int GetTotalTrajectories()
        {
            return discTrajectories.Count;
        }

        // Signal that the current throw has ended and the next one can be started at any time.
        public void FinalizeCurrentTrajectory()
        {
            // Add the current one to the list of trajectories.
            discTrajectories.Add(discTrajectory);
            // Create a new one to keep track of the upcoming trajectory.
            discTrajectory = new DiscTrajectory();
            // Clear the current points.
            discPoints.Clear();
            TrajectoryDone = false;
        }

        // Stop the depthsensor.
        public void StopDepthSensor()
        {
            sensor.stopDepthSensor();
        }

        public bool CreateCallibration(Point calTopLeft, Point calBottomRight)
        {
            callibrationTopLeft = calTopLeft;
            callibrationBottomRight = calBottomRight;

            int newArraySize = (int)((callibrationBottomRight.X - callibrationTopLeft.X) * (callibrationBottomRight.Y - callibrationTopLeft.Y));
            try
            {
                distArray = new float[newArraySize];
                callibrationArray = new float[newArraySize];
            }
            catch (OverflowException)
            {
                MessageBox.Show("Make sure to make a calibration before measuring.");
            }

            // Max values are multiplied by 2 since the pixelcount is divided by 2.
            xmax = Convert.ToInt32(callibrationBottomRight.X - callibrationTopLeft.X) * pixelDivider;
            ymax = Convert.ToInt32(callibrationBottomRight.Y - callibrationTopLeft.Y) * pixelDivider;

            try
            {
                // Create a callibration frame.
                callibrationArray = sensor.readDistance(callibrationTopLeft, callibrationBottomRight);
            }
            catch(Exception)
            {
                return false;
            }

            return true;
        }

        // Getting distances from the sensor and comparing them to the original callibration.
        public void ComparePixels()
        {
            difCounter = 0;
            sinceLastLine++;

            if (measureLooping)
            {
                distArray = sensor.readDistance(callibrationTopLeft, callibrationBottomRight);
            }

            // 320x240 res, standard divider = 2. 
            // This gives a total pixelcount of (320/2) * (240/2) = 19200.
            // Y-axis first followed by the X-axis. Top left to bottom left then moving one to the right.
            int x = 0; // Keep track of the x coordinate of the current pixel.
            int y = 0;  // Keep track of the y coordinate of the current pixel.

            const float noiseSupression = 0.01f; // Variable to prevent the noise of the sensor.

            Rectangle[,] rectangles = new Rectangle[(int)(callibrationBottomRight.X - callibrationTopLeft.X), (int)(callibrationBottomRight.Y - callibrationTopLeft.Y)];

            // Initial point is 1 out of the range of the callibration.
            // When a new point is visible this one will always be taken over.
            Point tempDiscPoint = new Point(-1, -1);

            for (int i = 0; i < callibrationArray.Length; i++)
            {
                // For every pixel thats closer to the sensor than the callibration.
                if (callibrationArray[i] != 0
                    && distArray[i] != 0
                    && distArray[i] + noiseSupression < callibrationArray[i])
                {
                    difCounter++;

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
                    Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        if (placeFinalDot)
                        {
                            rectangles[x, y] =
                                        new Rectangle()
                                        {
                                            Width = 10,
                                            Height = 10,
                                            Fill = Brushes.Purple,
                                            RenderTransform = new TranslateTransform(x * pixelDivider, y * pixelDivider)
                                        };
                            discTrajectory.AddDot(rectangles[x, y]); // Add to the trajectory.
                        }
                    });
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
                    && ( (lastPoint.X + 5 < tempDiscPoint.X || lastPoint.X - 5 > tempDiscPoint.X)
                    || (lastPoint.Y + 5 < tempDiscPoint.Y || lastPoint.Y - 5 > tempDiscPoint.Y)))
                {
                    // Add the point to the list.
                    discPoints.Add(tempDiscPoint);

                    // Draw a line between the points
                    if (lastItem > 0)
                    {
                        Application.Current.Dispatcher.Invoke((Action)delegate {
                            tempLine = new Line();
                            tempLine.Stroke = System.Windows.Media.Brushes.Black;
                            // Every second dot is counted, therefore to map it itll need to be multiplied by 2.
                            tempLine.X1 = lastPoint.X * pixelDivider;
                            tempLine.Y1 = lastPoint.Y * pixelDivider;
                            tempLine.X2 = tempDiscPoint.X * pixelDivider;
                            tempLine.Y2 = tempDiscPoint.Y * pixelDivider;
                            tempLine.StrokeThickness = 5;
                        });
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
                        discTrajectory.AddLine(tempLine); // Add line to the trajectory.
                        sinceLastLine = 0;
                    }
                }
            }
            // Stop the looping when the disc hasnt been moving.
            if((difCounter == 0 || sinceLastLine > 50) && discTrajectory.GetTrajectoryLines().Count > 0) 
            {
                sinceLastLine = 0;
                TrajectoryDone = true;
            }   
        }
    }
}