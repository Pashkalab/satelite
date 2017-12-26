using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Excel = Microsoft.Office.Interop.Excel;

namespace Client
{
    public partial class ViewForm : Form
    {
        #region properties
        public enum Operation { Insert, Delete, Update, None }
        //private PostrgeSQLHandler Handler;
        private string UserLogin;
        public int ClientID = -1;
        private bool DataBaseIsEmpty;
        private Operation CurrentOperation;
        private Dictionary<KeyValuePair<int, int>, string> CellsUpdated;
        #endregion

        public ViewForm(string login, string pass)
        {
            InitializeComponent();

            UserLogin = login;
            CellsUpdated = new Dictionary<KeyValuePair<int, int>, string>();

            ValidateDataBase();
            if (!DataBaseIsEmpty)
                DisplayTableInfo("satellite");

            CheckUserPriviliges();
        }
        /// <summary>
        /// Sets DataBaseIsEmpty true when 0 tables given
        /// and blocks functionality that depends on tables
        /// </summary>
        private void ValidateDataBase()
        {
            // При отсутсвии таблиц - блокировать возможные операции с таблицами  
            IList<string> tableNames = GetTablesNames();
            if (tableNames.Count == 0)
            {
                DataBaseIsEmpty = true;
                operationsToolStripMenuItem.Enabled = false;
                reportToolStripMenuItem.Enabled = false;
                pictureBoxSearch.Enabled = false;
            }
        }
        /// <summary>
        /// Disable/Enables elements depending on user
        /// </summary>
        private void CheckUserPriviliges()
        {
            if (UserLogin == "client")
            {
                reportToolStripMenuItem.Enabled = operationsToolStripMenuItem.Enabled = false;
                foreach (ToolStripItem item in tablesToolStripMenuItem.DropDownItems)
                    if (item.Text != "satellite" && item.Text != "designer" &&
                        item.Text != "satelite_designer" && item.Text != "manufacturer")
                        item.Visible = false;
            }
        }

