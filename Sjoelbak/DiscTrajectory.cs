using System.Collections.Generic;
using System.Windows.Shapes;

namespace Sjoelbak
{
    internal class DiscTrajectory
    {
        // List of all the different lines the trajectory consists of.
        private List<Line> trajectoryLines = new List<Line>();
        // List of all rectangles that make the final dot where the disc ended up.
        private List<Rectangle> finalDot = new List<Rectangle>();

        public DiscTrajectory() { }

        // Add new line to the trajectory.
        public void AddLine(Line line)
        {
            trajectoryLines.Add(line);
        }

        // Adding new rect to the dot.
        public void AddDot(Rectangle rect) 
        {
            finalDot.Add(rect);
        }

        // Return all the lines of the trajectory.
        public List<Line> GetTrajectoryLines()
        {
            return trajectoryLines;
        }

        // Return all the dots of the final placing of the disc.
        public List<Rectangle> GetFinalDot()
        {
            return finalDot;
        }

        // Clearing the trajectory of the disc.
        public void ClearTrajectory()
        {
            trajectoryLines.Clear();
            finalDot.Clear();
        }
    }
}
