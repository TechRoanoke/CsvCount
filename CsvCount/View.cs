using DataAccess;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvCount
{
    class View
    {
        // Only include these columns. 
        // Order here is preserved, so this can also be used to rearrange the columns. 
        // If null, ignore. 
        public string[] Select;

        // Only include N rows. 
        public int? Take; 

        // if Null, then don't filter. 
        public IRowFilter[] Filters;

        public int[] GetSelectedIndices(DataTable dt)
        {
            if (this.Select == null)
            {
                // Return all
                return Enumerable.Range(0, dt.ColumnNames.Count()).ToArray();
            }
            int[] id = Array.ConvertAll(this.Select, x => dt.GetColumnIndex(x));
            return id;
        }

        public bool IsRowIncluded(Row row)
        {
            if (this.Filters == null)
            {
                return true;
            }

            // Ensure all columns are present
            foreach (var filter in this.Filters)
            {
                bool include = filter.IsValid(row);

                if (!include)
                {
                    return false;
                }
            }
            return true;            
        }
    }
}