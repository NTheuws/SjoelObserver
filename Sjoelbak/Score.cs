using System;

namespace Sjoelbak
{
    internal class Score
    {
        private int OnePoint;
        private int TwoPoint; 
        private int ThreePoint;
        private int FourPoint;

        public Score()
        {
            // Always start with 0 points;
            OnePoint = 0;
            TwoPoint = 0;   
            ThreePoint = 0;
            FourPoint = 0;
        }

        // Scored a disc in the 1 point area.
        public void ScoredOne()
        {
            OnePoint++;
        }
        // Scored a disc in the 2 point area.
        public void ScoredTwo()
        {
            TwoPoint++;
        }
        // Scored a disc in the 3 point area.
        public void ScoredThree()
        {
            ThreePoint++;
        }
        // Scored a disc in the 4 point area.
        public void ScoredFour()
        {
            FourPoint++;
        }

        // Reset the score of the player.
        public void ResetScore()
        {
            OnePoint = 0;
            TwoPoint = 0;
            ThreePoint = 0;
            FourPoint = 0;
        }

        // Calculate and return the current score.
        public int GetScore()
        {
            int points = 0;
            int lowestVal = GetLowestValue(OnePoint, TwoPoint, ThreePoint, FourPoint);

            // When 1 disc is in each area you get 20points instead of 10.
            points += lowestVal * 20;
            // Afterwards count the scoring directly to the total, after substracting the amount of disc each area has.
            points += (OnePoint - lowestVal) + (TwoPoint - lowestVal) * 2 + (ThreePoint - lowestVal) * 3 + (FourPoint - lowestVal) * 4;

            return points;
        }

        // Returns the lowest value among the 4.
        private int GetLowestValue(int val1, int val2, int val3, int val4)
        {
            return Math.Min(Math.Min(Math.Min(val1, val2), val3), val4);
        }
    }
}
