using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using Sjoelbak;

namespace DistRS
{
    public partial class MainWindow : Window
    {
        // Values for the pixel size.
        private const int height = 240;
        private const int width = 320;
        private const int pixelDivider = 2; // every x'th pixel will be checked when calculating differences.    

        // Callibration variables.
        private int callibrationClickCount = 0;
        private Point callibrationTopLeft = new Point(0f, 0f); // Minimal values possible.
        private Point callibrationBottomRight = new Point(width, height); // Maximum values possible.

        // Thread to prevent blocking the screen from being updated while measuring.
        private System.Threading.Thread observeThread;

        // Manages the launching mechanism and the connection to the arduino.
        private LaunchManager launchManager;
        // Manages everything related to the observation of the board.
        private TrajectObserver trajectObserver;

        public MainWindow()
        {
            InitializeComponent();
            // Create an observer to start the sensor.
            trajectObserver = new TrajectObserver(pixelDivider, imgDepth, imgColor);
        }

        // Reset current values to be able to start the next throw.
        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            if (!trajectObserver.MeasureLooping)
            {
                ClearCanvasTrajectory();
            }
        }

        // Manually connect to the arduino if it failed before.
        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            // Make sure the connection isnt already there.
            if (!launchManager.GetConnectionState())
            {
                launchManager.ConnectSerial();

                // Check connection status again.
                if (launchManager.GetConnectionState())
                {
                    BtnConnect.Visibility = Visibility.Hidden;
                    BtnFire.Visibility = Visibility.Visible;
                    BtnLoopMeassure.Visibility = Visibility.Visible;
                }
            }
        }

        // First two clicks are used to determine the playing field.
        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // divide the axis to increase performance, divider determines the amount of pixels measured.
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

                    // Transform the canvas to start on the topleft coordinate.
                    translate.X = callibrationTopLeft.X * pixelDivider;
                    translate.Y = callibrationTopLeft.Y * pixelDivider;

                    bool result = trajectObserver.CreateCallibration(callibrationTopLeft, callibrationBottomRight);
                    if (!result)
                    {
                        MessageBox.Show("Make sure the depthsensor is connected.");
                    }
                    else
                    {
                        tbText.Text = "Callibration has been made.";
                    }

                    // Create a launchManager to manage the launcher and connection to the arduino.
                    launchManager = new LaunchManager();
                    //Show the correct buttons 
                    if (launchManager.GetConnectionState())
                    {
                        // Connection has been made.
                        BtnFire.Visibility = Visibility.Visible;
                        BtnLoopMeassure.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // Connection failed.
                        BtnConnect.Visibility = Visibility.Visible;
                    }
                    
                    break;
            }
            callibrationClickCount++;
        }

        // Button to loop through the meassure mode.
        private void ButtonMeassureLoop_Click(object sender, RoutedEventArgs e)
        {
            if (!trajectObserver.MeasureLooping)
            {
                // Prepare firing mechanism only when connected.
                if (launchManager.GetConnectionState())
                {
                    launchManager.PrepareLauncher();
                }

                ClearCanvasTrajectory(); // Clear the map so it doesn't add another trajectory on top.
                trajectObserver.PlaceFinalDot = false;
                trajectObserver.MeasureLooping = true;
                // Start the loop in another thread.
                observeThread = new System.Threading.Thread(MeassureLoop);
                observeThread.IsBackground = true;
                observeThread.Start();
                tbText.Text = "Started Measuring";
            }
            else
            {
                // Loop one final time and place a final dot stamp.
                trajectObserver.PlaceFinalDot = true;
               trajectObserver.MeasureLooping = false;
                tbText.Text = "Stopped Measuring (manual)";
            }
        }
        // Thread to continiously loop scanning and comparing distances.
        private void MeassureLoop()
        {
            while (trajectObserver.MeasureLooping)
            {
                // Get the last frames values from the sensor and compare them to the callibration.
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    trajectObserver.ComparePixels();
                });
                // If the disc has come to a standstill or dissapears out of vision, itll be seen as done.
                if (trajectObserver.TrajectoryDone)
                {
                    trajectObserver.PlaceFinalDot = true;
                    trajectObserver.ComparePixels();
                    trajectObserver.MeasureLooping = false;
                }
            }
            // Stop looping to check frames.
            trajectObserver.MeasureLooping = false;

            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                tbText.Text = "Stopped Measuring";

                DiscTrajectory tempTrajectory = trajectObserver.GetTrajectory();
                DrawTrajectory(tempTrajectory);
            });
            int score = trajectObserver.FinalizeCurrentTrajectory();
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                // See if the player has scored and if so show it.
                int lastScore = Convert.ToInt32(tbPlayerScore.Text);
                tbText.AppendText("Scored: (+" + (score - lastScore).ToString() + ")");

                tbPlayerScore.Text = score.ToString();
            });
            
            UpdateSlider();
            observeThread.Abort();
        }

        private void UpdateSlider()
        {
            // Update the slider
            this.Dispatcher.Invoke(() =>
            {
                int trajectoriesCount = trajectObserver.GetTotalTrajectories();
                lbShownIndex.Content = "Trajectory 0 / " + trajectoriesCount;
                indexSlider.Maximum = trajectoriesCount;
                indexSlider.Value = 0;
            });
        }

        // Clear the current canvas of all the drawn items.
        private void ClearCanvasTrajectory()
        {
            CanvasMap.Children.Clear();
            indexSlider.Value = 0;
        }

        // When the slider value has been moved.
        private void indexSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int index = Convert.ToInt32(Math.Round(indexSlider.Value - 1)); // -1 To use the index within the list.
            lbShownIndex.Content = "Trajectory " + (index + 1) + " / " + trajectObserver.GetTotalTrajectories(); // Show currently selected trajectory.

            // When meassuring is in process go back to 0;
            if (trajectObserver.MeasureLooping)
            {
                indexSlider.Value = 0;
            }
            // Show the selected trajectory.
            else
            {
                CanvasMap.Children.Clear();
                if (index >= 0)
                {
                    DiscTrajectory tempTrajectory = trajectObserver.GetTrajectory(index);
                    //Show the hovered index trajectory.
                    DrawTrajectory(tempTrajectory);
                }
            }
        }

        // Draw trajectory in canvas.
        private void DrawTrajectory(DiscTrajectory traject)
        {
            if (traject != null)
            {
                foreach (Line item in traject.GetTrajectoryLines())
                {
                    CanvasMap.Children.Add(item);
                }
                foreach (Rectangle item in traject.GetFinalDot())
                {
                    CanvasMap.Children.Add(item);
                }
            }
        }

        // Turn off the sensor on closing the window.
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            trajectObserver.StopDepthSensor();
        }

        // Button to fire the disc from the launcher.
        private void BtnFire_Click(object sender, RoutedEventArgs e)
        {
            // Only send when connected.
            if(launchManager.GetConnectionState())
            {
                launchManager.FireLauncher();
            }
        }

        // Finalize the score of the current player. 
        private void BtnNextPlayer_Click(object sender, RoutedEventArgs e)
        {
            int score = trajectObserver.EndPlayerTurn();
            tbPlayerScore.AppendText(score.ToString());
        }
    }
}