using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.HealthChecks.Internal;

namespace Microsoft.Extensions.HealthChecks
{
    public static class HealthCheckBuilderExtensions
    {
        public static HealthCheckBuilder AddMinValueCheck<T>(this HealthCheckBuilder builder, string name, T minValue, Func<T> currentValueFunc)
            where T : IComparable<T>
        {
            Guard.ArgumentNotNull(nameof(builder), builder);
            Guard.ArgumentNotNullOrWhitespace(nameof(name), name);
            Guard.ArgumentNotNull(nameof(currentValueFunc), currentValueFunc);

            builder.AddCheck(name, () =>
            {
                var currentValue = currentValueFunc();
                var status = currentValue.CompareTo(minValue) >= 0 ? CheckStatus.Healthy : CheckStatus.Unhealthy;
                return HealthCheckResult.FromStatus(status, $"{name}: min={minValue}, current={currentValue}");
            });

            return builder;
        }

        public static HealthCheckBuilder AddMaxValueCheck<T>(this HealthCheckBuilder builder, string name, T maxValue, Func<T> currentValueFunc)
            where T : IComparable<T>
        {
            Guard.ArgumentNotNull(nameof(builder), builder);
            Guard.ArgumentNotNullOrWhitespace(nameof(name), name);
            Guard.ArgumentNotNull(nameof(currentValueFunc), currentValueFunc);

            builder.AddCheck($"{name}", () =>
            {
                var currentValue = currentValueFunc();
                var status = currentValue.CompareTo(maxValue) <= 0 ? CheckStatus.Healthy : CheckStatus.Unhealthy;
                return HealthCheckResult.FromStatus(status, $"{name}: max={maxValue}, current={currentValue}");
            });

            return builder;
        }

        public static HealthCheckBuilder AddPrivateMemorySizeCheck(this HealthCheckBuilder builder, long maxSize)
            => AddMaxValueCheck(builder, $"PrivateMemorySize({maxSize})", maxSize, () => Process.GetCurrentProcess().PrivateMemorySize64);

        public static HealthCheckBuilder AddUrlCheck(this HealthCheckBuilder builder, string url)
            => AddUrlCheck(builder, url, response => DefaultUrlCheck(response));

        public static HealthCheckBuilder AddUrlCheck(this HealthCheckBuilder builder, string url,
                                                     Func<HttpResponseMessage, IHealthCheckResult> checkFunc)
        {
            Guard.ArgumentNotNull(nameof(checkFunc), checkFunc);

            return AddUrlCheck(builder, url, response => new ValueTask<IHealthCheckResult>(checkFunc(response)));
        }

        public static HealthCheckBuilder AddUrlCheck(this HealthCheckBuilder builder, string url,
                                                     Func<HttpResponseMessage, Task<IHealthCheckResult>> checkFunc)
        {
            Guard.ArgumentNotNull(nameof(checkFunc), checkFunc);

            return AddUrlCheck(builder, url, response => new ValueTask<IHealthCheckResult>(checkFunc(response)));
        }

        public static HealthCheckBuilder AddUrlCheck(this HealthCheckBuilder builder, string url,
                                                     Func<HttpResponseMessage, ValueTask<IHealthCheckResult>> checkFunc)
        {
            Guard.ArgumentNotNull(nameof(builder), builder);
            Guard.ArgumentNotNullOrWhitespace(nameof(url), url);
            Guard.ArgumentNotNull(nameof(checkFunc), checkFunc);

            var urlCheck = new UrlChecker(checkFunc, url);
            builder.AddCheck($"UrlCheck({url})", () => urlCheck.CheckAsync());
            return builder;
        }

        public static HealthCheckBuilder AddUrlChecks(this HealthCheckBuilder builder, IEnumerable<string> urlItems, string groupName)
            => AddUrlChecks(builder, urlItems, groupName, CheckStatus.Warning, response => DefaultUrlCheck(response));

        public static HealthCheckBuilder AddUrlChecks(this HealthCheckBuilder builder, IEnumerable<string> urlItems, string groupName,
                                                      Func<HttpResponseMessage, IHealthCheckResult> checkFunc)
        {
            Guard.ArgumentNotNull(nameof(checkFunc), checkFunc);

            return AddUrlChecks(builder, urlItems, groupName, CheckStatus.Warning, response => new ValueTask<IHealthCheckResult>(checkFunc(response)));
        }

        public static HealthCheckBuilder AddUrlChecks(this HealthCheckBuilder builder, IEnumerable<string> urlItems, string groupName,
                                                      Func<HttpResponseMessage, Task<IHealthCheckResult>> checkFunc)
        {
            Guard.ArgumentNotNull(nameof(checkFunc), checkFunc);

            return AddUrlChecks(builder, urlItems, groupName, CheckStatus.Warning, response => new ValueTask<IHealthCheckResult>(checkFunc(response)));
        }

