using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace project2
{
    /// <summary>
    /// This class represents a single tftp instance.
    /// </summary>
    class tftp
    {
        static int errors = 0;

        bool errors_enabled = false;
        string server_name = "";
        string filename = "";
        const string oct = "octet";

        byte[] req_msg;
        byte[] data_msg;
        byte[] ack_msg;
        byte[] nack_msg;
        
        Int16 next_block_num = 1;

        bool finished = false;

        int left_over_bits_count = 0;
        BitArray left_over_bits = new BitArray(8);

        int final_bytes_count = 0;
        byte[] final_bytes = new byte[3000];

        int last_port = 7000;

        const string count_1_ = "10101010101010101010101010101010";
        const string count_2_ = "01100110011001100110011001100110";
        const string count_4_ = "00011110000111100001111000011110";
        const string count_8_ = "00000001111111100000000111111110";
        const string count_16_ = "00000000000000011111111111111110";

        /// <summary>
        /// A simple constructor
        /// </summary>
        /// <param name="errors"></param>
        /// <param name="serve"></param>
        /// <param name="filename"></param>
        public tftp(bool errors, string serve, string filename)
        {
            this.errors_enabled = errors;
            serve = serve == "kayrun" ? this.server_name = "kayrun.cs.rit.edu" : serve;
            this.filename = filename;
        }

        /// <summary>
        /// This class is responsible retrieving and saving the file
        /// </summary>
        public void run()
        {
            int rec;
            int tmp;
            byte[] tmp_9 = new byte[2];
            bool right_block_rec = false;

            var client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var server = new IPEndPoint((IPAddress)Dns.GetHostAddresses(server_name).Where(x => x.AddressFamily == AddressFamily.InterNetwork).First(), 7000);
            client.ReceiveTimeout = 5000;

            initializeMsgTypes();

            client.SendTo(req_msg, server);

            var RemoteIpEndpoint = new IPEndPoint(IPAddress.Any, 0);
            var remoteServer = (EndPoint)RemoteIpEndpoint;

            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            do
            {
                try
                {
                    rec = client.ReceiveFrom(data_msg, ref remoteServer);

                    if (data_msg[1] != 3)
                    {
                        Console.WriteLine("The server sent an error message");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }

                    // checking for right block number
                    tmp_9[0] = data_msg[2];
                    tmp_9[1] = data_msg[3];
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(tmp_9);
                    if (next_block_num != BitConverter.ToInt16(tmp_9, 0))
                    {
                        right_block_rec = false;
                    }
                    else
                    {
                        right_block_rec = true;
                    }

                    // checkign for updated port
                    if (int.Parse(remoteServer.ToString().Split(':')[1]) != last_port)
                    {
                        last_port = int.Parse(remoteServer.ToString().Split(':')[1]);
                        RemoteIpEndpoint.Port = last_port;
                        remoteServer = (EndPoint)RemoteIpEndpoint;

                        Console.WriteLine("Port updated to: " + last_port);
                    }

                    tmp = final_bytes_count;
                    left_over_bits_count = 0;
                    if (right_block_rec && processedWithoutErrors(rec))
                    {
                        if (rec < data_msg.Length)
                        {
                            writeToFile();
                            Console.WriteLine("Recieved only " + rec + " bytes. Done.");
                            finished = true; // done
                        }
                        else
                        {
                            if ((final_bytes_count + (rec - 4)) > final_bytes.Length)
                            {
                                writeToFile();
                            }

                            updateACKMsg();
                            client.SendTo(ack_msg, server);
                            next_block_num++;
                        }
                    }
                    else
                    {
                        final_bytes_count = tmp;
                        updateNACKMsg();
                        client.SendTo(nack_msg, server);
                    }
                    
                }
                catch (SocketException s)
                {
                    Console.WriteLine("Exception raised");
                    if (errors++ < 2) // start over
                    {
                        if (File.Exists(filename))
                        {
                            File.Delete(filename);
                        }

                        final_bytes_count = 0;
                        next_block_num = 1;
                        left_over_bits_count = 0;
                        client.SendTo(req_msg, server);
                    }
                    else
                    {
                        Console.WriteLine("There was an error.");
                        break;
                    }
                }
            } while (!finished);
            
            Console.WriteLine("Press any key to exit..");
            Console.ReadKey();
        }

        /// <summary>
        /// This method processes each block
        /// </summary>
        /// <param name="num_recieved"></param>
        /// <returns></returns>
        private bool processedWithoutErrors(int num_recieved)
        {
            byte[] tmp_bytes = new byte[4];
            byte tmp_byte;
            BitArray bits;

            int index = 4;
            while ((num_recieved - index) >= 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    tmp_bytes[i] = data_msg[i + index];
                }

                // swapping bytes
                tmp_byte = tmp_bytes[0];
                tmp_bytes[0] = tmp_bytes[3];
                tmp_bytes[3] = tmp_byte;
                tmp_byte = tmp_bytes[1];
                tmp_bytes[1] = tmp_bytes[2];
                tmp_bytes[2] = tmp_byte;

                bits = new BitArray(tmp_bytes);

                bits = reverseBitsOfBytes(bits);

                // extract hamming bits
                BitArray bits_1 = new BitArray(26);

                bits = errorsCorrected(bits);

                if (bits == null)
                {
                    return false;
                }

                int idx = 0;
                for (int i = 0; i < 32; i++)
                {
                    if (i != (31) && i != (30) && i != (28) && i != (24) && i != (16) && i != (0))
                    {
                        bits_1[idx++] = bits[i];
                    }
                }

                // append lefovers
                idx = 0;
                BitArray bits_2 = new BitArray(26 + left_over_bits_count);
                for (int i = 0; i < left_over_bits_count; i++)
                {
                    bits_2[idx++] = left_over_bits[i];
                }

                left_over_bits_count = 0;

                for (int i = 0; i < bits_1.Count; i++)
                {
                    bits_2[idx++] = bits_1[i];
                }

                bits_2 = reverseBitsOfBytes(bits_2);

                for (int i = 0; i < bits_2.Count/8; i++)
                {
                    tmp_byte = 0;
                    for (int j = 0; j < 8; j++)
                    {
                        if (bits_2[(i*8) + j])
                            tmp_byte += (byte)Math.Pow(2, 7-j);
                    }
                    final_bytes[final_bytes_count++] = tmp_byte;
                }

                idx = (bits_2.Count / 8) * 8;

                for (int i = 0; i < bits_2.Count % 8; i++)
                {
                    left_over_bits[left_over_bits_count++] = bits_2[idx++];
                }

                index += 4;
                reverseLeftovers();
            }

            return true;
        }

        /// <summary>
        /// This checks/corrects each packet
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private BitArray errorsCorrected(BitArray input)
        {
            bool[] output = new bool[input.Count];

            int count_1 = 0;
            int count_2 = 0;
            int count_4 = 0;
            int count_8 = 0;
            int count_16 = 0;
            int count_tot = 0;

            bool corrected = false;

            bool tmp_bit;

            int tmp_num = 0;

            // reversing everythign
            for (int i = 31; i >= 0; i--)
            {
                output[tmp_num++] = input[i];
            }

            for (int i = 1; i < 33; i++)
            {
                if (output[i - 1])
                {
                    if (count_1_[i - 1] == '1')
                    {
                        count_1++;
                    }

                    if (count_2_[i - 1] == '1')
                    {
                        count_2++;
                    }

                    if (count_4_[i - 1] == '1')
                    {
                        count_4++;
                    }

                    if (count_8_[i - 1] == '1')
                    {
                        count_8++;
                    }

                    if (count_16_[i - 1] == '1')
                    {
                        count_16++;
                    }

                    count_tot++;
                }
            }

            if (((count_1 % 2) != 0) ||
                ((count_2 % 2) != 0) ||
                ((count_4 % 2) != 0) ||
                ((count_8 % 2) != 0) ||
                ((count_16 % 2) != 0))
            {

                tmp_num = 0;
                tmp_num += (count_1 % 2) == 0 ? 0 : 1;
                tmp_num += (count_2 % 2) == 0 ? 0 : 2;
                tmp_num += (count_4 % 2) == 0 ? 0 : 4;
                tmp_num += (count_8 % 2) == 0 ? 0 : 8;
                tmp_num += (count_16 % 2) == 0 ? 0 : 16;
                
                output[tmp_num - 1] = output[tmp_num - 1] ? false : true;

                corrected = true;
            }

            if ((corrected && (count_tot % 2) == 0))
            {
                return null;
            }
            
            // reversing everythign
            for (int i = 0; i < 32/2; i++)
            {
                tmp_bit = output[i];
                output[i] = output[32 - i - 1];
                output[32 - i - 1] = tmp_bit;
            }

            BitArray ret = new BitArray(output.Length);
            for (int i = 0; i < output.Length; i++)
            {
                ret[i] = output[i];
            }

            return ret;
        }

        /// <summary>
        /// Simple method to incrementally write to the file
        /// </summary>
        /// <returns></returns>
        public bool writeToFile()
        {
            try
            {
                using (var fs = new FileStream(filename, FileMode.Append))
                {
                    fs.Write(final_bytes, 0, final_bytes_count);
                    final_bytes_count = 0;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught in process: {0}", ex);
                return false;
            }
        }

        /// <summary>
        /// Method to reverse leftover bytes
        /// </summary>
        private void reverseLeftovers()
        {
            bool tmp_bit;
            for (int i = 0; i < left_over_bits_count/2; i++)
            {
                tmp_bit = left_over_bits[i];
                left_over_bits[i] = left_over_bits[left_over_bits_count - i - 1];
                left_over_bits[left_over_bits_count - i - 1] = tmp_bit;
            }
        }

        /// <summary>
        /// A method to reverse the bits of each byte
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private BitArray reverseBitsOfBytes(BitArray input)
        {
            bool tmp_bit;
            for (int i = 0; i < input.Length/8; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    tmp_bit = input[(i * 8) + j];
                    input[(i * 8) + j] = input[(i * 8) + (8 - j - 1)];
                    input[(i * 8) + (8 - j - 1)] = tmp_bit;
                }
            }

            BitArray tmp = new BitArray(input.Length % 8);
            if (tmp.Length > 0)
            {
                for (int i = input.Length - (input.Length % 8); i < input.Length; i++)
                {
                    tmp[i - (input.Length - (input.Length % 8))] = input[i];
                }

                for (int i = 0; i < tmp.Length / 2; i++)
                {
                    tmp_bit = tmp[i];
                    tmp[i] = tmp[tmp.Length - i - 1];
                    tmp[tmp.Length - i - 1] = tmp_bit;
                }

                for (int i = 0; i < tmp.Length; i++)
                {
                    input[(input.Length / 8) * 8 + i] = tmp[i];
                }
            }

            return input;
        }

        /// <summary>
        /// Initializes all the messages that could be sent to the server
        /// </summary>
        private void initializeMsgTypes()
        {
            // req msg
            req_msg = new byte[filename.Length + oct.Length + 4];
            req_msg[0] = 0; req_msg[1] = errors_enabled ? (byte)2 : (byte)1;
            int idx = 2; 
            foreach (char c in filename)
            {
                req_msg[idx++] = Convert.ToByte(c);
            }
            req_msg[idx++] = 0;
            foreach (char c in oct)
            {
                req_msg[idx++] = Convert.ToByte(c);
            }
            req_msg[idx] = 0;

            // data msg
            data_msg = new byte[516];

            // ack msg
            ack_msg = new byte[4] { 0x00, 0x04, 0x00, 0x00 };

            // nack msg
            nack_msg = new byte[4] { 0x00, 0x06, 0x00, 0x00 };

        }

        /// <summary>
        /// Updates the block number in the ack message
        /// </summary>
        private void updateACKMsg()
        {
            byte[] ret = new byte[2];
            next_block_num %= short.MaxValue;

            ret = BitConverter.GetBytes(next_block_num);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(ret);
            ack_msg[2] = ret[0];
            ack_msg[3] = ret[1];
        }

        /// <summary>
        /// Updates the block number in the nack message
        /// </summary>
        private void updateNACKMsg()
        {
            byte[] ret = new byte[2];
            next_block_num %= short.MaxValue;

            ret = BitConverter.GetBytes(next_block_num);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(ret);
            nack_msg[2] = ret[0];
            nack_msg[3] = ret[1];
        }
    }
}
