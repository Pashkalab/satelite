using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Forms;

namespace Client
{
    public partial class InsertForm : Form
    {
        private DataTable Table;
        private ViewForm.Operation Operation;
        private InterfaceGenerator Generator;

        public InsertForm(DataTable table, ViewForm.Operation operation)
        {
            InitializeComponent();

            Table = table;
            Operation = operation;

            // Помещаем на форму поля с подписями для заполнения
            CreateInterface();
        }

        private void CreateInterface()
        {
            int y_top = 20, delta = 40;
            Generator = new InterfaceGenerator(Table);
            Control[] dataElements = Generator.CreateInterface(y_top, delta, 170, new Size(180, 22));
            Controls.AddRange(dataElements);

            // Подстраиваем положение кнопки и размер формы
            int y = y_top + Generator.GetColumnNames().Count * delta;
            Size = new Size(400, y + delta * 2);
            buttonOK.Location = new Point(153, y + delta * 2 - 80);
        }
        /// <summary>
        /// Forms INSERT query and execute it
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            TextBox[] textBoxes = Controls.OfType<TextBox>().ToArray();
            string values = GetValues(textBoxes);
            if (values == null)
                return;

            try
            {
                if (Table.Columns.Contains("id"))
                    ((ViewForm)Owner).InsertOneWithId(Table.TableName, values);
                else
                    ((ViewForm)Owner).InsertOneWithoutId(Table.TableName, values);
            }
            catch (Exception exception)
            {
                MessageBox.Show(string.Format("На жаль, при виконанні сталася помилка.{0} Повідомлення: {1}", Environment.NewLine, exception.Message),
                    "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show("Запис був успішно доданий.", "Шановний користувач!", MessageBoxButtons.OK,  MessageBoxIcon.Information);
            ClearTextBoxes();
        }
        private string GetValues(TextBox[] textBoxes)
        {
            Label[] labels = Controls.OfType<Label>().ToArray();

            FieldsValidator validator = new FieldsValidator();
            StringBuilder values = new StringBuilder();

            string newValue;
            DataColumn column;
            for (int i = 0; i < textBoxes.Length; i++)
            {
                newValue = textBoxes[i].Text.Trim();
                column = Table.Columns[Table.Columns.IndexOf(labels[i].Text)];
                if (!validator.CheckField(column, newValue))
                {
                    MessageBox.Show(string.Format("Поле '{0}' заповнене невірно.", Table.Columns[labels[i].Text].ColumnName),
                                    "Уважаемый пользователь!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return null;
                }

                // Изменить поля с датой в соответствие формату в БД
                if (column.DataType.Name.Equals("DateTime"))
                    newValue = validator.ConvertToPostgreSQLDate(newValue);

                if (string.IsNullOrEmpty(newValue))
                    values.Append(" DEFAULT");
                else
                    values.Append(" '").Append(newValue).Append("'");
                if (i + 1 < textBoxes.Length)
                    values.Append(",");
            }

            return values.ToString();
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

        private void ClearTextBoxes()
        {
            Array.ForEach(Controls.OfType<TextBox>().ToArray(), box => box.Clear());
        }

        private void InsertForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Owner != null)
                ((ViewForm)Owner).DisplayTableInfo(Table.TableName);
        }
    }
}
