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

        private string _pathToLogFile = null;
        private string port = "3333";
        private string serverIP = null;

        private int countOfBytesInBuffer = 1024;
        private int countOfClient = 100;
        private string pathToSaveFolder = "/";

        private Stream strLocal = null;
        private NetworkStream strRemote = null;
        private TcpListener tlsServer = null;
        private bool isError = false;
        private List<string> logMas = new List<string>();

        public NetworkReciever(string pathToLogFile)
        {
            _pathToLogFile = pathToLogFile;

            serverIP = GetCurrentIP();
        }

        public NetworkReciever()
        {
            serverIP = GetCurrentIP();
        }

        public string GetCurrentIP()
        {
            String host = System.Net.Dns.GetHostName();
            System.Net.IPAddress ip = System.Net.Dns.GetHostByName(host).AddressList[0];

            return ip.ToString();
        }

        public void SetServerIP(string ip)
        {
            serverIP = ip;
        }

        public void SetPort(string port)
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
                UpdateStatus("The server has started");
                UpdateStatus("Please connect the client to " + ipLocal.ToString());
                TcpClient client = tlsServer.AcceptTcpClient();
                UpdateStatus("The server has accepted the client");
                strRemote = client.GetStream();
                UpdateStatus("The server has received the stream");

                int bytesSize = 0;
                byte[] downBuffer = new byte[countOfBytesInBuffer];
                bytesSize = strRemote.Read(downBuffer, 0, countOfBytesInBuffer);

                string strFileName = System.Text.Encoding.UTF8.GetString(downBuffer, 0, bytesSize);
                strFileName = strFileName.Substring(0, strFileName.IndexOf('\n'));
                strLocal = new FileStream(pathToSaveFolder + strFileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

                downBuffer = new byte[countOfBytesInBuffer];
                bytesSize = strRemote.Read(downBuffer, 0, countOfBytesInBuffer);
                string strFileSize = System.Text.Encoding.UTF8.GetString(downBuffer, 0, bytesSize);
                strFileSize = strFileSize.Substring(0, strFileSize.IndexOf('\n'));
                long FileSize = Convert.ToInt64(strFileSize);

                UpdateStatus("Receiving file " + strFileName + " (" + FileSize + " bytes)");
                downBuffer = new byte[countOfBytesInBuffer];

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
                    UpdateStatus("The file was received");
                }
                else
                    UpdateStatus("The file was NOT received");
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
                    strRemote.Flush();
                    strRemote.Close();
                    strRemote.Dispose();
                    strRemote = null;
                }
                UpdateStatus("Streams are now closed \n");

                // записываем лог и чистим промежуточный лист
                if (_pathToLogFile != null)
                {
                    File.AppendAllLines(_pathToLogFile, logMas);
                    logMas.Clear();
                }

                // заново запускаем прослушивание...
                StartReceiving();
            }
        }

        private void UpdateStatus(string statusMessage)
        {
            Console.WriteLine(statusMessage);
            logMas.Add(statusMessage);
        }
    }

    class NetworkSender
    {
        //********************************
        // Отправка сетевых файлов
        //********************************

        private int countOfBytesInBuffer = 1024;
        private string clientIP = "172.16.1.24";
        private int clientPort = 3333;

        private TcpClient tcpClient = null;
        private FileStream fstFile = null;
        private bool isError = false;
        private NetworkStream strRemote = null;
        private string _pathToLogFile = null;
        List<string> logMas = new List<string>();

        public NetworkSender(string pathToLogFile)
        {
            _pathToLogFile = pathToLogFile;
        }

        public NetworkSender()
        {

        }

        private void ConnectToServer()
        {
            tcpClient = new TcpClient();
            try
            {
                tcpClient.Connect(clientIP, clientPort);
                UpdateStatus("Successfully connected to server");
            }
            catch (Exception exMessage)
            {
                UpdateStatus(exMessage.Message);
            }
        }

        public void SendFile(string pathToFile)
        {
            try
            {
                if (tcpClient == null)
                {
                    ConnectToServer();
                }
                UpdateStatus("Sending file information");
                strRemote = tcpClient.GetStream();
                byte[] byteSend = new byte[tcpClient.ReceiveBufferSize];
                fstFile = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);
                BinaryReader binFile = new BinaryReader(fstFile);

                FileInfo fInfo = new FileInfo(pathToFile);
                string fileName = fInfo.Name;
                byte[] ByteFileName = new byte[countOfBytesInBuffer];
                byte[] tempByteFileName = System.Text.Encoding.UTF8.GetBytes( (fileName + "\n").ToCharArray() );

                for (int i = 0; i < tempByteFileName.Length; i++)
                {
                    ByteFileName[i] = tempByteFileName[i];
                }
                strRemote.Write(ByteFileName, 0, countOfBytesInBuffer);

                long FileSize = fInfo.Length;
                byte[] ByteFileSize = new byte[countOfBytesInBuffer];
                string strFileSize = FileSize.ToString();
                strFileSize = strFileSize + "\n";
                byte[] tempByteFileSize = System.Text.Encoding.UTF8.GetBytes(strFileSize.ToCharArray());

                for (int i = 0; i < tempByteFileSize.Length; i++)
                {
                    ByteFileSize[i] = tempByteFileSize[i];
                }
                strRemote.Write(ByteFileSize, 0, countOfBytesInBuffer);

                UpdateStatus("Sending the file '" + fileName + "'");

                int bytesSize = 0;
                byte[] downBuffer = new byte[countOfBytesInBuffer];

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
                    UpdateStatus("File sent");
                }
                else
                {
                    UpdateStatus("File does NOT sent");
                }
                isError = false;

                if (tcpClient != null)
                {
                    tcpClient.Close();
                    tcpClient = null;
                }
                if (strRemote != null)
                {
                    strRemote.Flush();
                    strRemote.Close();
                    strRemote.Dispose();
                    strRemote = null;
                }
                if (fstFile != null)
                {
                    fstFile.Flush();
                    fstFile.Close();
                    fstFile.Dispose();
                    fstFile = null;
                }
                UpdateStatus("Streams and connections are now closed \n");

                // записываем лог
                if (_pathToLogFile != null)
                {
                    File.AppendAllLines(_pathToLogFile, logMas);
                    logMas.Clear();
                }
            }
        }

        private void UpdateStatus(string statusMessage)
        {
            Console.WriteLine(statusMessage);
            logMas.Add(statusMessage);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            //NetworkSender ns = new NetworkSender();
            //ns.SendFile(args[0]);

            NetworkSender ns = new NetworkSender();
            ns.SendFile("/генетический алгоритм.pdf");

            //NetworkReciever nr = new NetworkReciever();
            //nr.StartReceiving();

            //AsyncNetworkReciever anr = new AsyncNetworkReciever();
            //anr.initListener();
            //anr.StartAsyncReceiving();
        }
    }
}