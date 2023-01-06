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
        const int pixelDivider = 2; // every x'th pixel will be checked when calculating differences.    

        // Callibration variables.
        int callibrationClickCount = 0;
        Point callibrationTopLeft = new Point(0f, 0f); // Minimal values possible.
        Point callibrationBottomRight = new Point(width, height); // Maximum values possible.
        bool callibrationCornersSet = false;

        // Thread to prevent blocking the screen from being updates while measuring.
        System.Threading.Thread observeThread;

        // Communication to arduino.
        private SerialCommunication launcherCom;

        TrajectObserver observer;

        public MainWindow()
        {
            InitializeComponent();
            // Create an observer to start the sensor.
            observer = new TrajectObserver(pixelDivider, imgDepth, imgColor);
        }

        // Reset current values to be able to start the next throw.
        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            if (!observer.GetMeasureLoopState())
            {
                ClearCanvasTrajectory();
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
                    callibrationCornersSet = true;

                    // Transform the canvas to start on the topleft coordinate.
                    translate.X = callibrationTopLeft.X * pixelDivider;
                    translate.Y = callibrationTopLeft.Y * pixelDivider;

                    bool result = observer.CreateCallibration(callibrationTopLeft, callibrationBottomRight);
                    if (!result)
                    {
                        MessageBox.Show("Make sure the depthsensor is connected.");
                    }
                    else
                    {
                        tbText.Text = "Callibration has been made.";
                    }
                    break;
            }
            callibrationClickCount++;
        }

        // Button to loop through the meassure mode.
        private void ButtonMeassureLoop_Click(object sender, RoutedEventArgs e)
        {
            if (!observer.GetMeasureLoopState())
            {
                CanvasMap.Children.Clear(); // Clear the map so it doesn't add another trajectory on top.
                observer.FinalDotToggle(false);
                observer.MeasureLoopToggle(true);
                // Start the loop in another thread.
                observeThread = new System.Threading.Thread(MeassureLoop);
                observeThread.IsBackground = true;
                observeThread.Start();
                tbText.Text = "Started Measuring";
            }
            else
            {
                // Loop one final time and place a final dot stamp.
                observer.FinalDotToggle(true);
                observer.MeasureLoopToggle(false);
                tbText.Text = "Stopped Measuring (manual)";
            }
        }
        // Thread to continiously loop scanning and comparing distances.
        private void MeassureLoop()
        {
            while (observer.GetMeasureLoopState())
            {
                // Get the last frames values from the sensor and compare them to the callibration.
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    observer.ComparePixels();
                });
                if (observer.TrajectoryDone)
                {
                    observer.FinalDotToggle(true);
                    observer.ComparePixels();
                    observer.MeasureLoopToggle(false);
                }
            }

            observer.MeasureLoopToggle(false);

            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                tbText.Text = "Stopped Measuring";

                DiscTrajectory tempTrajectory = observer.GetTrajectory();
                DrawTrajectory(tempTrajectory);
            });
            int score = observer.FinalizeCurrentTrajectory();
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
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
                int trajectoriesCount = observer.GetTotalTrajectories();
                lbShownIndex.Content = "Trajectory 0 / " + trajectoriesCount;
                indexSlider.Maximum = trajectoriesCount;
                indexSlider.Value = 0;
            });
        }

        // Clear the current canvas of all the drawn items.
        private void ClearCanvasTrajectory()
        {
            CanvasMap.Children.Clear();
            tbText.Text = "Canvas has been reset.";
            indexSlider.Value = 0;
        }

        // When the slider value has been moved.
        private void indexSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int index = Convert.ToInt32(Math.Round(indexSlider.Value - 1)); // -1 To use the index within the list.
            lbShownIndex.Content = "Trajectory " + (index + 1) + " / " + observer.GetTotalTrajectories(); // Show currently selected trajectory.

            // When meassuring is in process go back to 0;
            if (observer.GetMeasureLoopState())
            {
                indexSlider.Value = 0;
            }
            // Show the selected trajectory.
            else
            {
                CanvasMap.Children.Clear();
                if (index >= 0)
                {
                    DiscTrajectory tempTrajectory = observer.GetTrajectory(index);
                    //Show the hovered index trajectory.
                    DrawTrajectory(tempTrajectory);
                }
            }
        }

        // Draw trajectory in canvas.
        private void DrawTrajectory(DiscTrajectory traject)
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

        // Turn off the sensor on closing the window.
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            observer.StopDepthSensor();
        }
    }
}