using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;

namespace Tracert
{
    internal class Program
    {
        private static Socket icmp = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);

        private static bool printHostName;
        private static IPEndPoint rmEP;
        private static IPEndPoint myEP;
        private static short numOfHops;
        private static short numOfPacks;

        static void Main(string[] args)
        {
            TryConfigure(args);

            EndPoint rpEP = rmEP;

            for (short i = 1; i < numOfHops; i++)
            {
                Console.Write(" {0}\t", icmp.Ttl = i);

                int timeOut = 0;

                for (int j = 0; j < numOfPacks; j++)
                {
                    byte[] msg = BuildEchoMsg(0, numOfPacks * i + j, "Hello, world!");
                    byte[] reply = new byte[256];

                    Stopwatch sw = Stopwatch.StartNew();

                    icmp.SendTo(msg, 0, msg.Length, SocketFlags.None, rmEP);
                    try
                    {
                        EndPoint ep = rmEP;
                        icmp.ReceiveFrom(reply, ref ep);
                        sw.Stop();
                        rpEP = ep;
                        Console.Write(" {0} ms\t", sw.ElapsedMilliseconds);
                    }
                    catch
                    {
                        timeOut++;
                        Console.Write(" *\t");
                    }
                }

                if (timeOut == numOfPacks)
                {
                    Console.WriteLine("Request timed out");
                    continue;
                }

                Console.WriteLine(GetEPText(rpEP, printHostName));

                if (rmEP.Equals(rpEP))
                {
                    break;
                }
            }

            icmp.Close();
        }

        static byte[] BuildEchoMsg(int id, int sn, string data)
        {
            byte[] icmpEchoMsg = new byte[8 + data.Length + (data.Length & 1)];

            icmpEchoMsg[0] = 0x08;
            icmpEchoMsg[1] = 0x00;

            Encoding.UTF8.GetBytes(data, 0, data.Length, icmpEchoMsg, 8);

            icmpEchoMsg[4] = (byte)(id >> 8);
            icmpEchoMsg[5] = (byte)(id);

            icmpEchoMsg[6] = (byte)(sn >> 8);
            icmpEchoMsg[7] = (byte)(sn);

            int checksum = Checksum(icmpEchoMsg);
            icmpEchoMsg[2] = (byte)(checksum >> 8);
            icmpEchoMsg[3] = (byte)(checksum);

            return icmpEchoMsg;
        }

        static int Checksum(byte[] icmp)
        {
            int checksum = 0;
            for (int i = 0; i < icmp.Length; i += 2)
            {
                checksum += (icmp[i] << 8) | (icmp[i + 1]);
                checksum = (checksum & 0xffff) + (checksum >> 16);
            }
            checksum = ~checksum & 0xffff;
            return checksum;
        }

        static string GetEPText(EndPoint ep, bool addName)
        {
            IPEndPoint ipEP = (IPEndPoint)ep;
            if (addName)
            {
                try
                {
                    return String.Format("{0} [{1}]", Dns.GetHostEntry(ipEP.Address).HostName, ipEP.Address);
                }
                catch { }
            }
            return ipEP.Address.ToString();
        }

        static void TryConfigure(string[] args)
        {
            try
            {
                Configure(args);
            }
            catch { }

            if (rmEP == null)
            {
                Console.WriteLine("Illegal destination");
                Environment.Exit(0);
            }

            if (myEP == null || !myEP.Equals(icmp.LocalEndPoint))
            {
                Console.WriteLine("Illegal source ip");
                Environment.Exit(0);
            }
        }

        static void Configure(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Use: \n\t tracer destination -s num.num.num.num -p");
                Console.WriteLine("Example: \n\t tracer 8.8.8.8 -s 192.168.1.5 -p");
                Console.WriteLine();
                Console.WriteLine("-s num.num.num.num\t specify source ip");
                Console.WriteLine("-p                \t print host names");
                Console.WriteLine();
                Environment.Exit(0);
            }

            foreach (IPAddress rmIP in Dns.GetHostAddresses(args[0]))
            {
                if (rmIP.AddressFamily == AddressFamily.InterNetwork)
                {
                    rmEP = new IPEndPoint(rmIP, 0);
                    break;
                }
            }

            Console.WriteLine("Trace route to {0} [{1}]", args[0], rmEP.Address);

            for (int i = 1; i < args.Length; i++)
            {
                if ("-s".Equals(args[i]))
                {
                    myEP = new IPEndPoint(IPAddress.Parse(args[++i]), 0);
                    icmp.Bind(myEP);
                    continue;
                }
                if ("-p".Equals(args[i]))
                {
                    printHostName = true;
                    continue;
                }
            }

            if (myEP == null)
            {
                myEP = new IPEndPoint(IPAddress.Any, 0);
                icmp.Bind(myEP);
            }

            icmp.ReceiveTimeout = 500;
            numOfHops = 20;
            numOfPacks = 3;
        }

    }
}