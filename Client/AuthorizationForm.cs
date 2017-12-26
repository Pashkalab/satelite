using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Threading;


namespace Client
{
    public partial class AuthorizationForm : Form
    {
        private bool ConnectedToDB;

        public AuthorizationForm()
        {
            InitializeComponent();
        }

        private void buttonLog_Click(object sender, EventArgs e)
        {
            string login = comboBoxLogin.Text;
            string password = textBoxPassword.Text;

            // Если не все поля заполнены, отказать во входе
            if (!FieldsFilled(login, password))
                return;

            Cursor = Cursors.WaitCursor;
            // Проверка совпадения пароля и логина учётной записи
            if (VerifyMd5Hash(login, password))
            {
                // Переход на главную форму для работы от имени учётной записи
                Thread.Sleep(200);
                Hide();
                ViewForm viewer = new ViewForm(login, password);
                viewer.Show(this);
            }
            else if (ConnectedToDB)
            {
                MessageBox.Show("Неправильний пароль.", "Помилка авторизації", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Cursor = Cursors.Default;
            // Очищаем поле с паролем
            textBoxPassword.Clear();
        }

        private bool VerifyMd5Hash(string login, string password)
        {
            // Получаем хеш эквивалент введенного пароля
            string output;
            using (MD5 md5Hash = MD5.Create())
                output = GetMd5Hash(md5Hash, password);

            // С помощью сервера ищем в базе истинное хеш-значение пароля по логину
            string outputDB = string.Empty;
            try
            {
                outputDB = GetMd5HashFromServer(login, password);
            }
            catch
            {
                // Обрабатываем возможную ошибку подключения к серверу
                MessageBox.Show("На жаль, при підключенні до бази сталася помилка.", "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ConnectedToDB = false;
                return false;
            }

            ConnectedToDB = true;

            // Делаем вывод о равенстве двух полученных паролей
            return outputDB == null ? false : Equals(output, outputDB);
        }
        private string GetMd5Hash(MD5 md5Hash, string input)
        {
            // Переводим данную строку в массив байтов.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Представляем биты как шестнадцатеричную строку
            StringBuilder sBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
                sBuilder.Append(data[i].ToString("x2"));

            return sBuilder.ToString();
        }

        private string GetMd5HashFromServer(string login, string pass)
        {
            // Создаем запрос
            KeyValuePair<string, IList<string>> requestData =
                new KeyValuePair<string, IList<string>>("pass", new List<string> { login, pass, "satelite" });
            // Сериализируем обьект
            var mStream = new MemoryStream();
            var binFormatter = new BinaryFormatter();
            binFormatter.Serialize(mStream, requestData);
            // и получаем массив байтов, представляющий запрос
            byte[] byteRequest = mStream.ToArray();

            KeyValuePair<int, byte[]> response = SendRequestToServer(byteRequest);

            string passFromServer = Encoding.UTF8.GetString(response.Value, 0, response.Key);

            return passFromServer;
        }

        private bool FieldsFilled(params string[] fields)
        {
            // Если есть хоть одно незаполненное поле - ложь
            foreach (string field in fields)
            {
                if (string.IsNullOrEmpty(field))
                {
                    MessageBox.Show("Заповніть усі поля.", "Помилка вводу", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }

            return true;
        }

        private KeyValuePair<int, byte[]> SendRequestToServer(byte[] request, bool withAnswer = true)
        {
            IPHostEntry ipHost = Dns.GetHostEntry("localhost");
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, 11000);
            Socket socket = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            socket.Connect(ipEndPoint);
            socket.Send(request);
            byte[] answer = new byte[8192];
            int bytesRec = 0;
            if (withAnswer)
                bytesRec = socket.Receive(answer);

            socket.Shutdown(SocketShutdown.Both);
            socket.Close();

            return new KeyValuePair<int, byte[]>(bytesRec, answer);
        }
    }
}
