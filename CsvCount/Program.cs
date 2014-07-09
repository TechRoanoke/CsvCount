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
            if (args.Length != 1)
            {
                Console.WriteLine(
@"Usage:
  CsvCount %filename%

Used for viewing large CSV files.
Prints the first row of data in the CSV file and counts the total # of rows.
");
                return;
            }
            string file = args[0];

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
