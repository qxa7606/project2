using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace project2
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            // ensuring we have all the arguments, creating a tftp instance and running it
            if (args.Length == 3 && (args[0] == "error" || args[0] == "noerror"))
            {
                tftp cli = new tftp(args[0] == "error" ? true : false, args[1], args[2]);
                cli.run();
            }
            else
            {
                Console.WriteLine("Usage: program.exe |error|noerror| tftp-host file");
            }
            
        }
    }
}