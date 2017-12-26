using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace Server
{
    public class Program
    {
        private static PostgreSqlHandler Handler;
        private static byte[] ClientBuffer;
        private static Socket Socket;

        private static void Main(string[] args)
        {
            // Устанавливаем для сокета локальную конечную точку
            IPHostEntry ipHost = Dns.GetHostEntry("localhost");
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, 11000);
            // Создаем сокет Tcp/Ip для прослушки
            Socket listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                // Назначаем сокет локальной конечной точке и слушаем входящие сокеты
                listener.Bind(ipEndPoint);
                listener.Listen(10);

                // Начинаем слушать соединения
                while (true)
                {
                    Console.WriteLine("Ожидаем соединение через порт {0}", ipEndPoint);
                    ClientBuffer = new byte[3072];

                    // Программа приостанавливается, ожидая входящее соединение
                    Socket = listener.Accept();
                    new Thread(Run).Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                Console.ReadKey();
            }
        }

        private static void Run()
        {
            int bytesRec = Socket.Receive(ClientBuffer);

            // Получаем массив байтов, представляющий объект
            var mStream = new MemoryStream();
            mStream.Write(ClientBuffer, 0, bytesRec);
            mStream.Position = 0;
            // Десериализируеи переданный объект
            var binFormatter = new BinaryFormatter();
            object requestData = null;
            byte[] answer = null;
            try
            {
                requestData = (KeyValuePair<string, IList<string>>)binFormatter.Deserialize(mStream);
                answer = GetAnswerForRequest((KeyValuePair<string, IList<string>>)requestData);
            }
            catch (Exception e)
            {
                try
                {
                    Console.WriteLine(e.Message);
                    mStream.Position = 0;
                    requestData = (KeyValuePair<string, IList<string>[]>)binFormatter.Deserialize(mStream);
                    answer = GetAnswerForRequest((KeyValuePair<string, IList<string>[]>)requestData);
                }
                catch
                {
                    Console.WriteLine(e);
                    Console.ReadKey();
                }
            }
            Console.Write("Полученный объект: " + requestData + "\n");

            if (answer != null)
                Socket.Send(answer);
            
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Close();
        }

        private static byte[] GetAnswerForRequest(KeyValuePair<string, IList<string>> requestData)
        {
            string operationKeyWord = requestData.Key;
            switch (operationKeyWord)
            {
                /*case "initHandler":
                    string login = requestData.Value[0];
                    string password = requestData.Value[1];
                    string database = requestData.Value[2];
                    Handler = new PostgreSqlHandler(login, password, database);
                    break;
                case "pass":
                    return GetPassword(requestData.Value);*/
                case "pass":
                    string login = requestData.Value[0];
                    string password = requestData.Value[1];
                    string database = requestData.Value[2];
                    Handler = new PostgreSqlHandler(login, password, database);
                    return GetPassword(requestData.Value);
                case "tablesNames":
                    return GetTablesNames();
                case "table":
                    return GetTable(requestData.Value);
                case "sampleFromTable":
                    return GetSampleFromTable(requestData.Value);
                case "delete":
                    ExecuteDelete(requestData.Value);
                    break;
                case "updateOneValue":
                    UpdateOneValue(requestData.Value);
                    break;
                case "insertWithId":
                    ExecuteInsertWithId(requestData.Value);
                    break;
                case "insertWithoutId":
                    ExecuteInsert(requestData.Value);
                    break;
                case "getUserId":
                    return GetUserId(requestData.Value);
                case "getHistory":
                    return GetOrdersHistoryByReader(requestData.Value);
                case "getBooks":
                    return GetBooksByPublisher(requestData.Value);
            }
            return null;
        }
        private static byte[] GetAnswerForRequest(KeyValuePair<string, IList<string>[]> requestData)
        {
            string operationKeyWord = requestData.Key;
            switch (operationKeyWord)
            {
                case "updateOnWithId":
                    ExecuteUpdateOneWithId(requestData.Value);
                    break;
            }
            return null;
        }

        private static byte[] GetPassword(IList<string> parameters)
        {
            if (parameters.Count < 1)
                return null;

            string login = parameters[0];
            string reply = new PostgreSqlHandler("postgres", "12345", "sateliteUsers").GetPassword(login);
            return Encoding.UTF8.GetBytes(reply);
        }

        private static byte[] GetTablesNames()
        {
            IList<string> tablesNames = Handler.GetTableNames();

            var mStream = new MemoryStream();
            new BinaryFormatter().Serialize(mStream, tablesNames);
            return mStream.ToArray();
        }

        private static byte[] GetTable(IList<string> parameters)
        {
            if (parameters.Count < 2)
                return null;

            string tableName = parameters[0];
            string sortColumnName = parameters[1];
            DataTable table = Handler.GetTable(tableName, sortColumnName);

            var mStream = new MemoryStream();
            new BinaryFormatter().Serialize(mStream, table);
            return mStream.ToArray();
        }
        private static byte[] GetSampleFromTable(IList<string> parameters)
        {
            if (parameters.Count < 2)
                return null;

            string tableName = parameters[0];
            string keyPhrase = parameters[1];
            string searchColumnName = parameters[2];
            bool separate = parameters[3].Equals("1");
            string sortColumnName = parameters[4];
            DataTable table = Handler.GetSampleFromTable(tableName, keyPhrase, searchColumnName, separate, sortColumnName);

            var mStream = new MemoryStream();
            new BinaryFormatter().Serialize(mStream, table);
            return mStream.ToArray();
        }

        private static void ExecuteDelete(IList<string> parameters)
        {
            if (parameters.Count < 3)
                return;

            string tableName = parameters[0];
            string columnName = parameters[1];
            IList<string> values = new List<string>();
            for (int i = 2; i < parameters.Count; i++)
                values.Add(parameters[i]);
            Handler.DeleteManyWithOneColumn(tableName, columnName, values);
        }

        private static void UpdateOneValue(IList<string> parameters)
        {
            if (parameters.Count < 5)
                return;

            string tableName = parameters[0];
            string updateColumnName = parameters[1];
            string updateValue = parameters[2];
            string whereColumnName = parameters[3];
            string whereValue = parameters[4];
            Handler.UpdateOneValueWithAnyColumn(tableName, updateColumnName, updateValue, whereColumnName, whereValue);
        }
        private static void ExecuteUpdateOneWithId(IList<string>[] parameters)
        {
            if (parameters.Length < 4)
                return;

            string tableName = parameters[0][0];
            IList<string> columns = parameters[1];
            IList<string> values = parameters[2];
            int id = Convert.ToInt32(parameters[3][0]);
            Handler.UpdateOneWithId(tableName, columns, values, id);
        }

        private static void ExecuteInsertWithId(IList<string> parameters)
        {
            if (parameters.Count < 2)
                return;

            string tableName = parameters[0];
            string values = parameters[1];
            Handler.InsertOneWithId(tableName, values);
        }
        private static void ExecuteInsert(IList<string> parameters)
        {
            if (parameters.Count < 2)
                return;

            string tableName = parameters[0];
            string values = parameters[1];
            Handler.InsertOneWithoutId(tableName, values);
        }

        private static byte[] GetUserId(IList<string> parameters)
        {
            if (parameters.Count < 3)
                return null;

            string tableName = parameters[0];
            string columnName = parameters[1];
            string value = parameters[2];
            string userId = Handler.GetUserId(tableName, columnName, value).ToString();
            return Encoding.UTF8.GetBytes(userId);
        }

        private static byte[] GetOrdersHistoryByReader(IList<string> parameters)
        {
            if (parameters.Count < 1)
                return null;

            int id = Convert.ToInt32(parameters[0]);
            DataTable table = Handler.GetOrdersHistoryByReader(id);

            if (table == null)
                return new byte[0];

            var mStream = new MemoryStream();
            new BinaryFormatter().Serialize(mStream, table);
            return mStream.ToArray();
        }
        private static byte[] GetBooksByPublisher(IList<string> parameters)
        {
            if (parameters.Count < 1)
                return null;

            int id = Convert.ToInt32(parameters[0]);
            DataTable table = Handler.GetBooksByPublisher(id);

            if (table == null)
                return new byte[0];

            var mStream = new MemoryStream();
            new BinaryFormatter().Serialize(mStream, table);
            return mStream.ToArray();
        }
    }
}
