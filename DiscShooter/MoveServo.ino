// angles to the scoring areas are predetermined, these can be found in the documents
// Scoring works as follows (left to right) 2, 3, 4, 1.
const int onePointAngle = 85;
const int threePointsAngle = 88;
const int fourPointsAngle = 92;
const int twoPointsAngle = 95;

// Idle will be used when the application starts, this shoots the disc straight down the center.
const int idleAngle = 90;

// For Loading and Launching a disc:
// A rubber band will be hanged behind a servo in a loading state, when wanting to fire the servo will let go of the band.
const int firingDiscAngle = 180;
const int loadingDiscAngle = 90;

// Move trigger to the loading position.
void servoLoadDisc() {
  launchTrigger.write(loadingDiscAngle);
}

// Move trigger to the firing position.
void servoFireDisc() {
  launchTrigger.write(firingDiscAngle);
}

// Angle servo to the right scoring area.
void changeFireAngle(int goal) {
  switch (goal) {
    case 1:
      launchAngler.write(onePointAngle);
      break;
    case 2:
      launchAngler.write(twoPointsAngle);
      break;
    case 3:
      launchAngler.write(threePointsAngle);
      break;
    case 4:
      launchAngler.write(fourPointsAngle);
      break;
    default:
    launchAngler.write(idleAngle);
      break;
  }
}