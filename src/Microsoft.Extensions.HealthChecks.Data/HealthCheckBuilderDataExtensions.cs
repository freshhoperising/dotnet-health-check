﻿using System;
using System.Data;
using System.Data.SqlClient;

namespace Microsoft.Extensions.HealthChecks
{
    public static class HealthCheckBuilderDataExtensions
    {
        public static HealthCheckBuilder AddSqlCheck(this HealthCheckBuilder builder, string name, string connectionString)
        {
            builder.AddCheck($"SqlCheck({name})", async () =>
            {
                try
                {
                    //TODO: There is probably a much better way to do this.
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandType = CommandType.Text;
                            command.CommandText = "SELECT 1";
                            var result = (int)await command.ExecuteScalarAsync();
                            if (result == 1)
                            {
                                return HealthCheckResult.Healthy($"SqlCheck({name}): Healthy");
                            }

                            return HealthCheckResult.Unhealthy($"SqlCheck({name}): Unhealthy");
                        }
                    }
                }
                catch(Exception ex)
                {
                    return HealthCheckResult.Unhealthy($"SqlCheck({name}): Exception during check: {ex.GetType().FullName}");
                }
            });

            return builder;
        }
    }
}
