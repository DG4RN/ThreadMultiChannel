C# ConsoleApp to receive and transmit data via 4 ports parallel. 
Used to distribute Pure Signal Feedback
to a Red Pitaya SDR including distribution of actual used frequency.

used port:
static SerialPort port1 = new SerialPort("COM8", 9600); // RX PS_Feedback by Thetis
static SerialPort port2 = new SerialPort("COM9", 9600); // CAT Data Thetis
static SerialPort port3 = new SerialPort("COM22", 19200); // Teensy Interface at Red Pitaya
static SerialPort port4 = new SerialPort("COM12", 4800); // LDMOS Interface ICOM Format
