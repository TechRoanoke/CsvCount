using DataAccess;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvCount
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(
@"Usage:
  CsvCount 

    %filename%  :  prints first few rows of filename. 

    %filename% -vertical  :  prints vert

    %filename% -vertical -required A,B,C  -where Column=Value

Used for viewing large CSV files.
Prints the first row of data in the CSV file and counts the total # of rows.
");
                return;
            }

            IList<IRowFilter> filters = new List<IRowFilter>();

            string file = args[0];

            bool vertical = false;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "-vertical")
                {
                    vertical = true;
                    continue;
                }
                if (args[i] == "-required" || args[i] == "-req")
                {
                    var val = args[i + 1];
                    string[] required = val.Split(',');
                    filters.Add(new MustHaveColumnsFilter(required));
                    i++;
                    continue;
                }
                if (args[i] == "-where")
                {
                    var clause = args[i + 1];
                    var parts = clause.Split('=');
                    string columnName = parts[0];
                    string value = parts[1];

                    filters.Add(new WhereFilter(columnName, value));
                    i++;
                    continue;
                }
            }

            if (!vertical)
            {
                PrintFastSummary(file);
            }
            else
            {
                PreviewVertical(file, filters);
            }
        }

        // Provide a "vertical" view where each column is on its own line.
        // This is easier to pick out individual values. 
        static void PreviewVertical(string file, IList<IRowFilter> filters = null)
        {            
            var dt = DataTable.New.ReadLazy(file);
                        
            var columns = dt.ColumnNames.ToArray();
            Console.WriteLine("[{0} columns]", columns.Length);

            foreach (var row in dt.Rows)
            {
                if (filters != null)
                {
                    bool skip = false;
                    // Ensure all columns are present
                    foreach (var filter in filters)
                    {
                        bool include = filter.IsValid(row);

                        if (!include)
                        {
                            skip = true;
                            break;
                        }                        
                    }
                    if (skip)
                    {
                        continue;
                    }
                }

                for(int i = 0; i < columns.Length; i++)
                {
                    string value = row.Values[i];
                    Console.WriteLine("{0}: {1}", columns[i], value);
                }

                // Stop after first. 
                break;
            }
        }


        // Print naive summary 
        // No pasring, so very fast. Good for getting a quick count on large files. 
        static void PrintFastSummary(string file)
        {
            // Use StreamReader since it skips parsing and is blazing fast. 
            int N = 5;
            int totalRows = 0;
            using (var tr = new StreamReader(file))
            {
                string line;
                while (true)
                {
                    line = tr.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    totalRows++;
                    if (totalRows < N)
                    {
                        Console.WriteLine(line);
                    }

                    if (totalRows % 500000 == 0)
                    {
                        Console.Write(".");
                    }
                }
            }
            Console.WriteLine();
            Console.WriteLine("Total rows: {0:n0}", totalRows);
        }
    }
}
