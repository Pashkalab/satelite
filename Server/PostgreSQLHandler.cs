using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace Server
{
    class PostgreSqlHandler
    {
        #region properties
        private string DataBase;
        private string ConnectionString;
        #endregion

        public PostgreSqlHandler(string login, string password, string database)
        {
            DataBase = database;
            ConnectionString = string.Format("Server=localhost;Port=5300;Username={0};Password={1};Database={2};", login, password, DataBase);
        }

        public DataTable GetTable(string tableName, string orderColumn = "")
        {
            return GetResultTable(tableName, null, null, false, orderColumn);
        }
        public DataTable GetSampleFromTable(string tableName, string keyPhrase, string place = "",
            bool separate = false, string orderColumn = "")
        {
            return GetResultTable(tableName, keyPhrase, place, separate, orderColumn);
        }
        private DataTable GetResultTable(string tableName, string keyPhrase = null, string place = null,
            bool separate = false, string orderColumn = "")
        {
            DataTable table = new DataTable(tableName);
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                connection.Open();
                string query = GetSelectionQuery(tableName, keyPhrase, place, orderColumn, separate);
                using (var command = new NpgsqlCommand(query, connection))
                {
                    NpgsqlDataAdapter adapter = new NpgsqlDataAdapter();
                    adapter.SelectCommand = command;
                    adapter.FillSchema(table, SchemaType.Source);
                    adapter.Fill(table);
                }
                connection.Close();
            }
            return table;
        }

        private string GetSelectionQuery(string tableName, string keyPhrase, string place, string orderColumn,
            bool separate)
        {
            StringBuilder query = new StringBuilder(string.Format("SELECT * FROM {0};", tableName));

            if (!string.IsNullOrEmpty(keyPhrase))
            {
                StringBuilder condition = new StringBuilder(" WHERE ");
                IList<string> columnNames = GetColumnNames(tableName);
                string columnType;
                if (!columnNames.Contains(place))
                {
                    for (int i = 0; i < columnNames.Count; i++)
                    {
                        place = columnNames[i];
                        columnType = GetColumnType(columnNames[i]);
                        condition.Append(GetCondition(keyPhrase, place, separate, columnType));
                        if (i != columnNames.Count - 1 &&
                            !condition.ToString().EndsWith(" OR ") && !condition.ToString().EndsWith(" WHERE "))
                            condition.Append(" OR ");
                    }
                    string conditionString = condition.ToString();
                    if (conditionString.EndsWith(" OR "))
                        condition = new StringBuilder(conditionString.Remove(conditionString.LastIndexOf("OR ")));
                }
                else
                {
                    columnType = GetColumnType(place);
                    condition.Append(GetCondition(keyPhrase, place, separate, columnType));
                }
                query = query.Insert(query.ToString().LastIndexOf(';'), condition);
            }

            if (!string.IsNullOrEmpty(orderColumn))
                query = query.Insert(query.ToString().LastIndexOf(';'), string.Format(" ORDER BY {0}", orderColumn));

            return query.ToString();
        }

        private string GetCondition(string keyPhrase, string place, bool separate, string type)
        {
            StringBuilder condition = new StringBuilder();
            if (type == "USER-DEFINED" || (type == "integer" && !keyPhrase.All(char.IsDigit)))
                return condition.ToString();

            if (type == "integer")
                condition.Append(place).Append(" = ").Append(keyPhrase);
            else if (type == "date")
            {
                Regex datePattern = new Regex(@"^((0[1-9])|([1,2][0-9])|(3[0-1]))\.((0[1-9])|(1[0-2]))\.\d{4}$");
                if (datePattern.IsMatch(keyPhrase))
                    condition.Append(place).Append(string.Format(" = '{0}'", keyPhrase));
            }
            else
                condition.Append(place).Append(separate
                                ? string.Format(" = '{0}'", keyPhrase)
                                : string.Format(" ~ '{0}'", keyPhrase));

            return condition.ToString();
        }

        public IList<string> GetTableNames()
        {
            IList<string> tableNames = GetListOfNames("SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_type = 'BASE TABLE';");
            IList<string> liquibaseTableNames = new List<string> { "databasechangelog", "databasechangeloglock" };
            foreach (var liquibaseTableName in liquibaseTableNames)
                tableNames.Remove(liquibaseTableName);

            return tableNames;
        }
        public IList<string> GetColumnNames(string tableName)
        {
            IList<string> tableNames = GetListOfNames(string.Format("SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name = '{0}';", tableName));
            return tableNames;
        }
        private IList<string> GetListOfNames(string query)
        {
            IList<string> names = new List<string>();
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    NpgsqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                        names.Add(Convert.ToString(reader[0]));
                }
                connection.Close();
            }
            return names;
        }

        public void InsertOneWithId(string tableName, string values)
        {
            if (values.Contains(";"))
                throw new ArgumentException("Символ ';' - заборенений!");

            string query = string.Format("INSERT INTO {0} VALUES (DEFAULT, {1});", tableName, values);
            ExecuteQuery(query);
        }
        public void InsertOneWithoutId(string tableName, string values)
        {
            if (values.Contains(";"))
                throw new ArgumentException("Символ ';' - заборенений!");

            string query = string.Format("INSERT INTO {0} VALUES ({1});", tableName, values);
            ExecuteQuery(query);
        }

        public void DeleteManyWithOneColumn(string tableName, string columnName, IList<string> values)
        {
            StringBuilder condition = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                condition.Append(GetCondition(values[i], columnName, true, GetColumnType(columnName)));
                if (i != values.Count - 1)
                    condition.Append(" OR ");
            }
            string query = string.Format("DELETE FROM {0} WHERE {1};", tableName, condition);
            ExecuteQuery(query);
        }

        public void UpdateOneValueWithAnyColumn(string tableName, string updateColumnName, string updateValue, string whereColumnName, string whereValue)
        {
            StringBuilder condition = new StringBuilder();
            condition.Append(GetCondition(whereValue, whereColumnName, true, GetColumnType(updateColumnName)));

            StringBuilder setParameters = new StringBuilder();
            setParameters.Append(GetCondition(updateValue, updateColumnName, true, GetColumnType(updateColumnName)));

            if (condition.ToString().Contains(";") || setParameters.ToString().Contains(";"))
                throw new ArgumentException("Символ ';' - заборенений!");

            string query = string.Format("UPDATE {0} SET {1} WHERE {2};", tableName, setParameters, condition);
            ExecuteQuery(query);
        }
        public void UpdateOneWithId(string tableName, IList<string> columns, IList<string> values, int id)
        {
            StringBuilder query = new StringBuilder();
            query.Append("UPDATE ").Append(tableName).Append(" SET ");
            for (int i = 0; i < columns.Count; i++)
            {
                if (values[i].Contains(";"))
                    throw new ArgumentException("Символ ';' - заборенений!");

                query.Append(columns[i]).Append("=").Append(values[i]);
                if (i < columns.Count - 1)
                    query.Append(", ");
            }
            query.Append(" WHERE id = ").Append(id).Append(";");
            ExecuteQuery(query.ToString());
        }

        private void ExecuteQuery(string query)
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteReader();
                }
                connection.Close();
            }
        }

        public string GetPassword(string login)
        {
            string password;
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                string query = string.Format("SELECT password FROM \"UsersInfo\" where login='{0}';", login);
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    password = command.ExecuteScalar().ToString();
                }
                connection.Close();
            }
            return password;
        }
        public int GetUserId(string tableName, string columnName, string value)
        {
            int id;
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                string query = string.Format("SELECT id FROM {0} where {1}='{2}';", tableName, columnName, value);
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    id = Convert.ToInt32(command.ExecuteScalar().ToString());
                }
                connection.Close();
            }
            return id;
        }
        private string GetColumnType(string columnName)
        {
            string columnType;
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                string query = string.Format("SELECT data_type from information_schema.columns where column_name='{0}';", columnName);
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    columnType = command.ExecuteScalar().ToString();
                }
                connection.Close();
            }
            return columnType;
        }

        public DataTable GetOrdersHistoryByReader(int id)
        {
            DataTable table = new DataTable("history");
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                connection.Open();
                StringBuilder query = new StringBuilder();
                query.Append("SELECT book.title, book.kind, author.full_name AS author, publisher.name AS publisher, book_reader.taken, book_reader.returned ")
                    .Append("FROM book_reader, book, publisher, author, book_author ")
                    .Append("WHERE book_reader.reader_id = ").Append(id).Append(" AND book.id = book_reader.book_id AND publisher.id = book.publisher_id ")
                    .Append("AND book_author.book_id = book_reader.book_id AND author.id = book_author.author_id;");
                using (var command = new NpgsqlCommand(query.ToString(), connection))
                {
                    NpgsqlDataAdapter adapter = new NpgsqlDataAdapter();
                    adapter.SelectCommand = command;
                    adapter.FillSchema(table, SchemaType.Source);
                    adapter.Fill(table);
                }
                connection.Close();
            }
            return table.Rows.Count == 0 ? null : table;
        }

        public DataTable GetBooksByPublisher(int id)
        {
            DataTable table = new DataTable("books");
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                connection.Open();
                StringBuilder query = new StringBuilder();
                query.Append("SELECT book.title, book.kind, book.max_count, book.available_count, book.publish_year, author.full_name AS author ")
                    .Append("FROM book, author, book_author ")
                    .Append("WHERE book.publisher_id = ").Append(id).Append(" AND book_author.book_id = book.id AND author.id = book_author.author_id;");
                using (var command = new NpgsqlCommand(query.ToString(), connection))
                {
                    NpgsqlDataAdapter adapter = new NpgsqlDataAdapter();
                    adapter.SelectCommand = command;
                    adapter.FillSchema(table, SchemaType.Source);
                    adapter.Fill(table);
                }
                connection.Close();
            }
            return table.Rows.Count == 0 ? null : table;
        }
    }
}
