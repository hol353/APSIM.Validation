using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APSIM.Shared.Utilities;
using System.Reflection;
using System.Data;

namespace PredictedObserved
{
    class Program
    {
        private static string exportFolder;

        static void Main(string[] args)
        {
            try
            {
                exportFolder = FindExportFolder();
                if (args[0].Contains("ApsimX"))
                    ExportDataFromAllDBFiles(args[0]);
                else
                    ExportDataFromAllApsimFiles(args[0]);

            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
            }
        }

        private static string FindExportFolder()
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                                                 "..",
                                                 "..",
                                                 "..",
                                                 "APSIM.Validation.Portal",
                                                 "Database"));
        }

        private static void ExportDataFromAllApsimFiles(string workingFolder)
        {
            string apsimUserInterfaceBinary = Path.Combine(FindBinDirectory(workingFolder), "ApsimUI.exe");

            Directory.CreateDirectory(exportFolder);

            string outputFileName = Path.Combine(exportFolder, "Combined.csv");
            using (StreamWriter writer = new StreamWriter(outputFileName))
            {
                // Write header.
                writer.WriteLine("FileName,VariableName,Predicted,Observed");

                foreach (string apsimFileName in Directory.GetFiles(workingFolder, "*.apsim", SearchOption.AllDirectories))
                {
                    // Run APSIMUI.exe
                    string arguments = StringUtilities.DQuote(apsimFileName) + " PredictedObserved " + exportFolder;
                    Process p = Process.Start(apsimUserInterfaceBinary, arguments);
                    p.WaitForExit();

                    // There should now be a collection of .txt files in the exportFolder.
                    // Combine into a single csv file.
                    foreach (string txtFileName in Directory.GetFiles(exportFolder, "*.txt"))
                    {
                        List<double> predictedValues = new List<double>();
                        List<double> observedValues = new List<double>();
                        using (StreamReader reader = new StreamReader(txtFileName))
                        {
                            string line = reader.ReadLine();
                            while (line != string.Empty)
                            {
                                string[] words = line.Split('\t');
                                if (words.Length >= 2)
                                {
                                    observedValues.Add(Convert.ToDouble(words[0]));
                                    predictedValues.Add(Convert.ToDouble(words[1]));
                                }

                                // read next line
                                line = reader.ReadLine();
                            }
                        }
                        if (predictedValues.Count != observedValues.Count)
                            throw new Exception("Number of predicted values does not equal the number of observed values in file " + txtFileName);

                        // Work out the variable name 
                        string variableName = Path.GetFileNameWithoutExtension(txtFileName).ToLower().Replace("predicted", "").Replace("observed", "").Replace("vs", "");
                        variableName = variableName.Trim();

                        // Work out the crop name
                        string cropName = Path.GetFileNameWithoutExtension(apsimFileName).ToLower().Replace("validation", "").Replace("_", "");
                        cropName = cropName.Trim();

                        // Write everything to file.
                        for (int i = 0; i < predictedValues.Count; i++)
                            writer.WriteLine(cropName + "," + variableName + "," + predictedValues[i] + "," + observedValues[i]);

                        File.Delete(txtFileName);
                    }
                }
            }
        }

        private static string FindBinDirectory(string workingFolder)
        {
            string folder = workingFolder;
            string binFolder = Path.Combine(workingFolder, "Model");
            while (folder != @"C:\" && !Directory.Exists(binFolder))
            {
                folder = Path.GetFullPath(Path.Combine(folder, ".."));
                binFolder = Path.Combine(folder, "Model");
            }

            if (Directory.Exists(binFolder))
                return binFolder;
            else
                throw new Exception("Cannot find bin folder");
        }

        private static void ExportDataFromAllDBFiles(string workingFolder)
        {
            string outputFileName = Path.Combine(exportFolder, "CombinedNextGen.csv");
            using (StreamWriter writer = new StreamWriter(outputFileName))
            {
                // Write header.
                writer.WriteLine("FileName,VariableName,Predicted,Observed");

                foreach (string dbFileName in Directory.GetFiles(workingFolder, "*.db", SearchOption.AllDirectories))
                {
                    SQLite connection = new SQLite();
                    connection.OpenDatabase(dbFileName, readOnly: true);

                    string[] tableNames = GetTableNames(connection);

                    foreach (string tableName in tableNames)
                    {
                        if (tableName.Contains("PredictedObserved") || tableName.StartsWith("PO"))
                            WriteTableToCSV(connection, tableName, writer, dbFileName);
                    }
                }
            }
        }

        /// <summary>Write a specific table to a csv writer</summary>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="writer"></param>
        private static void WriteTableToCSV(SQLite connection, string tableName, StreamWriter writer, string dbFileName)
        {
            string sql = "SELECT * FROM " + tableName;
            DataTable table = connection.ExecuteQuery(sql);

            foreach (DataColumn column in table.Columns)
            {
                if (column.ColumnName.Contains("Observed."))
                {
                    try
                    {
                        double[] observedValues = DataTableUtilities.GetColumnAsDoubles(table, column.ColumnName);
                        string predictedColumnName = column.ColumnName.Replace("Observed.", "Predicted.");
                        double[] predictedValues = DataTableUtilities.GetColumnAsDoubles(table, predictedColumnName);
                        if (observedValues.Length > 0 && predictedValues.Length == observedValues.Length)
                        {
                            string variableName = predictedColumnName.Replace("Predicted.", "").Trim();
                            string fileName = Path.GetFileNameWithoutExtension(dbFileName);
                            fileName = fileName.ToLower().Replace("validation", "").Replace("_", "");
                            fileName = fileName.Trim();

                            // Write everything to file.
                            for (int i = 0; i < predictedValues.Length; i++)
                            {
                                if (!double.IsNaN(predictedValues[i]) && !double.IsNaN(observedValues[i]))
                                    writer.WriteLine(fileName + "NextGen," + variableName + "," + predictedValues[i] + "," + observedValues[i]);
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                }

            }

        }

        /// <summary>Return a list of table names or empty string[]. Never returns null.</summary>
        public static string[] GetTableNames(SQLite connection)
        {
            DataTable table = connection.ExecuteQuery("SELECT * FROM sqlite_master");
            List<string> tables = new List<string>();
            if (table != null)
            {
                tables.AddRange(DataTableUtilities.GetColumnAsStrings(table, "Name"));

                // remove the simulations table
                int simulationsI = tables.IndexOf("Simulations");
                if (simulationsI != -1)
                    tables.RemoveAt(simulationsI);
            }
            return tables.ToArray();
        }

    }
}
