using APSIM.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.UI;
using System.Web.UI.DataVisualization.Charting;
using System.Web.UI.WebControls;

namespace APSIM.Validation.Portal
{
    public partial class Main : System.Web.UI.Page
    {
        private DataTable allData = null;

        /// <summary>
        /// Form is being loaded - populate controls
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void Page_Load(object sender, EventArgs e)
        {
            allData = LoadDataFromFile();

            // Populate the model drop down.
            if (ModelList.Items.Count == 0)
                PopulateModelDropDownList();

            // Populate the variable drop down.
            PopulateVariableDropDownList();

            PopulateGridView();

            string[] fileNames = GetFileNames();
            if (fileNames.Length > 0)
                PopulateGraph(Chart1, fileNames[0], VariableList.Text);
            if (fileNames.Length > 1)
                PopulateGraph(Chart2, fileNames[1], VariableList.Text);
        }

        /// <summary>
        /// Populate the graph.
        /// </summary>
        private void PopulateGraph(Chart chart, string fileName, string variableName)
        {
            DataView view = GetDataView(fileName, ModelList.SelectedItem.Text, variableName);
            chart.DataSource = view;

            DataTable stats = GetRegressionStats(view, variableName);

            Series s = chart.Series.Add("Data");
            s.MarkerStyle = MarkerStyle.Circle;
            s.MarkerSize = 8;
            s.ChartType = SeriesChartType.Point;
            s.XValueMember = "Observed";
            s.YValueMembers = "Predicted";
            chart.DataBind();

            Series oneToOneSeries = chart.Series.Add("1:1");
            oneToOneSeries.MarkerStyle = MarkerStyle.None;
            oneToOneSeries.Color = System.Drawing.Color.Black;
            oneToOneSeries.ChartType = SeriesChartType.Line;
            oneToOneSeries.Points.DataBindXY(DataTableUtilities.GetColumnAsDoubles(stats, "x"),
                                             DataTableUtilities.GetColumnAsDoubles(stats, "1:1y"));

            Series regrSeries = chart.Series.Add("Regression");
            regrSeries.MarkerStyle = MarkerStyle.None;
            regrSeries.Color = System.Drawing.Color.Black;
            regrSeries.ChartType = SeriesChartType.Line;
            regrSeries.Points.DataBindXY(DataTableUtilities.GetColumnAsDoubles(stats, "x"),
                                         DataTableUtilities.GetColumnAsDoubles(stats, "Regressiony"));

            chart.ChartAreas[0].AxisX.RoundAxisValues();
            chart.ChartAreas[0].AxisX.Minimum = 0;
            chart.ChartAreas[0].AxisY.Minimum = 0;
            chart.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
            chart.ChartAreas[0].AxisY.MajorGrid.Enabled = false;
            chart.ChartAreas[0].AxisX.Title = "Observed " + variableName;
            chart.ChartAreas[0].AxisY.Title = "Predicted " + variableName;
            chart.ChartAreas[0].AxisX.TitleFont = new System.Drawing.Font(chart.ChartAreas[0].AxisX.TitleFont.FontFamily, 12);
            chart.ChartAreas[0].AxisY.TitleFont = new System.Drawing.Font(chart.ChartAreas[0].AxisX.TitleFont.FontFamily, 12);
            chart.Titles.Add(fileName);
            chart.Titles[0].Font = new System.Drawing.Font(chart.ChartAreas[0].AxisX.TitleFont.FontFamily, 12);
        }

