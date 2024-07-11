using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Policy;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;



namespace ConsoleApp_PS
{
    internal class Program
    {
        static SerialPort port1 = new SerialPort("COM8", 9600); // RX PS_Feedback by Thetis
        static SerialPort port2 = new SerialPort("COM9", 9600); // CAT Data Thetis
        static SerialPort port3 = new SerialPort("COM22", 19200); // Teensy Interface at Red Pitaya
        static SerialPort port4 = new SerialPort("COM12", 4800); // LDMOS Interface ICOM Format

        static BlockingCollection<string> data_tx = new BlockingCollection<string>();
        static BlockingCollection<string> frequenz_pa = new BlockingCollection<string>();

        static void Main(string[] args)
        {
            char[] readbuffer_aduino = new char[12];
            
            char[] frequenz_als_char = new char[12];
            char[] wechsel_format = new char[16];


            

            

           

            // Open the serial ports
            port1.Open();
            port2.Open();
            port3.Open();
            port4.Open();

            // Create two threads to handle data reception
            Thread thread1 = new Thread(ReceiveData1);
            Thread receivethread2 = new Thread(ReceiveData2);
            Thread transmitthread2 = new Thread(TransmitData2);
            
            Thread thread3 = new Thread(() => TransmitData3(port3));
            Thread receivethread4 = new Thread(ReceiveData4);
            Thread transmitthread4 = new Thread(() => TransmitData4(port4));


            thread1.Start();
            receivethread2.Start();
            transmitthread2.Start();
            thread3.Start();
            receivethread4.Start();
            transmitthread4.Start();


            

        }    //*************** Ende Main *********************

        static void ReceiveData1()     // Thetis Pure Signal Feedback
        {
            while (true)
            {
                if (port1.BytesToRead > 0)
                {
                    string data = port1.ReadLine();
                    data = "PS" + data + "E";
                    data_tx.Add(data);
                    Console.WriteLine("Data from Port 1: " + data);
                }
            }
        }

        static void TransmitData2()              // Aktuelle Frequenz von Thetis über CAT anfordern
        {

            char[] sendbuffer_sdr = new char[15];

            while (true)
            {
                int i = 0;
                sendbuffer_sdr[0] = 'F';  // = F  0x46
                sendbuffer_sdr[1] = 'A';  // = A  0x41
                sendbuffer_sdr[2] = ';';  // =;   0x3b


                

                //printf("?? %s %d \n", &sendbuffer_sdr[i],i);
                //char c_2_sdr = sendbuffer_sdr[i]; 
                port2.Write(sendbuffer_sdr, 0, 3);
                // printf("%u %u Bytes were written\r",bytesWritten, strlen(sendbuffer_sdr));

                Thread.Sleep(200);        // Pause 200 msec
            }
        }

            static void ReceiveData2()  // Aktuelle Frequenz von Thetis über CAT empfangen
        {
           

            char[] readbuffer_sdr = new char[255];
            char[] frequenz_extract = new char[11];
            char[] sendbuffer_aduino = new char[12];

            Int64 frequenz_rc;
            bool frequenz_received = false;

            while (true)
            {
                int i = 0;
                
                if (port2.BytesToRead > 0)
                {
                    int n = port2.ReadByte();
                    //Console.WriteLine("                  Data rx:  " + n);
                    i = 0;
                    while (28 > i)
                    {
                        readbuffer_sdr[i] = Convert.ToChar(port2.ReadByte());
                        i++;
                    }
                    

                    i = 0;
                    while (28 > i)
                    {

                        if ((readbuffer_sdr[i] == 0x46) && (readbuffer_sdr[i + 13] == 0x3b))  // ; =Ende Frequenzrückmelduung = ;
                        {
                            int o = i + 2;
                            Array.Copy(readbuffer_sdr, o, frequenz_extract, 0, 11);
                            string frequenz_rcd = new string(frequenz_extract);
                            frequenz_pa.Add(frequenz_rcd);

                            frequenz_rc = long.Parse(frequenz_rcd);

                            if ((frequenz_rc > 0) && (frequenz_rc < 200000000))
                            {
                                frequenz_received = true;
                            }
                            else
                            {
                                frequenz_received = false;
                            }
                            Console.WriteLine("Frequenz:  " + frequenz_rc);
                            o = 0;
                            i = 0;
                            if (frequenz_received)
                            {
                                if (frequenz_rc < 10000000)
                                {
                                    data_tx.Add("0000" + frequenz_rc);
                                }
                                else
                                {
                                    data_tx.Add("00" + frequenz_rc);
                                }
                             }
                            break;                        
                        }
                        i++;

                        


                    }

                }
            }
        }