        public static HealthCheckBuilder AddUrlChecks(this HealthCheckBuilder builder, IEnumerable<string> urlItems, string groupName,
                                                      Func<HttpResponseMessage, ValueTask<IHealthCheckResult>> checkFunc)
        {
            Guard.ArgumentNotNull(nameof(checkFunc), checkFunc);

            return AddUrlChecks(builder, urlItems, groupName, CheckStatus.Warning, response => checkFunc(response));
        }

        public static HealthCheckBuilder AddUrlChecks(this HealthCheckBuilder builder, IEnumerable<string> urlItems, string groupName,
                                                      CheckStatus partialSuccessStatus)
            => AddUrlChecks(builder, urlItems, groupName, partialSuccessStatus, response => DefaultUrlCheck(response));

        public static HealthCheckBuilder AddUrlChecks(this HealthCheckBuilder builder, IEnumerable<string> urlItems, string groupName,
                                                      CheckStatus partialSuccessStatus, Func<HttpResponseMessage, IHealthCheckResult> checkFunc)
        {
            Guard.ArgumentNotNull(nameof(checkFunc), checkFunc);

            return AddUrlChecks(builder, urlItems, groupName, partialSuccessStatus, response => new ValueTask<IHealthCheckResult>(checkFunc(response)));
        }

        public static HealthCheckBuilder AddUrlChecks(this HealthCheckBuilder builder, IEnumerable<string> urlItems, string groupName,
                                                      CheckStatus partialSuccessStatus, Func<HttpResponseMessage, Task<IHealthCheckResult>> checkFunc)
        {
            Guard.ArgumentNotNull(nameof(checkFunc), checkFunc);

            return AddUrlChecks(builder, urlItems, groupName, partialSuccessStatus, response => new ValueTask<IHealthCheckResult>(checkFunc(response)));
        }

        public static HealthCheckBuilder AddUrlChecks(this HealthCheckBuilder builder, IEnumerable<string> urlItems, string groupName,
                                                      CheckStatus partialSuccessStatus, Func<HttpResponseMessage, ValueTask<IHealthCheckResult>> checkFunc)
        {
            var urls = urlItems?.ToArray();

            Guard.ArgumentNotNull(nameof(builder), builder);
            Guard.ArgumentNotNullOrEmpty(nameof(urlItems), urls);
            Guard.ArgumentNotNullOrWhitespace(nameof(groupName), groupName);

            var urlChecker = new UrlChecker(checkFunc, urls) { PartiallyHealthyStatus = partialSuccessStatus };
            builder.AddCheck($"UrlChecks({groupName})", () => urlChecker.CheckAsync());
            return builder;
        }

        public static HealthCheckBuilder AddVirtualMemorySizeCheck(this HealthCheckBuilder builder, long maxSize)
            => AddMaxValueCheck(builder, $"VirtualMemorySize({maxSize})", maxSize, () => Process.GetCurrentProcess().VirtualMemorySize64);

        public static HealthCheckBuilder AddWorkingSetCheck(this HealthCheckBuilder builder, long maxSize)
            => AddMaxValueCheck(builder, $"WorkingSet({maxSize})", maxSize, () => Process.GetCurrentProcess().WorkingSet64);

        //TODO: Move this into a seperate project. Avoid DB dependencies in the main lib.
        //TODO: It is probably better if this is more generic, not SQL specific.
        public static HealthCheckBuilder AddSqlCheck(this HealthCheckBuilder builder, string connectionString)
        {
            builder.AddCheck($"SQL Check:", async () =>
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
                                return HealthCheckResult.Healthy($"AddSqlCheck: {connectionString}");
                            }

                            return HealthCheckResult.Unhealthy($"AddSqlCheck: {connectionString}");
                        }
                    }
                }
                catch
                {
                    return HealthCheckResult.Unhealthy($"AddSqlCheck: {connectionString}");
                }
            });

            return builder;
        }

        // Helpers

        private static ValueTask<IHealthCheckResult> DefaultUrlCheck(HttpResponseMessage response)
        {
            // REVIEW: Should this be an explicit 200 check, or just an "is success" check?
            var status = response.StatusCode == HttpStatusCode.OK ? CheckStatus.Healthy : CheckStatus.Unhealthy;
            IHealthCheckResult result = HealthCheckResult.FromStatus(status, $"UrlCheck({response.RequestMessage.RequestUri}): status code {response.StatusCode} ({(int)response.StatusCode})");
            return new ValueTask<IHealthCheckResult>(result);
        }
    }
}