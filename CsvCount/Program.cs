using DataAccess;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvCount
{
    // Viewing CSV files. Has lazy parsing especially for handling large files. 
    class Program
    {
        enum DisplayMode
        {
            // Output to a CSV. Used to apply a filter to produce a smaller CSV. 
            Csv,

            // Show a single column. 
            Vertical,

            // show summary stats on the file 
            Stats,

            // Historgram 
            Hist,
        }

        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-help" || args[0] == "-?")
            {
                Console.WriteLine(
@"Usage:
  CsvCount 

Used for viewing large CSV files.

    -diff %File1% %File2% PrimaryKey C1,C2,C3..
        diff 2 CSV files. This is symetric. 

    %filename%  [options]
        prints first few rows of filename. 
    
    %filename% -rename Src Dest
        rename a single column. Combine with -out. 

    %filename% -addHeader c1,c2,c3
        adds a header row to the file

    %filename% -replaceHeader c1,c2,c3
        revmove the previos header and add the new one. 

    %filename% -deleteHeader
        delete the first row (a header)

    %filename% -vertical  [options]
        prints a single column in vertical 

    %filename% -stats 
        print summary statistics about file 

    %filename% -hist [column]
        print a histogram for the given column 

Options include:
    -required A,B,C     : filter to require values for columns A,B,c
    -where Column=Value : filter on value. Can have multiple filters. 
    -take N             : only include first N rows that match the filter.
    -select A,B,C       : only include columns A,B,C

    -out <outfile>      : write resulting CSV to outfile
");
                return;
            }

            if (args[0] == "-diff")
            {
                string file1 = args[1];
                string file2 = args[2];
                string primaryKeyColumnName = args[3];
                string[] columnNames = args[4].Split(',');

                CsvDiff.Diff(file1, file2, primaryKeyColumnName, columnNames);
                return;
            }

            View view = new View();
            IList<IRowFilter> filters = new List<IRowFilter>();

            string outputFile = null;

            string file = args[0];

            DisplayMode mode = DisplayMode.Csv; // default


            string renameSrc = null;
            string renameDest = null;
            string histColumn = null;

            bool adjustHeader = false;
            string addHeader = null;
            bool fDeleteHeader = false;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "-replaceHeader")
                {
                    adjustHeader = true;
                    fDeleteHeader = true;
                    addHeader = args[i + 1];
                    i++;
                    continue;
                }
                if (args[i] == "-deleteHeader")
                {
                    adjustHeader = true;
                    fDeleteHeader = true;
                    continue;
                }
                if (args[i] == "-addHeader")
                {
                    adjustHeader = true;
                    addHeader = args[i + 1];
                    i++;
                    continue;
                }
                if (args[i] == "-rename")
                {
                    renameSrc = args[i+1];
                    renameDest = args[i + 2];
                    i+=2 ;
                    continue;
                }
                if (args[i] == "-out")
                {
                    outputFile = args[i + 1];
                    i++;
                    continue;
                }
                if (args[i] == "-stats")
                {
                    mode = DisplayMode.Stats;
                    continue;
                }
                if (args[i] == "-vertical")
                {
                    mode = DisplayMode.Vertical;
                    continue;
                }
                if (args[i] == "-hist")
                {
                    mode = DisplayMode.Hist;
                    histColumn = args[i+1];
                    i++;
                    continue;
                }
                if (args[i] == "-take")
                {
                    int num = int.Parse(args[i + 1]);
                    view.Take = num;
                    i++;
                    continue;
                }
                if (args[i] == "-select")
                {
                    var val = args[i + 1];
                    view.Select = val.Split(',');
                    i++;
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
                throw new InvalidOperationException("Unrecognized argument: " + args[i]);
            }
                   
            if (filters.Count > 0)
            {
                view.Filters = filters.ToArray();
            }

            TextWriter tw = null;
            if (outputFile != null)
            {
                tw = new StreamWriter(outputFile);
            }

            TextWriter twOutput = (tw ?? Console.Out);

            if (adjustHeader)
            {
                // Add a header row
                // Do a rename 
                AdjustHeader(file, addHeader, fDeleteHeader, twOutput);
            }
            else if (renameSrc != null)
            {
                // Do a rename 
                Rename(file, renameSrc, renameDest, twOutput);
            }
            else
            {
                switch (mode)
                {
                    case DisplayMode.Csv:
                        ApplyFilter(file, view, twOutput);
                        break;
                    case DisplayMode.Vertical:
                        if (view.Take.HasValue)
                        {
                            throw new InvalidOperationException("-vertical only shows 1 row. Can't use with -take switch.");
                        }
                        PreviewVertical(file, view);
                        break;

                    case DisplayMode.Stats:
                        if (view.Take.HasValue || view.Select != null || view.Filters != null)
                        {
                            throw new InvalidOperationException("unsupported command line switches for -stats.");
                        }
                        GetStats(file, twOutput);
                        break;

                    case DisplayMode.Hist:
                        if (view.Take.HasValue || view.Select != null || view.Filters != null)
                        {
                            throw new InvalidOperationException("unsupported command line switches for -stats.");
                        }
                        ShowHist(file, histColumn);
                        break;

                }
            }
          

            if (tw != null)
            {
                tw.Close();
                tw.Dispose();
            }
        }

        // Handles adding a header row, removing a row; and changing the header 
        private static void AdjustHeader(
            string file, 
            string newHeaderRow, 
            bool removeHeader,
            TextWriter twOutput)
        {
            int totalRows = 0;
            using (var tr = new StreamReader(file))
            {
                //twOutput.WriteLine(addHeader);

                while (true)
                {
                    var line = tr.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    if (newHeaderRow != null)
                    {
                        // adjust delimiter to match rest of file.
                        if (line.Contains("\t"))
                        {
                            newHeaderRow = newHeaderRow.Replace(',', '\t');
                        }
                        twOutput.WriteLine(newHeaderRow);
                        newHeaderRow = null;
                    }

                    totalRows++;

                    if (!removeHeader)
                    {
                        twOutput.WriteLine(line);
                    }
                    removeHeader = false;
                    if (totalRows % 500000 == 0)
                    {
                        Console.Write(".");
                    }
                }
            }
        }

        // Display a histogram 
        private static void ShowHist(string file, string histColumn)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var dt = DataTable.New.ReadLazy(file);
            int idx = dt.GetColumnIndex(histColumn);
            int total = 0;
            foreach (var row in dt.Rows)
            {
                total++;
                var value = row.Values[idx];
                int c;
                counts.TryGetValue(value, out c);
                c++;
                counts[value] = c;
            }            

            // Show final 
            Console.WriteLine("Histogram on column '{0}'", histColumn);
            foreach (var kv in counts)
            {
                Console.WriteLine("{0}, {1}, {2}%", kv.Key, kv.Value, (kv.Value * 100 / total));
            }
        }

        private static void Rename(string file, string renameSrc, string renameDest, TextWriter output)
        {
            Console.WriteLine("Renaming {0}-->{1}", renameSrc, renameDest);

            int totalRows = 0;
            using (var tr = new StreamReader(file))
            {
                string header = tr.ReadLine();
                // Apply renames 
                var headerNames = header.Split(',');
                var newHeaderNames = Array.ConvertAll(headerNames, x =>
                    {
                        // strip quotes 
                        if (x[0] == '\"')
                        {
                            x = x.Substring(1, x.Length - 2);
                        }
                        if (string.Equals(x, renameSrc, StringComparison.OrdinalIgnoreCase))
                        {
                            return renameDest;
                        }
                        return x;
                    });
                
                output.WriteLine(string.Join(",", newHeaderNames));

                string line;
                while (true)
                {
                    line = tr.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    totalRows++;
                    output.WriteLine(line);                   

                    if (totalRows % 500000 == 0)
                    {
                        Console.Write(".");
                    }
                }
            }
        }


        class HistRow
        {
            public int Count { get; set; }
        }

        static void GetStats(string file, TextWriter output)
        {
            int totalRows = GetTotalRows(file) - 1;

            var dt = DataTable.New.ReadLazy(file);

            int columnCounts = dt.ColumnNames.Count();

            Console.WriteLine("Getting stats. File has {0} rows x {1} columns.", totalRows, columnCounts);
            Console.WriteLine("(if this takes too long, use -select / -where / -take to shrink the data.");

            var dtCounts = dt.GetColumnValueCounts(0);
            dtCounts.CreateColumns<HistRow>(row => string.Format("{0}%", (row.Count * 100.0 / totalRows)));
            dtCounts.RenameColumn("value", "PercentUnique");

            dtCounts.SaveToStream(output);
        }

        // Provide a "vertical" view where each column is on its own line.
        // This is easier to pick out individual values. 
        static void PreviewVertical(string file, View view)
        {            
            var dt = DataTable.New.ReadLazy(file);

            var selectIndices = new HashSet<int>(view.GetSelectedIndices(dt));
            
                        
            var columns = dt.ColumnNames.ToArray();
            Console.WriteLine("[{0} columns]", columns.Length);

            foreach (var row in dt.Rows)
            {
                if (!view.IsRowIncluded(row))
                {
                    continue;
                }            

                for(int i = 0; i < columns.Length; i++)
                {
                    if (selectIndices.Contains(i))
                    {
                        string value = row.Values[i];
                        Console.WriteLine("{0}: {1}", columns[i], value);
                    }
                }

                // Stop after first. 
                break;
            }
        }


        // Apply filter to produce a new CSV
        // If the filter can skip parsing, then fallback to a fast path. 
        // Slower since it needs parsing         
        static void ApplyFilter(string file, View view, TextWriter output)
        {         
            if (view.Select == null && view.Filters == null)
            {
                PrintFastSummary(file, view.Take, output);
                return;
            }

            var dt = DataTable.New.ReadLazy(file);                        

            var selectIndices = new HashSet<int>(view.GetSelectedIndices(dt));
                     
            var columns = dt.ColumnNames.ToArray();

            // Write headers 
            bool first = true;
            for (int i = 0; i < columns.Length; i++)
            {
                if (selectIndices.Contains(i))
                {
                    if (!first)
                    {
                        output.Write(",");
                    }
                    first =false;
                    output.Write(columns[i]);
                }
            }
            output.WriteLine();

            // Filter on rows. 
            int skipped = 0;
            int rowCount = 0;
            bool stoppedEarly = false; 
            foreach (var row in dt.Rows)
            {
                if (!view.IsRowIncluded(row))
                {
                    skipped++;
                    continue;
                }

                first = true;
                for (int i = 0; i < columns.Length; i++)
                {
                    if (selectIndices.Contains(i))
                    {
                        if (!first)
                        {
                            output.Write(",");
                        }
                        first = false;
                        string value = row.Values[i];
                        output.Write(EscapeCsvCell(value));                        
                    }
                }
                output.WriteLine();

                rowCount++;
                if (view.Take.HasValue && rowCount >= view.Take.Value)
                {
                    stoppedEarly = true;
                    Console.WriteLine("[stopping after {0} rows. Use -take to specify more.]", rowCount);
                    break;                    
                }
            }

            if (!stoppedEarly)
            {
                Console.WriteLine("[File has {0} rows]", rowCount);
            }

            if (view.Filters != null)
            {
                Console.WriteLine("[Where clause skipped {0} rows.]", skipped);
            }
        }


        // Print naive summary 
        // No parsing, so very fast. Good for getting a quick count on large files. 
        static void PrintFastSummary(string file, int? take, TextWriter output)
        {
            // Use StreamReader since it skips parsing and is blazing fast. 
            int N = 5;
            if (take.HasValue)
            {
                N = take.Value;
            }
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
                        output.WriteLine(line);
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

        // Quickly count total rows. No parsing
        private static int GetTotalRows(string filename)
        {
            int count = 0;
            using (var tr = new StreamReader(filename))
            {
                while (true)
                {
                    if (tr.ReadLine() == null)
                    {
                        return count;
                    }
                    count++;
                }
            }
        }

        // $$$ Escape this if it has commas?
        public static string EscapeCsvCell(string s)
        {
            if (s == null)
            {
                return "";
            }
            s = s.Replace('\"', '\'');
            if (s.Contains(","))
            {
                return '\"' + s + '\"';
            }
            return s;
        }
    }
}
