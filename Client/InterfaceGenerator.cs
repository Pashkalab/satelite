using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Client
{
    public class InterfaceGenerator
    {
        private DataTable Table;

        public InterfaceGenerator( DataTable table)
        {
            Table = table;
        }

        public Control[] CreateInterface(int yTop, int yDelta, 
            int xLeftTextBox, Size textBoxSize, bool withValues=false)
        {
            IList<string> columnNames = GetColumnNames();
            IList<string> columnValues = GetColumnValues(columnNames);
            int columnsCount = columnNames.Count;
            TextBox[] textBoxes = new TextBox[columnsCount];
            Label[] labels = new Label[columnsCount];
            Control[] controls = new Control[columnsCount * 2];

            // Значения первого текстового поля и расстояние между полями
            int y = yTop;
            for (int i = 0; i < columnsCount; i++)
            {
                // Добавляем label с названием поля для ввода
                labels[i] = new Label
                {
                    Location = new Point(20, y),
                    Text = columnNames[i],
                    AutoSize = true
                };
                controls[i * 2] = labels[i];

                // Добавляем само поле для ввода
                textBoxes[i] = new TextBox
                {
                    Location = new System.Drawing.Point(xLeftTextBox, y),
                    Size = textBoxSize
                };
                if (withValues)
                    textBoxes[i].Text = columnValues[i];
                controls[i * 2 + 1] = textBoxes[i];

                y += yDelta;
            }
            return controls;
        }
        public IList<string> GetColumnNames()
        {
            IList<string> columnNames = new List<string>();
            foreach (DataColumn column in Table.Columns)
                columnNames.Add(column.ColumnName);

            if (columnNames.Contains("id"))
                columnNames.Remove("id");

            return columnNames;
        }
        private IList<string> GetColumnValues(IList<string> columnNames)
        {
            IList<string> columnValues = new List<string>();
            foreach (string column in columnNames)
                columnValues.Add(Table.Rows[0][column].ToString());

            return columnValues;
        }
    }
}
