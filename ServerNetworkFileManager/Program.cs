using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace SDKNetworkFileManager
{
    /// <summary>
    /// Класс позволяет принимать файлы по сети
    /// </summary>
    class NetworkReciever
    {
        private TcpListener _tcpListener = null;

        // данные для хранения логов
        private string _pathToLogFile = null;
        public string PathToLogFile
        {
            get { return _pathToLogFile; }
            set { _pathToLogFile = value; }
        }
        private List<string> _logMas = new List<string>();

        // насильно устанавливаемый IP (если установлен не null, то выбирается не автоматически)
        private string _forceMyIP = null;
        public string ForceMyIP
        {
            get { return _forceMyIP; }
            set { _forceMyIP = value; }
        }

        // папка куда будут сохраняться полученные по сети файлы (по умолчанию - корень)
        private string _pathToSaveFolder = "";
        public string PathToSaveFolder
        {
            get { return _pathToSaveFolder; }
            set { _pathToSaveFolder = value; }
        }

        // если true то после получения файла от клиента запуститься новое прослушивание
        bool _needToLongTimeRecieve = true;
        public bool NeedToLongTimeRecieve
        {
            get { return _needToLongTimeRecieve; }
            set { _needToLongTimeRecieve = value; }
        }

        // порт по которому будет вестись прослушивание
        private string _port = "3333";
        public string Port 
        {
            get { return _port; }
            set {_port = value; }
        }

        // количество байт в одном сетевом пакете
        private int _countOfBytesInBuffer = 1024;
        public int CountOfBytesInBuffer
        {
            get { return _countOfBytesInBuffer; }
            set { _countOfBytesInBuffer = value; }
        }

        // максимальное количество ожидающих клиентов
        private int _countOfClient = 100;
        public int CountOFClient
        {
            get { return _countOfClient; }
            set { _countOfClient = value; }
        }

        // нужно ли писать лог в консоль
        private bool _needToWriteToConsole = false;
        public bool NeedToWriteToConsole
        {
            get { return _needToWriteToConsole; }
            set { _needToWriteToConsole = value; }
        }

        // нужно ли писать лог в файл
        private bool _needToWriteLog = true;
        public bool NeedToWriteLog
        {
            get { return _needToWriteLog; }
            set { _needToWriteLog = value; }
        }

        private string _fileStorageFolder = "/ais/data";
        public string FileStorageData
        {
            get { return _fileStorageFolder; }
            set { _fileStorageFolder = value; }
        }

        /// <summary>
        /// Инициализирует объект для приема сетевых файлов
        /// </summary>
        public NetworkReciever()
        {
        }

        /// <summary>
        /// Возвращает IP адрес данного компьютера
        /// </summary>
        /// <returns></returns>
        private string GetCurrentMachineIP()
        {
            String host = System.Net.Dns.GetHostName();
            System.Net.IPAddress ip = System.Net.Dns.GetHostByName(host).AddressList[0];
            return ip.ToString();
        }

        /// <summary>
        /// Выбирает собственный IP если не задан насильно, то вычисляет IP самостоятельно
        /// </summary>
        /// <returns></returns>
        private string SelectMyIP()
        {
            if (_forceMyIP != null)
                return _forceMyIP;
            else
                return GetCurrentMachineIP();
        }

        /// <summary>
        /// Записывает события в лог
        /// </summary>
        /// <param name="statusMessage">Строка с сообщением</param>
        private void UpdateStatus(string statusMessage)
        {
            if (_needToWriteToConsole)
                Console.WriteLine(statusMessage);
            _logMas.Add(statusMessage);
        }

        /// <summary>
        /// Запускает прослушивание
        /// </summary>
        public void StartReceiving()
        {
            NetworkStream networkStream = null;
            TcpClient tcpClient = null;
            bool isError = false;
            string myIP = SelectMyIP();

            try
            {
                // если сервер для прослушивания не инициализирован, то инициализируем
                if (_tcpListener == null)
                    _tcpListener = new TcpListener(IPAddress.Parse(myIP), Int32.Parse(_port));

                UpdateStatus("Starting the server...");
                _tcpListener.Start(_countOfClient);

                UpdateStatus("The server has started");
                UpdateStatus("Please connect the client to " + myIP);

                tcpClient = _tcpListener.AcceptTcpClient();
                UpdateStatus("The server has accepted the client");

                networkStream = tcpClient.GetStream();
                UpdateStatus("The server has received the stream");

                // подождем пока придут пакеты (даем фору по времени клиенту)
                Thread.Sleep(300);

                string command = GetCommandFromNetPackage(networkStream);
                string[] metaInfo = GetMetaInfoFromNetPackage(networkStream);

                switch (command)
                {
                    case "command_load_file":
                        LoadFileFromNet(networkStream, metaInfo[0], metaInfo[1]);
                        break;
                    case "command_file_request":
                        SendFileToClient(metaInfo[0], metaInfo[1]);
                        break;
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
                    UpdateStatus("The file was received");
                else
                    UpdateStatus("The file was NOT received");

                // безопасно закрываем соединение
                CloseCurrentConnection(networkStream, tcpClient);
                UpdateStatus("Streams are now closed \n");
                // записываем лог если есть путь и разрешение для записи и чистим промежуточный лист
                if (_pathToLogFile != null && _needToWriteLog)
                    SaveLog();

                // заново запускаем прослушивание если необходимо
                if (_needToLongTimeRecieve)
                    StartReceiving();
            }
        }

        public void StopReceiving()
        {
            _tcpListener.Stop();
            _tcpListener = null;
        }

        private void SaveLog()
        {
            File.WriteAllLines(_pathToLogFile, _logMas);
            _logMas.Clear();
        }

        private static void CloseCurrentConnection(NetworkStream networkStream, TcpClient tcpClient)
        {
            if (networkStream != null)
            {
                networkStream.Flush();
                networkStream.Close();
            }
            if (tcpClient != null)
            {
                tcpClient.Close();
            }
        }

        private void SendFileToClient(string fileName, string clientIP)
        {
            NetworkSender ns = new NetworkSender(clientIP);
            ns.SendFile(_fileStorageFolder + "/" + fileName);
        }

        private void LoadFileFromNet(NetworkStream networkStream, string fileName, string strSize)
        {
            FileStream fileStream = null;
            try
            {
                // получаем размер файла
                long fileSize = Convert.ToInt64(strSize);
                UpdateStatus("Receiving file '" + fileName + "' (" + fileSize + " bytes)");
                // готовим буфер для чтения
                int bytesSize;
                byte[] downBuffer = new byte[_countOfBytesInBuffer];
                // зная имя файла создаем его и готовим для записи содержимого
                fileStream = new FileStream(_pathToSaveFolder + "/" + fileName, FileMode.Create);
                // считываем содержимое файла по пакетам и записываем его в локальный файл
                while ((bytesSize = networkStream.Read(downBuffer, 0, _countOfBytesInBuffer)) > 0)
                    fileStream.Write(downBuffer, 0, bytesSize);
            }
            catch (Exception ex)
            {
                // пропускаем исключение дальше
                throw ex;
            }
            finally
            {
                if (fileStream != null)
                {
                    fileStream.Flush();
                    fileStream.Close();
                }
            }
        }

        private string[] GetMetaInfoFromNetPackage(NetworkStream networkStream)
        {
            byte[] downBuffer = new byte[_countOfBytesInBuffer * 2];
            networkStream.Read(downBuffer, 0, _countOfBytesInBuffer * 2);
            string str = Encoding.UTF8.GetString(downBuffer, 0, _countOfBytesInBuffer * 2);
            str = str.Substring(0, str.IndexOf('\n'));

            return str.Split(new char[] {'\t'});
        }

        private string GetCommandFromNetPackage(NetworkStream networkStream)
        {
            byte[] downBuffer = new byte[_countOfBytesInBuffer * 2];
            networkStream.Read(downBuffer, 0, _countOfBytesInBuffer * 2);
            string str = Encoding.UTF8.GetString(downBuffer, 0, _countOfBytesInBuffer * 2);
            str = str.Substring(0, str.IndexOf('\n'));

            return str;
        }
    }

    /// <summary>
    /// Класс позволяет отправлять файлы по сети
    /// </summary>
    class NetworkSender
    {
        // текущее подключение по TCP протоколу
        private TcpClient _tcpClient = null;

        // данные для хранения логов
        List<string> _logMas = new List<string>();
        private string _pathToLogFile = null;
        public string PathToLogFile
        {
            get { return _pathToLogFile; }
            set { _pathToLogFile = value; }
        }

        bool _needToWriteLog = true;
        public bool NeedToWriteLog
        {
            get { return _needToWriteLog; }
            set { _needToWriteLog = value; }
        }

        // количество байт в одном сетевом пакете
        private int _countOfBytesInBuffer = 1024;
        public int CountOfBytesInBuffer
        {
            get { return _countOfBytesInBuffer;  }
            set { _countOfBytesInBuffer = value; }
        }

        // порт клиента, по которому он ведет прослушивание
        private int _clientPort = 3333;
        public int ClientPort
        {
            get { return _clientPort; }
            set { _clientPort = value; }
        }

        // нужно ли писать лог в консоль
        private bool _needToWriteToConsole = false;
        public bool NeedToWriteToConsole
        {
            get { return _needToWriteToConsole; }
            set { _needToWriteToConsole = value; }
        }

        // IP адрес клиента
        private string _clientIP = null;

        /// <summary>
        /// Инициализирует объект для отправки файлов по сети
        /// </summary>
        public NetworkSender(string clientIP)
        {
            _clientIP = clientIP;
        }

        /// <summary>
        /// Устанавливает соединение с сервером
        /// </summary>
        /// <param name="clientIP">IP адрес клиента, которому посылаем файл</param>
        private bool ConnectToServer()
        {
            _tcpClient = new TcpClient();
            try
            {
                _tcpClient.Connect(_clientIP, _clientPort);
                UpdateStatus("Successfully connected to server");
                return true;
            }
            catch (Exception exMessage)
            {
                UpdateStatus(exMessage.Message);
                return false;
            }
        }

        /// <summary>
        /// Записывает события в лог
        /// </summary>
        /// <param name="statusMessage">Строка с сообщением</param>
        private void UpdateStatus(string statusMessage)
        {
            if (_needToWriteToConsole)
                Console.WriteLine(statusMessage);
            _logMas.Add(statusMessage);
        }

        /// <summary>
        /// Возвращает IP адрес данного компьютера
        /// </summary>
        /// <returns></returns>
        private string GetCurrentMachineIP()
        {
            String host = System.Net.Dns.GetHostName();
            System.Net.IPAddress ip = System.Net.Dns.GetHostByName(host).AddressList[0];
            return ip.ToString();
        }

        public void SendRequestToGiveFile(string nameOfFile)
        {
            // если не удалось подключиться - выходим
            if ( !ConnectToServer() )
                return;

            bool isError = false;
            NetworkStream networkStream = null;
            try
            {
                // получаем сетевой поток для записи в него отправляемого файла
                networkStream = _tcpClient.GetStream();
                // передаем по сети команду c запросом выслать нам файл (1 пакет)
                SendStringInNetPackage(networkStream, "command_file_request");
                // передаем по сети название файла и собственное IP 
                SendStringInNetPackage(networkStream, nameOfFile + "\t" + GetCurrentMachineIP());
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
                    UpdateStatus("Request sent");
                else
                    UpdateStatus("Request does NOT sent");

                // безопасно завершаем соединение
                CloseConnection(networkStream);
                UpdateStatus("Streams and connections are now closed \n");
                // записываем лог если это нужно
                if (_pathToLogFile != null && _needToWriteLog)
                    SaveLogFile();
            }
        }

        private void SaveLogFile()
        {
            File.WriteAllLines(_pathToLogFile, _logMas);
            _logMas.Clear();
        }

        /// <summary>
        /// Отправляет файл по сети выбранному клиенту
        /// </summary>
        /// <param name="pathToFile">Путь к файлу который необходимо отправить</param>
        /// <param name="clientIP">IP адрес клиента, который ждет получения файла</param>
        public void SendFile(string pathToFile)
        {
            // если не удалось подключиться - выходим
            if ( !ConnectToServer() )
                return;

            bool isError = false;
            NetworkStream networkStream = null;
            FileStream fileStream = null;
            try
            {
                // получаем сетевой поток для записи в него отправляемого файла
                UpdateStatus("Sending file information");
                networkStream = _tcpClient.GetStream();

                // объект для получения информации о передаваемом файле
                FileInfo fInfo = new FileInfo(pathToFile);
                // открываем отправляемый файл
                fileStream = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);

                // передаем команду серверу
                SendStringInNetPackage(networkStream, "command_load_file");
                // передаем мета-данные файла (имя и размер)
                SendStringInNetPackage(networkStream, fInfo.Name + "\t" + fInfo.Length.ToString());

                // готовим буферный массив
                int bytesSize = 0;
                byte[] downBuffer = new byte[_countOfBytesInBuffer];
                // передаем содержание файла по сети в виде последовательности сетевых пакетов
                UpdateStatus("Sending the file '" + fInfo.Name + "'");
                while ((bytesSize = fileStream.Read(downBuffer, 0, _countOfBytesInBuffer)) > 0)
                    networkStream.Write(downBuffer, 0, bytesSize);
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
                    UpdateStatus("File sent");
                else
                    UpdateStatus("File does NOT sent");

                // безопасно завершаем соединение
                CloseConnection(networkStream);
                // закрываем файл
                CloseFile(fileStream);
                UpdateStatus("Streams and connections are now closed \n");
                // записываем лог если это нужно
                if (_pathToLogFile != null && _needToWriteLog)
                    SaveLogFile();
            }
        }

        private static void CloseFile(FileStream fileStream)
        {
            if (fileStream != null)
            {
                fileStream.Flush();
                fileStream.Close();
            }
        }

        private void CloseConnection(NetworkStream networkStream)
        {
            if (_tcpClient != null)
            {
                _tcpClient.Close();
            }
            if (networkStream != null)
            {
                networkStream.Flush();
                networkStream.Close();
            }
        }

        private void SendStringInNetPackage(NetworkStream networkStream, string str)
        {
            byte[] byteFileName = Encoding.UTF8.GetBytes( (str + "\n").ToCharArray() );
            byte[] toWriteName = new byte[_countOfBytesInBuffer * 2];
            byteFileName.CopyTo(toWriteName, 0);
            networkStream.Write(toWriteName, 0, _countOfBytesInBuffer * 2);
        }

        public static void ReleaseTesting()
        {
            string[] names = 
            {
                "Microsoft ADO NET Entity Framework Step by Step 2013.pdf",
                "1L.jpg",
                "cvoverview-110607125849-phpapp02.pdf",
                "OUT__100__percent.png",
           //     "SW_DVD5_Office_Professional_Plus_2013_W32_Russian_MLF_X18-55179.ISO",
                "procc.bmp",
                "Thru.jpg",
                "Новый текстовый документ (Новый).txt"
            };
            // for my pc
            NetworkSender ns = new NetworkSender("172.16.1.24");
            foreach (string next in names)
                ns.SendFile("/test/" + next);

            // for server
            //NetworkSender ns = new NetworkSender("172.16.16.8");
            //foreach (string next in names)
            //    ns.SendFile("/ais/test_data/" + next);
        }

        public static void ReleaseRequestTesting()
        {
            NetworkSender ns = new NetworkSender("172.16.1.24");
            ns.SendRequestToGiveFile("Thru.jpg");
            ns.SendRequestToGiveFile("Microsoft ADO NET Entity Framework Step by Step 2013.pdf");
            ns.SendRequestToGiveFile("1L.jpg");
            ns.SendRequestToGiveFile("cvoverview-110607125849-phpapp02.pdf");
            ns.SendRequestToGiveFile("OUT__100__percent.png");
            ns.SendRequestToGiveFile("procc.bmp");
            ns.SendRequestToGiveFile("Thru.jpg");
          //  ns.SendRequestToGiveFile("SW_DVD5_Office_Professional_Plus_2013_W32_Russian_MLF_X18-55179.ISO");
            ns.SendRequestToGiveFile("Новый текстовый документ (Новый).txt");
        }
    }

    public static class Network
    {
        private static string
            _serverIP = "192.168.1.4",
            _pathToSaveFolder = null,
            _pathToLogFileRec = null,
            _pathToLogFileSender = null;
        public static string ServerIP
        {
            get { return _serverIP; }
            set { _serverIP = value; }
        }

        public static string PathToSaveFolder
        {
            get { return _pathToSaveFolder; }
            set { _pathToSaveFolder = value; }
        }

        public static string PathToLogFileRec
        {
            get { return _pathToLogFileRec; }
            set { _pathToLogFileRec = value; }
        }

        public static string PathToLogFileSender
        {
            get { return _pathToLogFileSender; }
            set { _pathToLogFileSender = value; }
        }

        private static void StartContListen()
        {
            NetworkReciever nr = new NetworkReciever();
            nr.NeedToLongTimeRecieve = true;
            if (_pathToSaveFolder != null)
                nr.PathToSaveFolder = _pathToSaveFolder;
            if (_pathToLogFileRec != null)
                nr.PathToLogFile = _pathToLogFileRec;

            nr.NeedToWriteToConsole = true;
            nr.NeedToWriteLog = false;

            nr.StartReceiving();
        }

        private static void StartSingleListen()
        {
            NetworkReciever nr = new NetworkReciever();
            nr.NeedToLongTimeRecieve = false;
            if (_pathToSaveFolder != null)
                nr.PathToSaveFolder = _pathToSaveFolder;
            if (_pathToLogFileRec != null)
                nr.PathToLogFile = _pathToLogFileRec;
            nr.StartReceiving();
            nr.StopReceiving();
        }

        public static Thread StartContReceiving()
        {
            Thread threadForListen = new Thread(StartContListen);
            threadForListen.Start();

            return threadForListen;
        }

        public static Thread StartSingleReceiving()
        {
            Thread threadForListen = new Thread(StartSingleListen);
            threadForListen.Start();

            return threadForListen;
        }

        public static void DownloadFile(string nameOfFile)
        {
            // включаем фоновое прослушивание чтобы скачать файл
            Thread threadForListen = StartSingleReceiving();

            // отправляем запрос серверу с просьбой выслать нам файл
            NetworkSender ns = new NetworkSender(_serverIP);
            if (_pathToLogFileSender != null)
                ns.PathToLogFile = _pathToLogFileSender;
            ns.SendRequestToGiveFile(nameOfFile);

            // ждем пока файл не скачается
            threadForListen.Join();
        }

        public static void UploadFile(string pathToFile)
        {
             NetworkSender ns = new NetworkSender(_serverIP);

            ns.NeedToWriteToConsole = true;
            ns.NeedToWriteLog = false;

             if (_pathToLogFileSender != null)
                 ns.PathToLogFile = _pathToLogFileSender;
             ns.SendFile(pathToFile);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Network.UploadFile("test.txt");
            Console.ReadKey();

            //  Network.StartContReceiving();
        }
    }
}