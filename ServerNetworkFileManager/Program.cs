using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace ServerNetworkFileManager
{
    class NetworkReciever
    {
        //********************************
        // Прием сетевых файлов
        //********************************

        public StreamWriter sw;
        public NetworkReciever()
        {
            //sw = new StreamWriter("/ais/logs/logReciever.txt");
            sw = new StreamWriter("/logReciever.txt");
        }

        private Stream strLocal = null;
        private NetworkStream strRemote = null;
        private TcpListener tlsServer = null;
        private String port = "3333",
                         serverIP = "172.16.16.8";
                     //  serverIP = "172.16.1.24";
        private int countOfClient = 100;
        private bool isError = false;
        //private string pathToParDir = "/ais/data/";
        private string pathToParDir = "/";

        public void SetServerIP(String ip)
        {
            serverIP = ip;
        }

        public void SetPort(String port)
        {
            this.port = port;
        }

        public void StartReceiving()
        {
            try
            {
                IPAddress ipLocal = IPAddress.Parse(serverIP);
                if (tlsServer == null)
                {
                    tlsServer = new TcpListener(ipLocal, Int32.Parse(port));
                }
                UpdateStatus("Starting the server...");
                tlsServer.Start(countOfClient);
                UpdateStatus("The server has started. Please connect the client to " + ipLocal.ToString() + ".");
                TcpClient client = tlsServer.AcceptTcpClient();
                UpdateStatus("The server has accepted the client.");
                strRemote = client.GetStream();
                UpdateStatus("The server has received the stream.");

                int bytesSize = 0;
                byte[] downBuffer = new byte[2048];
                bytesSize = strRemote.Read(downBuffer, 0, 2048);

                string strFileName = System.Text.Encoding.UTF8.GetString(downBuffer, 0, bytesSize);
                strFileName = strFileName.Substring(0, strFileName.IndexOf('\n'));
                strLocal = new FileStream(pathToParDir + strFileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

                downBuffer = new byte[2048];
                bytesSize = strRemote.Read(downBuffer, 0, 2048);
                string strFileSize = System.Text.Encoding.UTF8.GetString(downBuffer, 0, bytesSize);
                strFileSize = strFileSize.Substring(0, strFileSize.IndexOf('\n'));
                long FileSize = Convert.ToInt64(strFileSize);

                UpdateStatus("Receiving file " + strFileName + " (" + FileSize + " bytes)");
                downBuffer = new byte[2048];

                while ((bytesSize = strRemote.Read(downBuffer, 0, downBuffer.Length)) > 0)
                {
                    strLocal.Write(downBuffer, 0, bytesSize);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Erorr!!!");
                UpdateStatus(ex.Message);
                isError = true;
            }
            finally
            {
                if (!isError)
                {
                    UpdateStatus("The file was received.");
                }
                else
                    UpdateStatus("The file was NOT received.");
                isError = false;

                if (strLocal != null)
                {
                    strLocal.Flush();
                    strLocal.Close();
                    strLocal.Dispose();
                    strLocal = null;
                }
                if (strRemote != null)
                {
                    strRemote.Close();
                    strRemote.Dispose();
                    strRemote = null;
                }
                UpdateStatus("Streams are now closed.");

                sw.Flush();
                GC.Collect();
                StartReceiving();
            }
        }

        private void UpdateStatus(string StatusMessage)
        {
            Console.WriteLine(StatusMessage);
            sw.WriteLine(StatusMessage);
        }
    }

    class NetworkSender
    {
        //********************************
        // Отправка сетевых файлов
        //********************************

        private TcpClient tcpClient = null;
        private FileStream fstFile = null;
        private bool isError = false;
        private NetworkStream strRemote = null;
        public StreamWriter sw = null;
        private string pathToLogFile = "/ais/logs/logSender.txt";

        private string clientIP = "172.16.16.8";
        private int clientPort = 3333;

        public NetworkSender()
        {
            sw = new StreamWriter(pathToLogFile);
        }

        private void ConnectToServer()
        {
            tcpClient = new TcpClient();
            try
            {
                tcpClient.Connect(clientIP, clientPort);
                UpdateStatus("Successfully connected to server.");
            }
            catch (Exception exMessage)
            {
                UpdateStatus(exMessage.Message);
            }
        }

        public void SendFile(String pathToFile)
        {
            try
            {
                if (tcpClient == null)
                {
                    ConnectToServer();
                }
                UpdateStatus("Sending file information.");
                strRemote = tcpClient.GetStream();
                byte[] byteSend = new byte[tcpClient.ReceiveBufferSize];
                fstFile = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);
                BinaryReader binFile = new BinaryReader(fstFile);

                FileInfo fInfo = new FileInfo(pathToFile);
                string FileName = fInfo.Name;
                byte[] ByteFileName = new byte[1024];
                FileName = FileName + "\n";
                byte[] tempByteFileName = System.Text.Encoding.UTF8.GetBytes(FileName.ToCharArray());

                for (int i = 0; i < tempByteFileName.Length; i++)
                {
                    ByteFileName[i] = tempByteFileName[i];
                }
                strRemote.Write(ByteFileName, 0, 1024);

                long FileSize = fInfo.Length;
                byte[] ByteFileSize = new byte[1024];
                string strFileSize = FileSize.ToString();
                strFileSize = strFileSize + "\n";
                byte[] tempByteFileSize = System.Text.Encoding.UTF8.GetBytes(strFileSize.ToCharArray());

                for (int i = 0; i < tempByteFileSize.Length; i++)
                {
                    ByteFileSize[i] = tempByteFileSize[i];
                }
                strRemote.Write(ByteFileSize, 0, 2048);

                UpdateStatus("Sending the file " + FileName + ".");

                int bytesSize = 0;
                byte[] downBuffer = new byte[2048];

                while ((bytesSize = fstFile.Read(downBuffer, 0, downBuffer.Length)) > 0)
                {
                    strRemote.Write(downBuffer, 0, bytesSize);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Erorr!!!");
                UpdateStatus(ex.Message);
                isError = true;
            }
            finally
            {
                if (!isError)
                {
                    UpdateStatus("File sent.");
                }
                else
                {
                    UpdateStatus("File does NOT sent.");
                }
                isError = false;

                if (tcpClient != null)
                {
                    tcpClient.Close();
                    tcpClient = null;
                }
                if (strRemote != null)
                {
                    strRemote.Close();
                    strRemote.Dispose();
                    strRemote = null;
                }
                if (fstFile != null)
                {
                    fstFile.Close();
                    fstFile.Dispose();
                    fstFile = null;
                }
                UpdateStatus("Streams and connections are now closed.");
                sw.Flush();
                GC.Collect();
            }
        }

        private void UpdateStatus(string StatusMessage)
        {
            Console.WriteLine(StatusMessage);
            sw.WriteLine(StatusMessage);
        }
    }

    class AsyncNetworkReciever
    {
        //********************************
        // Прием сетевых файлов
        //********************************

        private string pathToLogFile = "/logReciever.txt",
                       pathToDownloadDir = "/",
                       port = "3333",
                       serverIP = "";
        public AsyncNetworkReciever()
        {
            sw = new StreamWriter(pathToLogFile);
         
            if (serverIP == "")
            {
                // Получение имени компьютера.
                String host = System.Net.Dns.GetHostName();
                // Получение ip-адреса.
                System.Net.IPAddress ip = Dns.GetHostByName(host).AddressList[0];
                serverIP = ip.ToString();
            }
        }

        public StreamWriter sw = null;
        private Stream strLocal = null;
        private NetworkStream strRemote = null;
        private TcpListener listener = null;
        private bool isError = false;
        
        public static ManualResetEvent tcpClientConnected = new ManualResetEvent(false);

        public void SetServerIP(String ip)
        {
            serverIP = ip;
        }

        public void SetPort(String port)
        {
            this.port = port;
        }

        // Accept one client connection asynchronously.
        public void StartAsyncReceiving()
        {
            // Set the event to nonsignaled state.
            tcpClientConnected.Reset();

            // Start to listen for connections from a client.
            UpdateStatus("Waiting for a connection...");

            // Accept the connection. 
            // BeginAcceptSocket() creates the accepted socket.
            listener.BeginAcceptTcpClient(new AsyncCallback(DoAcceptTcpClientCallback), listener);

            // Wait until a connection is made and processed before 
            // continuing.

            tcpClientConnected.WaitOne();
        }

        // Process the client connection.
        public void DoAcceptTcpClientCallback(IAsyncResult ar)
        {
            try
            {
                // Get the listener that handles the client request.
                TcpListener listener = (TcpListener)ar.AsyncState;

                // End the operation and display the received data on the console.
                TcpClient client = listener.EndAcceptTcpClient(ar);
                UpdateStatus("The server has accepted the client.");
                strRemote = client.GetStream();
                UpdateStatus("The server has received the stream.");

                int bytesSize = 0;
                byte[] downBuffer = new byte[2048];
                bytesSize = strRemote.Read(downBuffer, 0, 2048);

                string strFileName = System.Text.Encoding.UTF8.GetString(downBuffer, 0, bytesSize);
                strFileName = strFileName.Substring(0, strFileName.IndexOf('\n'));
                strLocal = new FileStream(pathToDownloadDir + strFileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

                downBuffer = new byte[2048];
                bytesSize = strRemote.Read(downBuffer, 0, 2048);
                string strFileSize = System.Text.Encoding.UTF8.GetString(downBuffer, 0, bytesSize);
                strFileSize = strFileSize.Substring(0, strFileSize.IndexOf('\n'));
                long FileSize = Convert.ToInt64(strFileSize);

                UpdateStatus("Receiving file " + strFileName + " (" + FileSize + " bytes)");
                downBuffer = new byte[2048];

                while ((bytesSize = strRemote.Read(downBuffer, 0, downBuffer.Length)) > 0)
                {
                    strLocal.Write(downBuffer, 0, bytesSize);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Erorr!!!");
                UpdateStatus(ex.Message);
                isError = true;
            }
            finally
            {
                if (!isError)
                {
                    UpdateStatus("The file was received.");
                }
                else
                    UpdateStatus("The file was NOT received.");
                isError = false;
                if (strLocal != null)
                {
                    strLocal.Flush();
                    strLocal.Close();
                    strLocal.Dispose();
                    strLocal = null;
                }
                if (strRemote != null)
                {
                    strRemote.Close();
                    strRemote.Dispose();
                    strRemote = null;
                }
                UpdateStatus("Streams are now closed.");
                sw.Flush();
                GC.Collect();
                // Signal the calling thread to continue.
                tcpClientConnected.Set();

                StartAsyncReceiving();
            }
        }

        public void initListener()
        {
            if (listener == null)
            {
                listener = new TcpListener(IPAddress.Parse(serverIP), Int32.Parse(port));
            }
            UpdateStatus("Starting the server...");
            listener.Start();
            UpdateStatus("The server has started. Please connect the client to " + serverIP + ".");
        }

        private void UpdateStatus(string StatusMessage)
        {
            Console.WriteLine(StatusMessage);
            sw.WriteLine(StatusMessage);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // NetworkSender ns = new NetworkSender();
            // ns.SendFile("/ais/Procc.bmp");

            NetworkReciever nr = new NetworkReciever();
            nr.StartReceiving();
            
            //AsyncNetworkReciever anr = new AsyncNetworkReciever();
            //anr.initListener();
            //anr.StartAsyncReceiving();
        }
    }
}