        /// <summary>
        /// Calculate and return pred/obs regression stats for the specified view.
        /// </summary>
        private DataTable GetRegressionStats(DataView view, string variableName)
        {
            DataTable stats = new DataTable();
            stats.Columns.Add("x", typeof(double));
            stats.Columns.Add("1:1y", typeof(double));
            stats.Columns.Add("Regressiony", typeof(double));

            double[] predicted = DataTableUtilities.GetColumnAsDoubles(view, "Predicted");
            double[] observed = DataTableUtilities.GetColumnAsDoubles(view, "Observed");

            double maximum = Math.Max(MathUtilities.Max(predicted), MathUtilities.Max(observed));
            double minimum = Math.Min(MathUtilities.Min(predicted), MathUtilities.Min(observed));

            MathUtilities.RegrStats stat = MathUtilities.CalcRegressionStats(variableName, predicted, observed);

            if (stat != null)
            {
                DataRow newRow = stats.NewRow();
                newRow[0] = minimum;
                newRow[1] = minimum;
                newRow[2] = stat.Slope * minimum + stat.Intercept;
                stats.Rows.Add(newRow);
                newRow = stats.NewRow();
                newRow[0] = maximum;
                newRow[1] = maximum;
                newRow[2] = stat.Slope * maximum + stat.Intercept;
                stats.Rows.Add(newRow);
            }
            return stats;
        }

        /// <summary>
        /// Populate the model drop down list.
        /// </summary>
        private void PopulateModelDropDownList()
        {
            SortedSet<string> models = new SortedSet<string>();
            foreach (string model in DataTableUtilities.GetColumnAsStrings(allData, "Model"))
                models.Add(model);
            foreach (string model in models)
                ModelList.Items.Add(model);
            ModelList.SelectedIndex = 0;
        }

        /// <summary>
        /// Populate the variable drop down list.
        /// </summary>
        private void PopulateVariableDropDownList()
        {
            string selectedItem = VariableList.Text;
            string[] variableNames = GetVariableNames();
            VariableList.Items.Clear();
            bool foundSelectedItem = false;
            foreach (string variable in variableNames)
            {
                VariableList.Items.Add(variable);
                if (variable == selectedItem)
                    foundSelectedItem = true;
            }
            if (foundSelectedItem)
                VariableList.Text = selectedItem;
            else
                VariableList.SelectedIndex = 0;
        }

        /// <summary>
        /// Populate the data grid with stats for the current model.
        /// </summary>
        private void PopulateGridView()
        {
            // Get a list of filenames in the all data table.
            string[] fileNames = GetFileNames();

            DataTable statsTable = new DataTable();
            statsTable.Columns.Add("Variable", typeof(string));
            statsTable.Columns.Add("Stat", typeof(string));
            foreach (string fileName in fileNames)
                statsTable.Columns.Add(fileName, typeof(double));
            statsTable.Columns.Add("Diff", typeof(double));

            // Get a list of variable names in the all data table.
            string[] variables = GetVariableNames();

            foreach (string variable in variables)
            {
                List<MathUtilities.RegrStats> stats = new List<MathUtilities.RegrStats>();
                foreach (string fileName in fileNames)
                {
                    DataView view = GetDataView(fileName, ModelList.SelectedItem.Text, variable);

                    double[] predicted = DataTableUtilities.GetColumnAsDoubles(view, "Predicted");
                    double[] observed = DataTableUtilities.GetColumnAsDoubles(view, "Observed");
                    if (predicted.Length > 0 && predicted.Length == observed.Length)
                    {
                        MathUtilities.RegrStats stat = MathUtilities.CalcRegressionStats(variable, predicted, observed);
                        stats.Add(stat);
                    }
                    else
                        stats.Add(null);
                }

                foreach (FieldInfo field in typeof(MathUtilities.RegrStats).GetFields())
                {
                    if (field.Name != "Name")
                    {
                        DataRow row = statsTable.NewRow();
                        row[0] = variable;
                        row[1] = field.Name;
                        if (stats[0] != null)
                            row[2] = field.GetValue(stats[0]);
                        if (stats[1] != null)
                        {
                            row[3] = field.GetValue(stats[1]);
                            if (stats[0] != null)
                                row[4] = Convert.ToDouble(row[2]) - Convert.ToDouble(row[3]);
                        }
                        statsTable.Rows.Add(row);
                    }
                }
            }

            GridView.DataSource = statsTable;
            GridView.DataBind();
        }

        /// <summary>
        /// Get a list of file names.
        /// </summary>
        /// <returns></returns>
        private string[] GetFileNames()
        {
            SortedSet<string> fileNames = new SortedSet<string>();
            foreach (string model in DataTableUtilities.GetColumnAsStrings(allData, "FileName"))
                fileNames.Add(model);
            return fileNames.ToArray();
        }

