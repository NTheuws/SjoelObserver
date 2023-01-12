using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sjoelbak
{
    internal class TrajectObserver
    {
        // Variables for the minimal and maximum size of the lines drawn.
        private int xmin = 0;
        private int ymin = 0;
        private int xmax;
        private int ymax;
        private int pixelDivider;

        // Score area dividers.
        private int upperDivider;
        private int middleDivider;
        private int lowerDivider;

        private Line tempLine;
        public bool TrajectoryDone = false;

        private bool measureLooping = false; // True when actively tracking trajectory.
        private bool placeFinalDot = false; // True when the final dot will be spotted.

        // Variables for the depth sensor.
        private IDepthSensor sensor;
        private Point callibrationTopLeft;
        private Point callibrationBottomRight;
        private float[] callibrationArray;
        private float[] distArray;

        private int sinceLastLine; // Counts the amount of frames where there hasnt been a new line for the trajectory.

        private List<Point> discPoints = new List<Point>();  // Array of the recorded points within 1 throw.
        private List<DiscTrajectory> discTrajectories = new List<DiscTrajectory>();
        private DiscTrajectory discTrajectory = new DiscTrajectory();
        private List<PlayerScore> Scores = new List<PlayerScore>();
        private PlayerScore player1 = new PlayerScore();

        public TrajectObserver(int pixelDiv, Image depth, Image color) 
        {
            pixelDivider = pixelDiv;

            // Create and start the depthsensor.
            sensor = new RealSenseL515();
            sensor.Start(depth, color);
        }
        public bool PlaceFinalDot
        {
            get { return placeFinalDot; }
            set { placeFinalDot = value; }
        }
        public bool MeasureLooping
        {
            get { return measureLooping; }
            set { measureLooping = value; }
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

        // Return the total amount of trajectories.
        public int GetTotalTrajectories()
        {
            return discTrajectories.Count;
        }

        // Signal that the current throw has ended and the next one can be started at any time.
        public int FinalizeCurrentTrajectory()
        {
            // Determine if points have been scored.
            // When there's no final dot it means that the disc went out of the sensors view
            // Being either back towards the player or in one of the scoring areas.
            if (discTrajectory.GetFinalDot().Count == 0)
            {
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    Line lastLine = discTrajectory.GetTrajectoryLines().Last();

                    // Check if the disc moved towards the left side on the last line of the trajectory.
                    if (lastLine.X1 > lastLine.X2)
                    {
                        // rc = Δy ⁄ Δx
                        double rc = (lastLine.Y1 - lastLine.Y2) / (lastLine.X1 - lastLine.X2);

                        // Calculate the point where the disc ends up.
                        double xDif = lastLine.X2 - xmin;
                        double yIncrease = xDif * rc;
                        double yEndpoint = yIncrease + lastLine.Y2;

                        // Check scoring depending on Y coordinate.
                        // From furthest away to closest by the scoring is : 2, 4, 3, 1
                        if (yEndpoint < upperDivider)
                        {
                            // Disc went into 2.
                            player1.ScoredTwo();
                        }
                        else if (yEndpoint < middleDivider && yEndpoint > upperDivider)
                        {
                            // Disc went into 4.
                            player1.ScoredFour();
                        }
                        else if (yEndpoint < lowerDivider && yEndpoint > middleDivider)
                        {
                            // Disc went into 3.
                            player1.ScoredThree();
                        }
                        else if (yEndpoint > lowerDivider)
                        {
                            // Disc went into 1.
                            player1.ScoredOne();
                        }
                    }
                });
            }

            // Add the current one to the list of trajectories.
            discTrajectories.Add(discTrajectory);
            // Create a new one to keep track of the upcoming trajectory.
            discTrajectory = new DiscTrajectory();
            // Clear the current points.
            discPoints.Clear();
            TrajectoryDone = false;

            return player1.GetScore();
        }

        // Ending the player's turn and finalizing their score.
        public int EndPlayerTurn()
        {
            int tempScore = player1.GetScore();
            Scores.Add(player1);
            player1 = new PlayerScore();
            return tempScore;
        }

        // Stop the depthsensor.
        public void Stop()
        {
            sensor.Stop();
        }

        // Creating a callibrationframe at the start to be able to compare all other data to this.
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

            // Set score area zones.
            middleDivider = ymax / 2; // Is positioned halfway on the playfield.
            upperDivider = middleDivider / 2; // Positioned on a quarter.
            lowerDivider = middleDivider + upperDivider; // Positioned on 3rd quarter.

            try
            {
                // Create a callibration frame.
                callibrationArray = sensor.ReadDistance(callibrationTopLeft, callibrationBottomRight);
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
            sinceLastLine++;

            if (measureLooping)
            {
                distArray = sensor.ReadDistance(callibrationTopLeft, callibrationBottomRight);
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
                                // rc = Δy ⁄ Δx
                                double rc = (tempLine.Y1 - tempLine.Y2) / (tempLine.X1 - tempLine.X2);

                                // Calculate the point where the disc comes from to prevent a larger gap from being created.
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
            if(sinceLastLine > 30 && discTrajectory.GetTrajectoryLines().Count > 0) 
            {
                sinceLastLine = 0;
                TrajectoryDone = true;
            }   
        }
    }
}
