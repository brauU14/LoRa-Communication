# main.py for ESP32 with Thonny

from machine import UART, Pin, ADC
import time

# --- Pin Definitions ---
TEMP_PIN = 34
HUMIDITY_PIN = 35
RELAY_PIN = 23

# --- Sensor Thresholds ---
TEMP_THRESHOLD = 30.0  # Activate pump above this temperature (in Celsius)
HUMIDITY_THRESHOLD = 40  # Activate pump below this humidity percentage

# --- UART for RYLR998 ---
# Using UART 2 on ESP32
# RX: GPIO 16, TX: GPIO 17
uart = UART(2, 115200)

# --- Pin Initializations ---
relay = Pin(RELAY_PIN, Pin.OUT)
temp_adc = ADC(Pin(TEMP_PIN))
temp_adc.atten(ADC.ATTN_11DB)  # Set attenuation for 0-3.6V range
humidity_adc = ADC(Pin(HUMIDITY_PIN))
humidity_adc.atten(ADC.ATTN_11DB) # Set attenuation for 0-3.6V range

def read_temperature():
    """Reads the LM35 sensor and returns the temperature in Celsius."""
    adc_value = temp_adc.read()
    voltage = adc_value * (3.3 / 4095)
    temperature = voltage * 100
    return temperature

def read_humidity():
    """Reads the FC-28 sensor and returns the humidity percentage."""
    adc_value = humidity_adc.read()
    # The output of the FC-28 is inverted, lower value means higher moisture
    # We will map the raw ADC values to a 0-100% scale
    # These values might need calibration based on your soil
    min_moisture = 0      # ADC value for very dry soil
    max_moisture = 4095   # ADC value for very wet soil
    humidity_percentage = 100 - ((adc_value - min_moisture) / (max_moisture - min_moisture)) * 100
    return max(0, min(100, humidity_percentage)) # Clamp between 0 and 100

def send_data(temp, hum, pump_status):
    """Sends data to the RYLR998 module."""
    data_string = "T:{:.1f},H:{:.1f},P:{}".format(temp, hum, pump_status)
    uart.write("AT+SEND=0,{},{}\r\n".format(len(data_string), data_string))
    print("Sent: " + data_string)
    time.sleep(1) # Wait for the module to process

# --- Main Loop ---
while True:
    try:
        temperature = read_temperature()
        humidity = read_humidity()
        pump_on = False

        if temperature > TEMP_THRESHOLD:
            relay.on()  # Turn pump ON
            pump_on = True
            print("Temperature high! Pump ON.")
        elif humidity < HUMIDITY_THRESHOLD:
            relay.on() # Turn pump ON
            pump_on = True
            print("Humidity low! Pump ON.")
        else:
            relay.off() # Turn pump OFF
            pump_on = False
            print("Conditions normal. Pump OFF.")

        pump_status_str = "ON" if pump_on else "OFF"
        
        # Send data every 5 seconds
        send_data(temperature, humidity, pump_status_str)

    except Exception as e:
        print("An error occurred: ", e)
        relay.off() # Ensure pump is off in case of error

    time.sleep(5)