        /// <summary>
        /// Return a data view for a specific data series.
        /// </summary>
        private DataView GetDataView(string fileName, string modelName, string variableName)
        {
            DataView view = new DataView(allData);
            view.RowFilter = "FileName='" + fileName + "'" +
                             " AND Model='" + modelName + "'" +
                             " AND Variable='" + variableName + "'";

            return view;
        }

        /// <summary>
        /// Get a list of unique variable names for the currently selected model.
        /// </summary>
        private string[] GetVariableNames()
        {
            // Get a list of filenames in the all data table.
            SortedSet<string> fileNames = new SortedSet<string>();
            foreach (string model in DataTableUtilities.GetColumnAsStrings(allData, "FileName"))
                fileNames.Add(model);

            SortedSet<string> variables = new SortedSet<string>();
            foreach (string fileName in fileNames)
            {
                DataView view = new DataView(allData);
                view.RowFilter = "FileName='" + fileName + "'" +
                                 " AND Model='" + ModelList.SelectedItem.Text + "'";

                foreach (string model in DataTableUtilities.GetColumnAsStrings(view, "Variable"))
                    variables.Add(model);
            }

            return variables.ToArray();
        }

        /// <summary>
        /// Load all predicted / observed data from file.
        /// </summary>
        private static DataTable LoadDataFromFile()
        {
            string path = HttpContext.Current.Request.Url.AbsolutePath;

            DataTable table = new DataTable();
            table.Columns.Add("FileName", typeof(string));
            table.Columns.Add("Model", typeof(string));
            table.Columns.Add("Variable", typeof(string));
            table.Columns.Add("Predicted", typeof(double));
            table.Columns.Add("Observed", typeof(double));
            string dataPath = HttpContext.Current.Server.MapPath("~");
            string[] csvFiles = Directory.GetFiles(dataPath, "*.csv");
            if (csvFiles.Length == 0)
            {
                dataPath = Path.Combine(dataPath, "Database");
                csvFiles = Directory.GetFiles(dataPath, "*.csv");
            }
            foreach (string csvFileName in csvFiles)
            {
                using (StreamReader reader = new StreamReader(csvFileName))
                {
                    reader.ReadLine();
                    string line = reader.ReadLine();
                    while (line != null && line != string.Empty)
                    {
                        string[] words = line.Split(',');
                        if (words.Length == 4)
                        {
                            DataRow row = table.NewRow();
                            row["FileName"] = Path.GetFileNameWithoutExtension(csvFileName);
                            row["Model"] = words[0];
                            row["Variable"] = words[1];
                            row["Predicted"] = Convert.ToDouble(words[2]);
                            row["Observed"] = Convert.ToDouble(words[3]);
                            table.Rows.Add(row);
                        }

                        // read next line
                        line = reader.ReadLine();
                    }
                }
            }
            return table;
        }

        /// <summary>
        /// User has clicked 'Graph' button in grid view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnRowCommand(object sender, GridViewCommandEventArgs e)
        {
            int rowIndex = Convert.ToInt32(e.CommandArgument);
            if (rowIndex < GridView.Rows.Count)
            {
                DataTable table = GridView.DataSource as DataTable;
                string variableName = table.Rows[rowIndex][0].ToString();
                
                Response.Redirect(Request.FilePath + "?Variable=" + variableName);
            }
        }

        /// <summary>
        /// Row has been data bound - apply formatting.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void GridView_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            for (int col = 2; col <= 4; col++)
            {
                double value;
                if (double.TryParse(e.Row.Cells[col].Text, out value))
                {
                    e.Row.Cells[col].Text = value.ToString("F3");
                    if (col == 4)
                    {
                        double tolerance = Convert.ToDouble(e.Row.Cells[3].Text) * 0.01;  // 1% 
                        if (!double.IsNaN(value) && MathUtilities.IsGreaterThan(Math.Abs(value), tolerance))
                            e.Row.Cells[col].BackColor = System.Drawing.Color.Salmon;
                    }
                }
            }
        }

    }
}