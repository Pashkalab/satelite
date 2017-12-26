using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Client
{
    class FieldsValidator
    {
        public string ConvertToPostgreSQLDate(string date)
        {
            if (string.IsNullOrEmpty(date))
                return date;

            StringBuilder sqlDate = new StringBuilder();
            sqlDate.Append(date.Substring(date.LastIndexOf('.') + 1)).Append('-');
            sqlDate.Append(date.Substring(date.IndexOf('.') + 1, date.LastIndexOf('.') - 3)).Append('-');
            sqlDate.Append(date.Substring(0, date.IndexOf('.')));
            return sqlDate.ToString();
        }


        public bool CheckField(DataColumn column, string inputValue)
        {
            inputValue = inputValue.Trim();
            if (FieldMathcNullRequirement(column, inputValue))
                return true;

            if (column.DataType.Name.Equals("Int32") && !inputValue.All(char.IsDigit))
                return false;
            return FieldMatchPattern(column, inputValue);
        }
        /// <summary>
        /// Checks if NULL is valid inputValue for field for no input
        /// </summary>
        /// <param name="column"></param>
        /// <param name="inputValue"></param>
        /// <returns></returns>
        public bool FieldMathcNullRequirement(DataColumn column, string inputValue)
        {
            if (column.AllowDBNull)
                return true;
            return !string.IsNullOrEmpty(inputValue);
        }

        /// <summary>
        /// Checks if NULL is valid inputValue for field 
        /// </summary>
        /// <param name="table"></param>
        /// <param name="inputValue"></param>
        /// <param name="columnIndex"></param>
        /// <returns></returns>
        /// <summary>
        /// Checks if inputValue matches pattern requirments
        /// </summary>
        /// <param name="column"></param>
        /// <param name="inputValue"></param>
        /// <returns></returns>
        public bool FieldMatchPattern(DataColumn column, string inputValue)
        {
            if (column.ColumnName.Equals("publish_year"))
                return Convert.ToInt16(inputValue) > 1450;

            Regex datePattern = new Regex(@"^((0[1-9])|([1,2][0-9])|(3[0-1]))\.((0[1-9])|(1[0-2]))\.\d{4}$");
            if (column.ColumnName.Equals("birth_date") || column.ColumnName.Equals("death_date"))
                return datePattern.IsMatch(inputValue);

            Regex emailPattern = new Regex(@"^\S*@\S*\.\w*$");
            if (column.ColumnName.Equals("email"))
                return emailPattern.IsMatch(inputValue);

            Regex urlPattern = new Regex(@"^(http:\/\/|https:\/\/|www.)?((((\S+\/)+\S)(\S*))|(\S*\/.\S*))$");
            if (column.ColumnName.Equals("website"))
                return urlPattern.IsMatch(inputValue);

            Regex phoneNumberPattern = new Regex(@"^(\+38)?0(\d{2}) \d{3}([- ])(\d{2}\3\d{2})$");
            if (column.ColumnName.Equals("phone_number"))
                return phoneNumberPattern.IsMatch(inputValue);

            return true;
        }
    }
}
