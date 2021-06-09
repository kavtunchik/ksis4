﻿using System;
using System.Net;

namespace Proxy
{
    class Program
    {
        private const string localHost = "127.0.0.1";
        private const int port = 5050;
        private const string blackListName = "black_list.txt";

        static void Main(string[] args)
        {
            Proxy proxy = new Proxy(IPAddress.Parse(localHost), port, blackListName);
            Console.WriteLine("Started");
            proxy.Start();
        }
    }
}
