#include <SoftwareSerial.h> // Required for SoftwareSerial on Uno/Nano

// Define pins for SoftwareSerial communication with Reyax module
// Connect Arduino Pin 10 to Reyax TXD
// Connect Arduino Pin 11 to Reyax RXD
// YOU MUST CONNECT THESE PINS AS SPECIFIED OR CHANGE THEM HERE!
const int REYAX_RX_PIN = 10; // Arduino RX pin for Reyax (Reyax TX to Arduino RX)
const int REYAX_TX_PIN = 11; // Arduino TX pin for Reyax (Reyax RX to Arduino TX)

SoftwareSerial ReyaxSerial(REYAX_RX_PIN, REYAX_TX_PIN); // RX, TX

// Pump Relay Pin Definition for Arduino
const int PUMP_RELAY_PIN = 7; // Pump relay control pin connected to Arduino Digital Pin 7

void setup() {
  // Initialize Hardware Serial (USB) for communication with VB.NET
  // Baud rate MUST match your VB.NET application's configuration
  Serial.begin(115200);

  // Initialize SoftwareSerial for communication with the Reyax module
  // Baud rate MUST match your Reyax module's configuration
  ReyaxSerial.begin(115200);

  // Set the pump relay pin as an OUTPUT
  pinMode(PUMP_RELAY_PIN, OUTPUT);
  // Ensure pump starts OFF
  digitalWrite(PUMP_RELAY_PIN, LOW); // Assuming LOW keeps the pump OFF

  delay(100);
  Serial.println("Arduino Gateway Initialized.");
}

void loop() {
  String receivedSensorData = "";

  // Check if data is available from the Reyax module
  while (ReyaxSerial.available()) {
    receivedSensorData = ReyaxSerial.readStringUntil('\n'); // Read until newline
    receivedSensorData.trim(); // Remove any whitespace
    Serial.print("Received from Reyax: "); // Debugging to USB Serial
    Serial.println(receivedSensorData);    // Debugging to USB Serial
  }

  // Get current pump status (from Arduino's own relay pin)
  int currentPumpStatus = digitalRead(PUMP_RELAY_PIN);

  // If we received sensor data from Reyax, combine it with pump status and send to VB.NET
  if (receivedSensorData.length() > 0) {
    // Expected format from ESP32: "T:XX.X,H:YYY"
    // We add pump status: "T:XX.X,H:YYY,P:Z"
    String dataToSendToVB = receivedSensorData + ",P:" + String(currentPumpStatus);
    Serial.println(dataToSendToVB); // Send to VB.NET via USB Serial
  }

  // --- Handle incoming commands from VB.NET ---
  // This part remains the same as before, handling "ON", "OFF", "AUTO" commands.
  // It's still called from loop, or could be in serialEvent if preferred.
  if (Serial.available()) {
    String command = Serial.readStringUntil('\n');
    command.trim();
    command.toUpperCase();

    if (command == "ON") {
      digitalWrite(PUMP_RELAY_PIN, HIGH); // Turn pump ON
      Serial.println("Pump turned ON by user.");
    } else if (command == "OFF") {
      digitalWrite(PUMP_RELAY_PIN, LOW);  // Turn pump OFF
      Serial.println("Pump turned OFF by user.");
    } else if (command == "AUTO") {
      Serial.println("System switched to AUTO mode.");
    }
  }

  delay(100); // Small delay to prevent constantly busy-looping, adjust as needed.
              // The main sensor data sending is still controlled by the ESP32's 2-second delay.
}