        #region ServerCalls
        public KeyValuePair<int, byte[]> SendRequestToServer(byte[] request)
        {
            IPHostEntry ipHost = Dns.GetHostEntry("localhost");
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, 11000);
            Socket socket = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            byte[] answer = new byte[102400];
            int bytesRec = 0;
            try
            {
                socket.Connect(ipEndPoint);
                socket.Send(request);
                bytesRec = socket.Receive(answer);

                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show("Вибачте за незручності. Проблема спілкування з сервером. " + e.Message, "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            return new KeyValuePair<int, byte[]>(bytesRec, answer);
        }

        private IList<string> GetTablesNames()
        {
            IList<string> tablesNames = null;
            try
            {
                KeyValuePair<string, IList<string>> requestData =
                    new KeyValuePair<string, IList<string>>("tablesNames", new List<string>());
                var binFormatter = new BinaryFormatter();
                var mStream = new MemoryStream();
                binFormatter.Serialize(mStream, requestData);
                byte[] byteRequest = mStream.ToArray();

                KeyValuePair<int, byte[]> response = SendRequestToServer(byteRequest);

                if (response.Key == 0)
                    return null;

                mStream = new MemoryStream();
                mStream.Write(response.Value, 0, response.Key);
                mStream.Position = 0;
                tablesNames = (IList<string>)binFormatter.Deserialize(mStream);
            }
            catch (Exception e)
            {
                MessageBox.Show("Вибачте за незручності. Проблема спілкування з сервером. " + e.Message, "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }

            return tablesNames;
        }

        private DataTable GetTable(string tableName, string sortColumnName = "")
        {
            DataTable table = null;
            try
            {
                KeyValuePair<string, IList<string>> requestData =
                    new KeyValuePair<string, IList<string>>("table", new List<string> { tableName, sortColumnName });
                var binFormatter = new BinaryFormatter();
                var mStream = new MemoryStream();
                binFormatter.Serialize(mStream, requestData);
                byte[] byteRequest = mStream.ToArray();

                KeyValuePair<int, byte[]> response = SendRequestToServer(byteRequest);

                if (response.Key == 0)
                    return null;

                mStream = new MemoryStream();
                mStream.Write(response.Value, 0, response.Key);
                mStream.Position = 0;
                table = (DataTable)binFormatter.Deserialize(mStream);
            }
            catch (Exception e)
            {
                MessageBox.Show("Вибачте за незручності. Проблема спілкування з сервером. " + e.Message, "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }

            return table;
        }
        public DataTable GetSampleFromTable(string tableName, string keyPhrase, string searchColumnName, bool isSeparate, string sortColumnName = "")
        {
            DataTable table = null;
            try
            {
                string isSeparateString = isSeparate ? "1" : "0";
                KeyValuePair<string, IList<string>> requestData =
                    new KeyValuePair<string, IList<string>>("sampleFromTable",
                        new List<string> { tableName, keyPhrase, searchColumnName, isSeparateString, sortColumnName });
                var binFormatter = new BinaryFormatter();
                var mStream = new MemoryStream();
                binFormatter.Serialize(mStream, requestData);
                byte[] byteRequest = mStream.ToArray();

                KeyValuePair<int, byte[]> response = SendRequestToServer(byteRequest);

                if (response.Key == 0)
                    return null;

                mStream = new MemoryStream();
                mStream.Write(response.Value, 0, response.Key);
                mStream.Position = 0;
                table = (DataTable)binFormatter.Deserialize(mStream);
            }
            catch (Exception e)
            {
                MessageBox.Show("Вибачте за незручності. Проблема спілкування з сервером. " + e.Message, "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }

            return table;
        }

        private void DeleteManyWithOneColumn(string tableName, string columnName, List<string> values)
        {
            IList<string> parameters = new List<string> { tableName, columnName };
            foreach (var value in values)
                parameters.Add(value);
            KeyValuePair<string, IList<string>> requestData =
                new KeyValuePair<string, IList<string>>("delete", parameters);
            var binFormatter = new BinaryFormatter();
            var mStream = new MemoryStream();
            binFormatter.Serialize(mStream, requestData);
            byte[] byteRequest = mStream.ToArray();

            SendRequestToServer(byteRequest);
        }

        private void UpdateOneValueWithAnyColumn(string tableName, string updateColumnName, string updateValue, string whereColumnName, string whereValue)
        {
            IList<string> parameters = new List<string> { tableName, updateColumnName, updateValue, whereColumnName, whereValue };
            KeyValuePair<string, IList<string>> requestData =
                new KeyValuePair<string, IList<string>>("updateOneValue", parameters);
            var binFormatter = new BinaryFormatter();
            var mStream = new MemoryStream();
            binFormatter.Serialize(mStream, requestData);
            byte[] byteRequest = mStream.ToArray();

            SendRequestToServer(byteRequest);
        }

        public void InsertOneWithId(string tableName, string values)
        {
            IList<string> parameters = new List<string> { tableName, values };
            KeyValuePair<string, IList<string>> requestData =
                new KeyValuePair<string, IList<string>>("insertWithId", parameters);
            var binFormatter = new BinaryFormatter();
            var mStream = new MemoryStream();
            binFormatter.Serialize(mStream, requestData);
            byte[] byteRequest = mStream.ToArray();

            SendRequestToServer(byteRequest);
        }

        public void InsertOneWithoutId(string tableName, string values)
        {
            IList<string> parameters = new List<string> { tableName, values };
            KeyValuePair<string, IList<string>> requestData =
                new KeyValuePair<string, IList<string>>("insertWithoutId", parameters);
            var binFormatter = new BinaryFormatter();
            var mStream = new MemoryStream();
            binFormatter.Serialize(mStream, requestData);
            byte[] byteRequest = mStream.ToArray();

            SendRequestToServer(byteRequest);
        }
        #endregion

        public void DisplayTableInfo(string tableName)
        {
            ClearTextFieldsAndComboBoxes();
            FillTable(tableName);
            SetDefaultProperties(tableName);
            FillColumnNames();
        }
        private void FillTable(string tableName, bool isSample = false)
        {
            try
            {
                dataGridView.DataSource = !isSample
                    ? GetTable(tableName, comboBoxSort.Text)
                    : GetSampleFromTable(tableName, textBoxSearch.Text, comboBoxSearch.Text,
                        checkBoxSeparate.Checked, comboBoxSort.Text);
            }
            catch (Exception e)
            {
                // Обработка возникшей ошибки
                string errorMessage = "На жаль, при виконанні сталася помилка." + e.Message;
                MessageBox.Show(errorMessage, "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        private void FillColumnNames()
        {
            foreach (DataColumn column in ((DataTable)dataGridView.DataSource).Columns)
            {
                comboBoxSort.Items.Add(column.ColumnName);
                comboBoxSearch.Items.Add(column.ColumnName);
            }
        }
        private void SetDefaultProperties(string tableName)
        {
            CurrentOperation = Operation.None;
            pictureBoxOk.Visible = false;

            #region dataGridViewProperties
            dataGridView.ReadOnly = true;
            dataGridView.AllowUserToAddRows = false;
            // Убираем возможность сортировать по колонками
            foreach (DataGridViewTextBoxColumn column in dataGridView.Columns)
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            dataGridView.SelectionMode = DataGridViewSelectionMode.CellSelect;

            #endregion

            // Отображаем или прячем кнопки перемещания таблицей в зависимости от размера таблицы
            bool tableIsEmpty = dataGridView.Rows.Count == 0;
            bool tableFitsFormHeigh = dataGridView.PreferredSize.Height > dataGridView.Height;
            pictureBoxUp.Visible = pictureBoxDown.Visible = tableFitsFormHeigh && !tableIsEmpty;
        }
        private void ClearTextFieldsAndComboBoxes()
        {
            comboBoxSearch.Items.Clear();
            comboBoxSearch.Items.Add("таблиці");
            comboBoxSort.Items.Clear();

            labelHelp.Text = string.Empty;
            textBoxSearch.Text = string.Empty;
            comboBoxSearch.Text = string.Empty;
        }

        private void comboBoxSort_SelectedIndexChanged(object sender, EventArgs e)
        {
            pictureBoxSearch_Click(sender, e);
        }
        private void comboBoxSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            pictureBoxSearch_Click(sender, e);
        }
        private void checkBoxSeparate_CheckedChanged(object sender, EventArgs e)
        {
            pictureBoxSearch_Click(sender, e);
        }
        private void pictureBoxSearch_Click(object sender, EventArgs e)
        {
            if (dataGridView.DataSource == null)
                return;

            bool isSample = textBoxSearch.Text != string.Empty;
            FillTable(((DataTable)dataGridView.DataSource).TableName, isSample);
        }

        private void pictureBoxUp_Click(object sender, EventArgs e)
        {
            if (dataGridView.Rows.Count == 0)
                return;

            // Выделение и фокус на первой строке таблицы
            dataGridView.ClearSelection();
            dataGridView.Rows[0].Selected = true;
            dataGridView.FirstDisplayedScrollingRowIndex = 0;
        }
        private void pictureBoxDown_Click(object sender, EventArgs e)
        {
            if (dataGridView.Rows.Count == 0)
                return;

            // Выделение и фокус на последней строке таблицы
            dataGridView.ClearSelection();
            dataGridView.Rows[dataGridView.RowCount - 1].Selected = true;
            dataGridView.FirstDisplayedScrollingRowIndex = dataGridView.RowCount - 1;
        }

        private void tablesToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (DataBaseIsEmpty)
            {
                MessageBox.Show("Відображення таблиць неможливе, оскільки база даних порожня.", "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string tableName = e.ClickedItem.Text;
            DisplayTableInfo(tableName);
        }

        private void insertToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView.DataSource == null)
                return;

            CurrentOperation = Operation.Insert;
            #region setFormElementsProperties
            pictureBoxOk.Visible = false;
            labelHelp.Text = string.Empty;
            dataGridView.ReadOnly = true;
            dataGridView.SelectionMode = DataGridViewSelectionMode.CellSelect;
            #endregion
            Add();
        }
        private void updateTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView.Rows.Count == 0)
                return;

            CurrentOperation = Operation.Update;
            #region setFormElementsProperties
            pictureBoxOk.Visible = true;
            labelHelp.Text = "Инструкция: Измените содержание полей, затем нажмите галочку.";
            dataGridView.ReadOnly = false;
            dataGridView.SelectionMode = DataGridViewSelectionMode.CellSelect;
            #endregion
        }
        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView.Rows.Count == 0)
                return;

            CurrentOperation = Operation.Delete;
            #region setFormElementsProperties
            pictureBoxOk.Visible = true;
            labelHelp.Text = "Инструкция: Выделите необходимые ряды, затем нажмите галочку.";
            dataGridView.ReadOnly = true;
            dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            #endregion
        }

        public void Add()
        {
            InsertForm add = new InsertForm((DataTable)dataGridView.DataSource, CurrentOperation);
            add.Show(this);
        }

        private void pictureBoxOk_Click(object sender, EventArgs e)
        {
            // В случае подтверждения действия, применяем его и обновляем таблицу
            DialogResult confirm = MessageBox.Show("Ви впевнені, що бажаєте застосувати зміни?", "Шановний користувач!", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                try
                {
                    CommitAction();
                }
                catch (Exception exception)
                {
                    // Обработка возникшей ошибки
                    string errorMessage = "На жаль, при виконанні сталася помилка." + Environment.NewLine + "Повідомлення: " + exception.Message;
                    MessageBox.Show(errorMessage, "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                DisplayTableInfo(((DataTable)dataGridView.DataSource).TableName);
            }
        }
        private void CommitAction()
        {
            if (CurrentOperation == Operation.Delete && dataGridView.SelectedRows.Count > 0)
                ProceedDelete();
            else if (CurrentOperation == Operation.Update)
            {
                // Проверка добавлено ли последние поле в словарь для обновления
                CheckLastCellSelection();
                ProceedUpdate();
                CellsUpdated.Clear();
            }
        }
        private void ProceedDelete()
        {
            DataTable table = (DataTable)dataGridView.DataSource;
            List<string> values = new List<string>();
            if (((DataTable)dataGridView.DataSource).Columns.Contains("id"))
            {
                for (int i = 0; i < dataGridView.SelectedRows.Count; i++)
                    values.Add(Convert.ToString(dataGridView.SelectedRows[i].Cells[table.Columns.IndexOf("id")].Value));
                DeleteManyWithOneColumn(table.TableName, "id", values);
            }
            else if (table.TableName.Equals("sphere") || table.TableName.Equals("country"))
            {
                for (int i = 0; i < dataGridView.SelectedRows.Count; i++)
                    values.Add(Convert.ToString(dataGridView.SelectedRows[i].Cells[table.Columns.IndexOf("name")].Value));
                DeleteManyWithOneColumn(table.TableName, "name", values);
            }
        }
        private void ProceedUpdate()
        {
            foreach (KeyValuePair<int, int> cellCoords in CellsUpdated.Keys)
            {
                DataTable table = (DataTable)dataGridView.DataSource;
                // Проверка на корректность содержимого полей
                FieldsValidator validator = new FieldsValidator();
                if (!validator.CheckField(table.Columns[cellCoords.Value], CellsUpdated[cellCoords]))
                    continue;

                string columnName = table.Columns[cellCoords.Value].ColumnName;
                string newValue = CellsUpdated[cellCoords];
                // Изменить поля с датой в соответствие формату в БД
                if (table.Columns[columnName].DataType.Name.Equals("DateTime"))
                    newValue = validator.ConvertToPostgreSQLDate(newValue);
                if (table.Columns.Contains("id"))
                    UpdateOneValueWithAnyColumn(table.TableName, columnName, newValue,
                                           "id", dataGridView.Rows[cellCoords.Key].Cells[table.Columns.IndexOf("id")].Value.ToString());
                if (table.TableName.Equals("sphere") || table.TableName.Equals("country"))
                    UpdateOneValueWithAnyColumn(table.TableName, columnName, newValue,
                                           "name", dataGridView.Rows[cellCoords.Key].Cells[table.Columns.IndexOf("name")].Value.ToString());
            }
        }
        private void CheckLastCellSelection()
        {
            // Если осталась выделенная ячейка таблицы, обработать и её
            if (dataGridView.SelectedCells.Count != 0)
            {
                var updateCell = dataGridView.SelectedCells[dataGridView.SelectedCells.Count - 1];
                dataGridView_CellValueChanged(pictureBoxOk, new DataGridViewCellEventArgs(updateCell.ColumnIndex, updateCell.RowIndex));
            }
        }

        private void dataGridView_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            DataTable table = (DataTable)dataGridView.DataSource;
            FieldsValidator validator = new FieldsValidator();

            if (CurrentOperation == Operation.Update && !validator.FieldMathcNullRequirement(table.Columns[e.ColumnIndex], e.Value.ToString()))
            {
                e.Value = "Ошибка!";
                e.ParsingApplied = true;
            }
        }
        private void dataGridView_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            DataTable table = (DataTable)dataGridView.DataSource;
            string newCellValue = e.FormattedValue.ToString();

            // Оповещение про несоответствие содержимого поля шаблону
            FieldsValidator validator = new FieldsValidator();
            if (!validator.FieldMatchPattern(table.Columns[e.ColumnIndex], newCellValue))
                dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "Поле не відповідає шаблону";

            // Оповещение про незаполненное поле
            else if (!validator.FieldMathcNullRequirement(table.Columns[e.ColumnIndex], newCellValue))
                dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "Дане поля не можна залишати порожнім";

            // Очищаем сообщение об ошибке
            else
                dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = null;

        }
        private void dataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (CurrentOperation != Operation.Update)
                return;

            // Добавляем координаты и содержимое поля таблицы в словарь
            KeyValuePair<int, int> coords = new KeyValuePair<int, int>(e.RowIndex, e.ColumnIndex);
            string value = (string)dataGridView.Rows[coords.Key].Cells[coords.Value].EditedFormattedValue;

            // Добавляем их в словарь Или перезаписываем содержимое
            if (!CellsUpdated.Keys.Contains(coords))
                CellsUpdated.Add(coords, value);
            else
                CellsUpdated[coords] = value;
        }
        private void dataGridView_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            e.Control.KeyPress += Cell_KeyPress;
        }
        private void Cell_KeyPress(object Sender, KeyPressEventArgs e)
        {
            // Ограничение ввода некорректных символов в поле
            DataTable table = (DataTable)dataGridView.DataSource;
            DataColumn column = table.Columns[dataGridView.CurrentCell.ColumnIndex];

            if (column.DataType.Name.Equals("Int32") && !char.IsDigit(e.KeyChar) && e.KeyChar != 8)
                e.KeyChar = Convert.ToChar("\0");

            if (column.DataType.Name.Equals("DateTime") &&
                char.IsLetter(e.KeyChar) && e.KeyChar != 8)
                e.KeyChar = Convert.ToChar("\0");
        }

        private void accountChangeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Owner.Show();
            Dispose();
        }

        private void reportGenerateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView.Rows.Count == 0)
                return;

            Excel.Application XlObj = new Excel.Application();
            XlObj.Visible = false;
            Excel._Workbook WbObj = XlObj.Workbooks.Add(string.Empty);
            Excel._Worksheet WsObj = (Excel.Worksheet)WbObj.ActiveSheet;

            // Присвоить ячейкам значения с таблицы
            try
            {
                DataTable Table = (DataTable)dataGridView.DataSource;

                int row = 1, col = 1;
                // Добавление колонок
                foreach (DataColumn column in Table.Columns)
                    WsObj.Cells[row, col++] = column.ColumnName;

                col = 1;
                row++;
                // Добавление данных
                for (int i = 0; i < Table.Rows.Count; i++, row++, col = 1)
                    Array.ForEach(Table.Rows[i].ItemArray, (cell) => WsObj.Cells[row, col++] = cell);

                WbObj.SaveAs(Path.Combine(Environment.CurrentDirectory, "data.xlsx"));
            }
            catch { }
            finally
            {
                WbObj.Close();
                XlObj.Quit();
                MessageBox.Show("Обрана дія була застосована до файла 'data.xlsx' поруч з виконуваним файлом.", "Шановний користувач!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void FormView_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }
    }
}
