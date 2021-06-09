using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
//http://live.legendy.by:8000/legendyfm
namespace Proxy
{
    class Proxy
    {
        public const int backlog = 20;
        public const int bufferSize = 1024 * 20;

        public string[] blackList;        
        private IPAddress address;
        private int port; 
        private Socket socket;

        public Proxy(IPAddress ip, int port, string listName)
        {
            address = ip;
            this.port = port;
            blackList = GetBlackList(listName);            
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); 
            socket.Bind(new IPEndPoint(address, port)); 
        }

        private static string[] GetBlackList(string fileName)
        {
            string list = "";
            using (StreamReader reader = new StreamReader(fileName))
            {
                list = reader.ReadToEnd();
            }

            string[] blackList = list.Trim().Split(new char[] { '\n' });
            return blackList;
        }

        public void Start()
        {
            socket.Listen(backlog); 
            while (true)
            {
                Socket newSocket = socket.Accept(); 
                Thread thread = new Thread(() => StartProxy(newSocket));
                thread.Start();
            }
        }

        public void StartProxy(Socket newSocket) 
        {
            NetworkStream networkStream = new NetworkStream(newSocket); 
            string message = Encoding.UTF8.GetString(Receive(networkStream));                                                                            
            ProxyAnswer(networkStream, message);
            newSocket.Dispose();
        }

        public byte[] Receive(NetworkStream networkStream)
        {
            byte[] buffer = new byte[bufferSize];
            byte[] allData = new byte[bufferSize];
            int reciveBytes = 0;
            int size;

            do
            {
                size = networkStream.Read(buffer, 0, buffer.Length); 
                Array.Copy(buffer, 0, allData, reciveBytes, size); 
                reciveBytes += size;
            } while (networkStream.DataAvailable && reciveBytes < bufferSize);

            return allData;
        }

        public void ProxyAnswer(NetworkStream networkStream, string message)
        {
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                message = GetSitePath(message); 
                string[] splitMessage = message.Split('\r', '\n');  
                string host = splitMessage.FirstOrDefault((str) => str.Contains("Host: ")); 
                host = host.Remove(host.IndexOf("Host: "), ("Host: ").Length); 

                if (blackList != null && Array.IndexOf(blackList, host.ToLower()) != -1)
                {
                    string temp = "This syte is in blacklist";
                    string error = $"HTTP/1.1 403 Forbidden\nContent-Type: text/html\r\nContent-Length: {temp.Length}\n\n{temp}";
                    byte[] errorpage = Encoding.UTF8.GetBytes(error);
                    networkStream.Write(errorpage, 0, errorpage.Length); 
                    Console.WriteLine(DateTime.Now + ": " + host + "(blocked)");
                    return;
                }

                string[] hostNameOrAddress = host.Split(':');
                IPAddress hostIP = Dns.GetHostEntry(hostNameOrAddress[0]).AddressList[0];
                IPEndPoint serverEP;
                if (hostNameOrAddress.Length == 1)
                { 
                    serverEP = new IPEndPoint(hostIP, 80);
                }
                else
                {
                    serverEP = new IPEndPoint(hostIP, int.Parse(hostNameOrAddress[1])); 
                }

                server.Connect(serverEP); 
                NetworkStream serverStream = new NetworkStream(server);            
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                serverStream.Write(messageBytes, 0, messageBytes.Length);
                byte[] receiveData = Receive(serverStream);
                networkStream.Write(receiveData, 0, receiveData.Length);

                string code = GetAnswerCode(Encoding.UTF8.GetString(receiveData));
                Console.WriteLine(DateTime.Now.ToString() + " Host: {0} code: {1}", hostNameOrAddress[0], code);
                serverStream.CopyTo(networkStream);
            }
            catch (Exception e)
            {
            }
            finally
            {
                server.Dispose();
            }
            

        }

        public string GetSitePath(string message) 
        {
            MatchCollection matchCollection = (new Regex(@"http:\/\/[a-z0-9а-я\.\:]*")).Matches(message);
            string host = matchCollection[0].Value;
            message = message.Replace(host, "");
            return message;
        }

        public string GetAnswerCode(string serverResponse) 
        {
            string[] response = serverResponse.Split('\n');
            string code = response[0].Substring(response[0].IndexOf(" ") + 1);

            return code;
        }
    }
}
