using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace Sjoelbak
{
    internal class DiscTrajectory
    {
        // List of all the different lines the trajectory consists of.
        List<Line> trajectLines = new List<Line>();
        // List of all rectangles that make the final dot where the disc ended up.
        List<Rectangle> finalDot = new List<Rectangle>();

        DiscTrajectory() { }

        // Add new line to the trajectory.
        public void AddLine(Line line)
        {
            trajectLines.Add(line);
        }

        // Adding new rect to the dot.
        public void AddDot(Rectangle rect) 
        {
            finalDot.Add(rect);
        }
    }
}
