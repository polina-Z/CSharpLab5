using System;
using System.Data.SqlClient;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace ServiceLib
{
    public class DataIO
    {
        readonly string connectionString;

        public DataIO(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public Task ClearInsightsAsync()
        {
            return Task.Run(() =>
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlTransaction transaction = connection.BeginTransaction();

                    SqlCommand command = new SqlCommand("sp_ClearInsight", connection);

                    command.CommandType = CommandType.StoredProcedure;
                    command.Transaction = transaction;

                    try
                    {
                        command.ExecuteNonQuery();

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        using (StreamWriter sw = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exceptions.txt"), true))
                        {
                            sw.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} Exception: {ex.Message}");
                        }

                        transaction.Rollback();
                    }
                }
            });
        }

        public Task InsertInsightAsync(string message)
        {
            return Task.Run(() =>
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlTransaction transaction = connection.BeginTransaction();

                    SqlCommand command = new SqlCommand("sp_AddInsight", connection);

                    command.CommandType = CommandType.StoredProcedure;
                    command.Transaction = transaction;

                    try
                    {
                        SqlParameter messageParam = new SqlParameter
                        {
                            ParameterName = "@message",
                            Value = message
                        };

                        SqlParameter timeParam = new SqlParameter
                        {
                            ParameterName = "@time",
                            Value = DateTime.Now
                        };

                        command.Parameters.AddRange(new[] { messageParam, timeParam });

                        command.ExecuteNonQuery();

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        using (StreamWriter sw = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exceptions.txt"), true))
                        {
                            sw.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} Exception: {ex.Message}");
                        }

                        transaction.Rollback();
                    }
                }
            });
        }

        public Task WriteInsightsToXmlAsync(string outputFolder)
        {
            return Task.Run(async () =>
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlTransaction transaction = connection.BeginTransaction();

                    SqlCommand command = new SqlCommand("sp_GetInsight", connection);

                    command.CommandType = CommandType.StoredProcedure;
                    command.Transaction = transaction;

                    try
                    {
                        SqlDataAdapter adapter = new SqlDataAdapter(command);

                        DataSet dataSet = new DataSet("Insights");

                        DataTable dataTable = new DataTable("Insight");

                        dataSet.Tables.Add(dataTable);

                        adapter.Fill(dataSet.Tables["Insight"]);

                        XmlGenerator xmlGenerator = new XmlGenerator(outputFolder);

                        await xmlGenerator.WriteToXmlAsync(dataSet, "appInsights");

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        using (StreamWriter sw = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exceptions.txt"), true))
                        {
                            await sw.WriteLineAsync($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} Exception: {ex.Message}");
                        }

                        transaction.Rollback();
                    }
                }
            });
        }

        public Task GetCustomersAsync(string outputFolder, DataIO appInsights, string customersFileName)
        {
            return Task.Run(async () =>
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlTransaction transaction = connection.BeginTransaction();

                    SqlCommand command = new SqlCommand("sp_GetPerson", connection);

                    command.CommandType = CommandType.StoredProcedure;
                    command.Transaction = transaction;

                    try
                    {
                        SqlDataAdapter adapter = new SqlDataAdapter(command);

                        DataSet dataSet = new DataSet("Person");

                        DataTable dataTable = new DataTable("Person");

                        dataSet.Tables.Add(dataTable);

                        adapter.Fill(dataSet.Tables["Person"]);

                        XmlGenerator xmlGenerator = new XmlGenerator(outputFolder);

                        await xmlGenerator.WriteToXmlAsync(dataSet, customersFileName);

                        await appInsights.InsertInsightAsync("Person were received successfully");

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        await appInsights.InsertInsightAsync("EXCEPTION: " + ex.Message);

                        transaction.Rollback();
                    }
                }
            });
        }
    }
}
