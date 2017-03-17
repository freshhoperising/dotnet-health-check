using System;
using Microsoft.Extensions.HealthChecks;

namespace Microsoft.AspNetCore.Hosting
{
    public static class WebHostExtensions
    {
        private const int DEFAULT_TIMEOUT_SECONDS = 300;

        public static void RunWhenHealthy(this IWebHost webHost)
        {
            webHost.RunWhenHealthy(TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS));
        }

        public static void RunWhenHealthy(this IWebHost webHost, TimeSpan timeout)
        {
            var healthChecks = webHost.Services.GetService(typeof(IHealthCheckService)) as IHealthCheckService;

            var loops = 0;
            do
            {
                var checkResult = healthChecks.CheckHealthAsync().Result;
                if (checkResult.CheckStatus == CheckStatus.Healthy)
                {
                    webHost.Run();
                    break;
                }

                System.Threading.Thread.Sleep(1000);
                loops++;

            } while (loops < timeout.TotalSeconds);
        }
    }
}
