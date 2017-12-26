using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Forms;

namespace Client
{
    public partial class LogInForm : Form
    {
        private string TableName;
        private ViewForm.Operation Operation;

        public LogInForm(string userType, ViewForm.Operation operation)
        {
            InitializeComponent();

            TableName = userType;
            Operation = operation;
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            if (Owner == null)
                return;

            string keyColumnName = string.Empty;
            if (TableName == "reader")
                keyColumnName = "full_name";
            else if (TableName == "publisher")
                keyColumnName = "name";
            try
            {
                string userId = GetUserId(TableName, keyColumnName, txtName.Text);
                ((ViewForm)Owner).ClientID = Convert.ToInt32(userId);
            }
            catch
            {
                MessageBox.Show("Користувача з таким іменем не існує. Перевірте введені дані.", "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show("Вхід був виконаний успішно.", "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
        }

        private string GetUserId(string tableName, string keyColumnName, string value)
        {
            KeyValuePair<string, IList<string>> requestData =
                new KeyValuePair<string, IList<string>>("getUserId", new List<string> { tableName, keyColumnName, value });
            var binFormatter = new BinaryFormatter();
            var mStream = new MemoryStream();
            binFormatter.Serialize(mStream, requestData);
            byte[] byteRequest = mStream.ToArray();

            KeyValuePair<int, byte[]> response = ((ViewForm)Owner).SendRequestToServer(byteRequest);

            mStream = new MemoryStream();
            mStream.Write(response.Value, 0, response.Key);
            mStream.Position = 0;
            string id = Encoding.UTF8.GetString(response.Value, 0, response.Key);

            return id;
        }

        private void LogInForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Owner != null)
                Owner.Enabled = true;
        }
    }
}
