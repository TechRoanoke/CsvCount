using DataAccess;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvCount
{
    interface IRowFilter
    {
        bool IsValid(Row row);
    }

    // Row must have non-empty values for all the specified columns
    public class MustHaveColumnsFilter : IRowFilter
    {
        int[] _columnIdx;
        string[] _columnNames;

        public MustHaveColumnsFilter(string[] columnNames)
        {
            _columnNames = columnNames;
        }

        public bool IsValid(Row row)
        {
            if (_columnIdx == null)
            {
                _columnIdx = row.GetIndices(_columnNames);
            }

            // Ensure all columns are present
            foreach (var x in _columnIdx)
            {
                string val = row.Values[x];
                if (string.IsNullOrWhiteSpace(val))
                {
                    return false;
                }
            }
            return true;
        }
    }

    public class WhereFilter : IRowFilter
    {
        string _columnName;
        string _value;
        int _idx = -1;

        public WhereFilter(string columnName, string value)
        {
            _columnName = columnName;
            _value = value;
        }
        public bool IsValid(Row row)
        {
            if (_idx == -1)
            {
                _idx = row.GetIndex(_columnName);
            }
            string x = row.Values[_idx];
            bool equal = string.Equals(x, _value);
            return equal;
        }
    }
}