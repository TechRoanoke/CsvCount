using DataAccess;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvCount
{
    // Helper to count diffs between 2 CSVs of the same schema. 
    // This is a symettric diff. 
    // A diff includes:
    // - difference in primary keys (key that appear in one but not the other). 
    // - if a primary key is in both files, count if any of the extra columns in columnName are different. 
    public class CsvDiff
    {
        public static void Diff(string file1, string file2, string primaryKeyColumnName, string[] columnNames)
        {
            int diffKeys = 0;
            int diffExtras = 0;

            Dictionary<string, string> vals = new Dictionary<string, string>();

            DataTable dt1 = DataTable.New.ReadLazy(file1);
            // Get first. 

            int originalSize1 =0;
            foreach(var tuple in GetKeys(dt1, primaryKeyColumnName, columnNames))
            {
                originalSize1++;
                string key = tuple.Item1;
                string extras = tuple.Item2;
                vals[key] = extras;
            }

            // Compare to second
            DataTable dt2 = DataTable.New.ReadLazy(file2);
            int originalSize2 =0;
            foreach(var tuple in GetKeys(dt2, primaryKeyColumnName, columnNames))
            {
                originalSize2++;
                string extra;
                if (vals.TryGetValue(tuple.Item1, out extra))
                {
                    if (extra != tuple.Item2)
                    {
                        // Key was in there, but extra info is different. 
                        diffExtras++;
                    }
                    else
                    {
                        // good!
                    }
                    vals.Remove(tuple.Item1);
                }
                else
                {
                    // Key was not present
                    diffKeys++;
                }
            }
 
            // Remaining keys
            diffKeys += vals.Count;


            int total = diffKeys + diffExtras;

            Console.WriteLine("              Different keys: {0}", diffKeys);
            Console.WriteLine(" same keys, different values: {0}", diffExtras);
            Console.WriteLine("                total errors: {0}", total);
            Console.WriteLine("            Original records: {0},{1}", originalSize1, originalSize2);
            
            int avgSize = (originalSize1 + originalSize2) / 2;
            Console.WriteLine("                  Error rate: {0:0.00}%", (total * 100.0 / avgSize));                        
        }

        static IEnumerable<Tuple<string,string>> GetKeys(DataTable dt, string primaryKeyColumnName, string[] columnNames)
        {
            int colPrimary = dt.GetColumnIndex(primaryKeyColumnName);

            int[] colExtra = Array.ConvertAll(columnNames, x => dt.GetColumnIndex(x));

            foreach (var row in dt.Rows)
            {

                string primaryKey = row.Values[colPrimary].ToLower();

                string[] extraVals = Array.ConvertAll(colExtra, x => row.Values[x]);
                string extra = string.Join(";", extraVals).ToLower();

                yield return Tuple.Create(primaryKey, extra);
            }
        }
    }
}