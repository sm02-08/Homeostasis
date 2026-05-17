#include "DHT.h"

// --- DHT11 CONFIGURATION ---
#define DHTPIN 9 
#define DHTTYPE DHT11
DHT dht(DHTPIN, DHTTYPE);

// --- MOTION SENSOR (HC-SR501) CONFIGURATION ---
const int motionPin = 10;

// --- JOYSTICK (KY-023) CONFIGURATION ---
const int pinX = A0;
const int pinY = A1;
const int pinSW = 5;

// --- TIMING VARIABLES ---
float lastFahrenheit = 0.0;
unsigned long lastDHTReadTime = 0;

void setup() {
  Serial.begin(9600);
  dht.begin();
  
  pinMode(motionPin, INPUT);
  pinMode(pinSW, INPUT_PULLUP); // Joystick button click
}

void loop() {
  // 1. Read DHT11 using a non-blocking timer (only updates once every 2 seconds)
  if (millis() - lastDHTReadTime >= 2000) {
    float f = dht.readTemperature(true); // true = Fahrenheit
    if (!isnan(f)) {
      lastFahrenheit = f;
    }
    lastDHTReadTime = millis();
  }

  // 2. Read HC-SR501 Motion Sensor (HIGH means motion, LOW means still)
  int motionState = digitalRead(motionPin);
  String motionStatus = (motionState == HIGH) ? "MOTION_DETECTED" : "STILL";

  // 3. Read Joystick Analog Values (0 to 1023)
  int joyX = analogRead(pinX);
  int joyY = analogRead(pinY);

  // 4. Construct and Stream the Unified Packet over Serial
  Serial.print(motionStatus);   Serial.print(",");
  Serial.print(lastFahrenheit, 1); Serial.print(","); // 1 decimal place rounding
  Serial.print(joyX);           Serial.print(",");
  Serial.println(joyY);         // println adds the newline (\r\n) to end the packet

  // 30ms delay gives roughly 33 clean updates per second in Godot
  delay(30); 
}