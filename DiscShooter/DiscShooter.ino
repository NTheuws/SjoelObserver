#include <Servo.h>

#define LAUNCHTRIGGERPIN 9
#define LAUNCHANGLERPIN 10

Servo launchTrigger;  // Servo that launches the disc.
Servo launchAngler;   // Servo that determines the angle on which the disc will be fired.

const int idle = 0;

// These will be used to determine the action that has to take place.
const int goal1 = 1;
const int goal2 = 2;
const int goal3 = 3;
const int goal4 = 4;
const int loadDisc = 5;
const int fireDisc = 6;


void setup() {
  launchTrigger.attach(LAUNCHTRIGGERPIN);
  launchAngler.attach(LAUNCHANGLERPIN);

  // Position both servos in an idle position.
  servoLoadDisc();
  changeFireAngle(idle);

  Serial.begin(9600);
}

void loop() {
  String receivedMessage = readSerial();
  switch (receivedMessage.toInt()) {
    case goal1:
      changeFireAngle(goal1);
      break;
    case goal2:
      changeFireAngle(goal2);
      break;
    case goal3:
      changeFireAngle(goal3);
      break;
    case goal4:
      changeFireAngle(goal4);
      break;
    case loadDisc:
      servoLoadDisc();
      break;
    case fireDisc:
      servoFireDisc();
      break;
  }
}