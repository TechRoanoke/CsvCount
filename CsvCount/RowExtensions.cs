using DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvCount
{
    public static class RowExtensions
    {
        public static int[] GetIndices(this Row row, string[] requiredColumns)
        {
            var columnNames = row.ColumnNames.ToArray();

            Dictionary<string, int> columnMap = 
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columnNames.Length; i++)
            {
                columnMap[columnNames[i]] = i;
            }

            var indices = Array.ConvertAll(requiredColumns, x => columnMap[x]);
            return indices;
        }

        public static int GetIndex(this Row row, string columnName)
        {
            var columnNames = row.ColumnNames.ToArray();
            for (int i = 0; i < columnNames.Length; i++)
            {
                if (string.Equals(columnNames[i], columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            throw new InvalidOperationException("Can't find column: " + columnName);
        }
    }
}
