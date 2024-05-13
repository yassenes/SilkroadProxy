using LkjFramework;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LkjAgent
{
    public class PlannedQuery
    {
        private string dbConnectionString_;
        private DateTime lastDateExecute_;
        public PlannedQuery()
        {
            dbConnectionString_ = "Server=DESKTOP-NS0OUFC;Database=SilkroadWeb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";
            lastDateExecute_ = new DateTime(
                                    1970,
                                    3,
                                    1,
                                    1,
                                    1,
                                    1,
                                    1,
                                    lastDateExecute_.Kind);
        }

        public void Run()
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await ReadDataSet();

                    await Task.Delay(1000);
                }
            });
        }
        public async Task ReadDataSet()
        {
            try
            {
                using (var connection = new SqlConnection(dbConnectionString_))
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT ID, ISNULL(Data1, ''), ISNULL(DateToExecute, '') FROM _PlannedQuery WHERE DateToExecute <= GETDATE() AND DateToExecute >= @Date";
                        command.Parameters.AddWithValue("@Date", lastDateExecute_);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int id = reader.GetInt32(0);
                                string data1 = reader.GetString(1);
                                DateTime date = reader.GetDateTime(2);

                                Console.WriteLine($"[{id}] {DateTime.Now.ToString()}: Query [ {data1} ]");

                                using (var plannedCommand = connection.CreateCommand())
                                {
                                    plannedCommand.CommandText = "EXECUTE (@query)";
                                    plannedCommand.Parameters.AddWithValue("@query", data1);

                                    await plannedCommand.ExecuteNonQueryAsync();

                                    lastDateExecute_ = date;

                                    using (var deletePlannedCommand = connection.CreateCommand())
                                    {
                                        deletePlannedCommand.CommandText = "DELETE FROM _BridgeCommands_Planned WHERE ID = @ID";
                                        deletePlannedCommand.Parameters.AddWithValue("@ID", id);

                                        await deletePlannedCommand.ExecuteNonQueryAsync();
                                    }
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
