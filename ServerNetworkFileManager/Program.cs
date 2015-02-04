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
        TcpListener tcpListener = null;

        // данные для хранения логов
        private string _pathToLogFile = null;
        private List<string> _logMas = new List<string>();

        // насильно устанавливаемый IP
        private string _forceMyIP = null;
        string ForceMyIP
        {
            get { return _forceMyIP; }
            set { _forceMyIP = value; }
        }

        // папка куда будут сохраняться полученные по сети файлы
        private string _pathToSaveFolder = "/";
        string PathToSaveFolder
        {
            get { return _pathToSaveFolder; }
            set { _pathToSaveFolder = value; }
        }

        // если true то после получения файла от клиента запуститься новое прослушивание
        bool _needToLongTimeRecieve = true;
        bool NeedToLongTimeRecieve
        {
            get { return _needToLongTimeRecieve; }
            set { _needToLongTimeRecieve = value; }
        }

        // порт по которому будет вестись прослушивание
        private string _port = "3333";
        // количество байт в одном сетевом пакете
        private int _countOfBytesInBuffer = 1024;
        // максимальное количество ожидающих клиентов
        private int _countOfClient = 100;
        // нужно ли писать лог в консоль
        private bool _needToWriteToConsole = true;
        bool NeedToWriteToConsole
        {
            get { return _needToWriteToConsole; }
            set { _needToWriteToConsole = value; }
        }

        /// <summary>
        /// Инициализирует объект для приема сетевых файлов
        /// </summary>
        /// <param name="pathToLogFile">Путь где будет храниться файл с логом</param>
        public NetworkReciever(string pathToLogFile)
        {
            _pathToLogFile = pathToLogFile;
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
            FileStream fileStream = null;
            NetworkStream networkStream = null;
            TcpClient tcpClient = null;
            bool isError = false;
            string myIP = SelectMyIP();

            try
            {
                IPAddress ipLocal = IPAddress.Parse(myIP);
                // если сервер для прослушивания не инициализирован, то инициализируем
                if (tcpListener == null)
                {
                    tcpListener = new TcpListener(ipLocal, Int32.Parse(_port));
                }

                UpdateStatus("Starting the server...");
                tcpListener.Start(_countOfClient);

                UpdateStatus("The server has started");
                UpdateStatus("Please connect the client to " + ipLocal.ToString());

                tcpClient = tcpListener.AcceptTcpClient();
                UpdateStatus("The server has accepted the client");

                networkStream = tcpClient.GetStream();
                UpdateStatus("The server has received the stream");

                // буфер для чтения по сетевому потоку и текущий размер буфера в байтах
                byte[] downBuffer;
                int bytesSize;

                // получаем имя получаемого файла с расширением
                downBuffer = new byte[_countOfBytesInBuffer];
                bytesSize = networkStream.Read(downBuffer, 0, _countOfBytesInBuffer);
                string fileName = System.Text.Encoding.UTF8.GetString(downBuffer, 0, bytesSize);
                fileName = fileName.Substring(0, fileName.IndexOf('\n'));

                // зная имя файла создаем его и готовим для записи содержимого
                fileStream = new FileStream(_pathToSaveFolder + fileName, FileMode.Create);

                // получаем размер файла
                downBuffer = new byte[_countOfBytesInBuffer];
                bytesSize = networkStream.Read(downBuffer, 0, _countOfBytesInBuffer);
                string strFileSize = System.Text.Encoding.UTF8.GetString(downBuffer, 0, bytesSize);
                strFileSize = strFileSize.Substring(0, strFileSize.IndexOf('\n'));
                long fileSize = Convert.ToInt64(strFileSize);

                UpdateStatus("Receiving file '" + fileName + "' (" + fileSize + " bytes)");

                // считываем содержимое файла по пакетам и записываем его в локальный файл
                downBuffer = new byte[_countOfBytesInBuffer];
                while ((bytesSize = networkStream.Read(downBuffer, 0, downBuffer.Length)) > 0)
                {
                    fileStream.Write(downBuffer, 0, bytesSize);
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

                // безопасно закрываем соединение
                if (fileStream != null)
                {
                    fileStream.Flush();
                    fileStream.Close();
                }
                if (networkStream != null)
                {
                    networkStream.Flush();
                    networkStream.Close();
                }
                if (tcpClient != null)
                {
                    tcpClient.Close();
                }
                UpdateStatus("Streams are now closed \n");

                // записываем лог и чистим промежуточный лист
                if (_pathToLogFile != null)
                {
                    File.AppendAllLines(_pathToLogFile, _logMas);
                    _logMas.Clear();
                }

                // заново запускаем прослушивание если необходимо
                if (_needToLongTimeRecieve)
                    StartReceiving();
            }
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
        private string _pathToLogFile = null;
        List<string> _logMas = new List<string>();

        // количество байт в одном сетевом пакете
        private int _countOfBytesInBuffer = 1024;
        // порт клиента, по которому он ведет прослушивание
        private int _clientPort = 3333;
        private string _clientIP = null;

        // нужно ли писать лог в консоль
        private bool _needToWriteToConsole = true;
        bool NeedToWriteToConsole
        {
            get { return _needToWriteToConsole; }
            set { _needToWriteToConsole = value; }
        }

        /// <summary>
        /// Инициализирует объект для отправки файлов по сети
        /// </summary>
        /// <param name="pathToLogFile">Путь по которому будет сохранем файл с логами</param>
        public NetworkSender(string pathToLogFile, string clientIP)
        {
            _pathToLogFile = pathToLogFile;
            _clientIP = clientIP;
        }

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
        private void ConnectToServer()
        {
            _tcpClient = new TcpClient();
            try
            {
                _tcpClient.Connect(_clientIP, _clientPort);
                UpdateStatus("Successfully connected to server");
            }
            catch (Exception exMessage)
            {
                UpdateStatus(exMessage.Message);
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
        /// Отправляет файл по сети выбранному клиенту
        /// </summary>
        /// <param name="pathToFile">Путь к файлу который необходимо отправить</param>
        /// <param name="clientIP">IP адрес клиента, который ждет получения файла</param>
        public void SendFile(string pathToFile)
        {
            ConnectToServer();
            // если не инициализирован клиент, дальнейшая работа бессмысленна
            if (_tcpClient == null)
                return;

            bool isError = false;
            NetworkStream networkStream = null;
            FileStream fileStream = null;
            try
            {
                // получаем сетевой поток для записи в него отправляемого файла
                UpdateStatus("Sending file information");
                networkStream = _tcpClient.GetStream();

                // открываем отправляемый файл
                fileStream = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);

                // объект для получения информации о передаваемом файле
                FileInfo fInfo = new FileInfo(pathToFile);

                // передаем по сети название файла в виде массива байтов (1 пакет)
                byte[] byteFileName = System.Text.Encoding.UTF8.GetBytes((fInfo.Name + "\n").ToCharArray());
                byte[] toWriteName = new byte[_countOfBytesInBuffer];
                byteFileName.CopyTo(toWriteName, 0);
                networkStream.Write(toWriteName, 0, _countOfBytesInBuffer);

                // передаем по сети размер файла в байтах в виде массива байтов (2 пакет)
                byte[] byteFileSize = System.Text.Encoding.UTF8.GetBytes((fInfo.Length.ToString() + "\n").ToCharArray());
                byte[] toWriteSize = new byte[_countOfBytesInBuffer];
                byteFileSize.CopyTo(toWriteSize, 0);
                networkStream.Write(toWriteSize, 0, _countOfBytesInBuffer);

                UpdateStatus("Sending the file '" + fInfo.Name + "'");

                // передаем содержание файла по сети в виде последовательности сетевых пакетов
                int bytesSize = 0;
                byte[] downBuffer = new byte[_countOfBytesInBuffer];
                while ((bytesSize = fileStream.Read(downBuffer, 0, _countOfBytesInBuffer)) > 0)
                {
                    networkStream.Write(downBuffer, 0, bytesSize);
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

                // безопасно завершаем соединение
                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                    _tcpClient = null;
                }
                if (networkStream != null)
                {
                    networkStream.Flush();
                    networkStream.Close();
                }
                if (fileStream != null)
                {
                    fileStream.Flush();
                    fileStream.Close();
                }
                UpdateStatus("Streams and connections are now closed \n");

                // записываем лог если это нужно
                if (_pathToLogFile != null)
                {
                    File.AppendAllLines(_pathToLogFile, _logMas);
                    _logMas.Clear();
                }
            }
        }

        public static void ReleaseTesting()
        {
            string[] names = 
            {
                "Microsoft ADO NET Entity Framework Step by Step 2013.pdf",
                "1L.jpg",
                "cvoverview-110607125849-phpapp02.pdf",
                "OUT__100__percent.png",
              //  "SW_DVD5_Office_Professional_Plus_2013_W32_Russian_MLF_X18-55179.ISO",
                "procc.bmp",
                "Thru.jpg",
                "Новый текстовый документ (Новый).txt"
            };
            NetworkSender ns = new NetworkSender("172.16.1.24");
            foreach (string next in names)
                ns.SendFile("/test/" + next);
        }
    }

    class Program
    {
        public static void StartListen()
        {
            NetworkReciever nr = new NetworkReciever();
            nr.StartReceiving();
        }

        static void Main(string[] args)
        {
            NetworkSender.ReleaseTesting();

            //Thread backListen = new Thread(StartListen);
            //backListen.Start();

            //AsyncNetworkReciever anr = new AsyncNetworkReciever();
            //anr.initListener();
            //anr.StartAsyncReceiving();
        }
    }
}