using Microsoft.Win32;
using Prometheus;
using Prometheus.Advanced;
using System;
using System.Reflection;
using Topshelf;

namespace PerformanceCountersExporter
{
    class PerformanceCountersExporterService
    {
        private MetricServer _metricServer;

        public PerformanceCountersExporterService(Uri url, string metricsConfigPath)
        {
            var metricsConfig = MetricsConfig.Load(metricsConfigPath);

            DefaultCollectorRegistry.Instance.Clear();
            DefaultCollectorRegistry.Instance.RegisterOnDemandCollectors(new MetricsCollector(metricsConfig));

            _metricServer = new MetricServer(
                hostname: url.Host,
                port: url.Port,
                url: url.AbsolutePath.Trim('/') + '/',
                useHttps: url.Scheme == "https");
        }

        public void Start()
        {
            _metricServer.Start();
        }

        public void Stop()
        {
            _metricServer.Stop();
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            var exitCode = HostFactory.Run(hc =>
            {
                var url = new Uri("http://localhost:9358/metrics"); // see https://github.com/prometheus/prometheus/wiki/Default-port-allocations
                var metricsConfigPath = "metrics.yml";
                hc.AddCommandLineDefinition("url", value => url = new Uri(value));
                hc.AddCommandLineDefinition("metrics", value => metricsConfigPath = value);
                hc.AfterInstall((ihs) =>
                {
                    // add service parameters.
                    using (var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))
                    using (var service = services.OpenSubKey(ihs.ServiceName, true))
                    {
                        service.SetValue(
                            "ImagePath",
                            string.Format(
                                "{0} -url {1} -metrics {2}",
                                service.GetValue("ImagePath"),
                                url,
                                metricsConfigPath));
                    }
                });
                hc.Service<PerformanceCountersExporterService>(sc =>
                {
                    sc.ConstructUsing(settings => new PerformanceCountersExporterService(url, metricsConfigPath));
                    sc.WhenStarted(service => service.Start());
                    sc.WhenStopped(service => service.Stop());
                });
                hc.EnableServiceRecovery(rc =>
                {
                    rc.RestartService(1); // first failure: restart after 1 minute
                    rc.RestartService(1); // second failure: restart after 1 minute
                    rc.RestartService(1); // subsequent failures: restart after 1 minute
                });
                hc.StartAutomatically();
                hc.RunAsLocalSystem();
                hc.SetDescription(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>().Description);
                hc.SetDisplayName(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title);
                hc.SetServiceName(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product);
            });

            return (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
        }
    }
}
