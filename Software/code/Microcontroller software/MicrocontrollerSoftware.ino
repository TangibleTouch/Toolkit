#include <bluefruit.h>
#include <Adafruit_LittleFS.h>
#include <InternalFileSystem.h>

#include <Wire.h>
#include "Adafruit_MPR121.h"

#ifndef _BV
#define _BV(bit) (1 << (bit)) 
#endif

// BLE Service
BLEDfu  bledfu;
BLEDis  bledis;
BLEUart bleuart;

Adafruit_MPR121 cap = Adafruit_MPR121();

// Storing 'touched' data
uint16_t currtouched = 0;

// Sensor configuration
const uint8_t SENSORS_AMOUNT = 12;
const uint8_t SOFTRESET_VALUE = 0x63;
const uint8_t FDLF_VALUE = 0xFF;
const uint8_t CONFIG1_VALUE = 0x3F;

const uint8_t THRESHOLD_TOUCH = 6;
const uint8_t THRESHOLD_RELEASE= 6;

bool transmitSensorData = false;

void setup()
{

  // Initialize touch sensing
  if (!cap.begin(0x5A)) {
    Serial.println("MPR121 not found!");
    while (1);
  
    Serial.println("MPR121 found!");
  }
  resetTouch();
  

  // Bluetooth setup
  
  Bluefruit.autoConnLed(true);
  Bluefruit.configPrphBandwidth(BANDWIDTH_MAX);
  
  Bluefruit.begin();
  Bluefruit.setTxPower(4);    // 4 - default
  Bluefruit.setName("Cube");

  Bluefruit.Periph.setConnectCallback(connect_callback);
  Bluefruit.Periph.setDisconnectCallback(disconnect_callback);

  bledfu.begin();

  bledis.setManufacturer("Martynas Dabravalskis");
  bledis.setModel("Interactive cube prototype");
  bledis.begin();

  bleuart.begin();

  startAdv();
  
}
// Start advertising
void startAdv(void)
{
  // Advertising packet
  Bluefruit.Advertising.addFlags(BLE_GAP_ADV_FLAGS_LE_ONLY_GENERAL_DISC_MODE);
  Bluefruit.Advertising.addTxPower();

  // Add bleuart service
  Bluefruit.Advertising.addService(bleuart);
  
  Bluefruit.ScanResponse.addName();

  // Default settings
  Bluefruit.Advertising.restartOnDisconnect(true);
  Bluefruit.Advertising.setInterval(32, 244);
  Bluefruit.Advertising.setFastTimeout(30);
  Bluefruit.Advertising.start(0); 
}

// Reset touch sensors
void resetTouch()
{
    cap.writeRegister(MPR121_SOFTRESET, SOFTRESET_VALUE); // Soft reset
    cap.writeRegister(MPR121_FDLF, FDLF_VALUE); 
    cap.writeRegister(MPR121_CONFIG1, CONFIG1_VALUE);
    cap.setThreshholds(THRESHOLD_TOUCH, THRESHOLD_RELEASE);
}
void loop()
{
  // Receive data
  if ( bleuart.available() )
  {
    uint8_t message = (uint8_t) bleuart.read();
    if(message == 1)
      transmitSensorData = true;
    else if(message == 0)
      transmitSensorData = false;
    else if(message == 3)
        resetTouch();
  }
  // Transmit data
  if(transmitSensorData)
    {
        delay(10);
        currtouched = cap.touched();
        byte data[5*SENSORS_AMOUNT];
        uint16_t baseline;
        uint16_t filtered;
        uint8_t isTouching = 0;
        
        for (uint8_t i=0; i<SENSORS_AMOUNT; i++) {

        if(currtouched & _BV(i))
          isTouching = 1;
          
        baseline = cap.baselineData(i);
        filtered = cap.filteredData(i);

        // Each sensor data is 5 bytes long. 1 byte for isTouching, 4 bytes for baseline and filtered raw data.
        uint8_t dataSensor[5] = {isTouching, ((baseline & 0xFF00) >> 8), (baseline & 0x00FF), ((filtered & 0xFF00) >> 8), (filtered & 0x00FF)};

        // Add single sensor data to the packet
        memcpy(data+sizeof(dataSensor)*i, dataSensor, sizeof(dataSensor));
       
        isTouching = 0;
      }
      // Send the packet
      bleuart.write(data, 5 * SENSORS_AMOUNT); 
    }

    
}

// Connect callback
void connect_callback(uint16_t conn_handle)
{
  // Get the reference to current connection
  BLEConnection* connection = Bluefruit.Connection(conn_handle);

  delay(1000);
   
  transmitSensorData = true;
}

// Disconnect callback
void disconnect_callback(uint16_t conn_handle, uint8_t reason)
{
  (void) conn_handle;
  (void) reason;

  Serial.println();
  Serial.print("Disconnected, reason = 0x"); Serial.println(reason, HEX);
  transmitSensorData = false;
}