           static void TransmitData3(SerialPort port)   // Tx Frequenz and PS feedback to com 22 to the Teensy Board 
           {
            while (true)
            {

                if (data_tx.TryTake(out string dataToSend))
                {
                    port.WriteLine(dataToSend);
                    Console.WriteLine("                              Data sent: Port 3 " + dataToSend);
                }
                
            }                
           }

           static void ReceiveData4()
            {
            char[] controlbuffer = new char[14];
            while (true)
            {
                if (port4.BytesToRead > 0)
                {
                    int i = 0;
                    while (13 > i)
                    {
                        controlbuffer[i] = Convert.ToChar(port4.ReadByte());
                        i++;
                    }
                    i = 0;
                    if (0xfe == controlbuffer[i] && 0xfe == controlbuffer[i + 1] && 0xfd == controlbuffer[i + 5])
                    {
                        Console.WriteLine("                                      Data from Port 4: rx" );
                        port4.DiscardInBuffer();
                    }
                }
            }

        }

           static void TransmitData4(SerialPort port)  // to com 12 in ICOM Format  for LDMOS PA (alle 700msec)
            {
                        
             while (true)
              {
                 if (frequenz_pa.TryTake(out string frequenzToSend))
                 {
                    byte[] bytes = PaFrequenz(frequenzToSend);

                    port4.Write(bytes, 0, 10);
                    Console.WriteLine("                                                              Port4     Data: " + frequenzToSend);
                    Thread.Sleep(400);
                }
                 
              }

             byte[] ConvertToBCD(int value)
             {
                string decimalString = value.ToString();
                if (decimalString.Length == 7)
                {
                    decimalString = "0" + decimalString;
                }

                int numDigits = decimalString.Length;

                // Each digit is represented by 4 bits, so we need half the number of digits for bytes
                int numBytes = (int)Math.Ceiling(numDigits / 2.0);

                byte[] bcdBytes = new byte[numBytes];

                for (int i = 0; i < numDigits; i++)
                {
                    int digit = decimalString[i] - '0';

                    // If the current position is even, the digit occupies the lower 4 bits
                    if (i % 2 == 0)
                    {
                        bcdBytes[i / 2] |= (byte)(digit & 0x0F);
                    }
                    // If the current position is odd, the digit occupies the upper 4 bits
                    else
                    {
                        bcdBytes[i / 2] |= (byte)((digit & 0x0F) << 4);
                    }
                }
                return bcdBytes;
             }

             byte SwapNibbles(byte value)
             {
                // Mask and shift to extract high and low nibbles
                byte highNibble = (byte)((value & 0xF0) >> 4);
                byte lowNibble = (byte)(value & 0x0F);

                // Swap and combine nibbles
                return (byte)((lowNibble << 4) | highNibble);
             }


             byte[] PaFrequenz(string frequence) //Converting frequence String (TS2000 frormat to Icom Format for ldmos PA)
             {
                int frequency = Convert.ToInt32(frequence);

                byte[] bcdBytes = ConvertToBCD(frequency);

                var SendBuffer = new byte[10];  //Icom format to return actual frequence


                SendBuffer[0] = 254;
                SendBuffer[1] = 254;
                SendBuffer[2] = 224;
                SendBuffer[3] = 00;
                SendBuffer[4] = 03;
                SendBuffer[8] = SwapNibbles(bcdBytes[0]);
                SendBuffer[7] = SwapNibbles(bcdBytes[1]);
                SendBuffer[6] = SwapNibbles(bcdBytes[2]);
                SendBuffer[5] = SwapNibbles(bcdBytes[3]);
                SendBuffer[9] = 253;

                return SendBuffer;
             }



        }

       


       

          


    }
        }
    

