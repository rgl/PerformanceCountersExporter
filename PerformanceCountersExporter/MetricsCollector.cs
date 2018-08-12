using Prometheus;
using Prometheus.Advanced;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace PerformanceCountersExporter
{
    class MetricsCollector : IOnDemandCollector, IDisposable
    {
        Metric[] _metrics;
        private readonly MetricsConfig _config;

        private class Metric : IDisposable
        {
            private readonly ICollectorRegistry _registry;
            public string Name;
            public string Help;
            public PerformanceCounter[] Counters;

            public Metric(ICollectorRegistry registry)
            {
                _registry = registry;
            }

            // NB do not use "instance" as a label. "instance" is used by prometheus to identify the scraped target.
            //    see https://prometheus.io/docs/concepts/jobs_instances/
            //    see https://prometheus.io/docs/concepts/data_model/
            public void Update()
            {
                foreach (var c in Counters)
                {
                    var gauge = Metrics.WithCustomRegistry(_registry).CreateGauge(Name, Help, "counter_instance_name");
                    gauge.Labels(c.InstanceName).Set(c.NextValue());
                    //var gaugeRaw = Metrics.WithCustomRegistry(_registry).CreateGauge(Name + "_raw", Help, "counter_instance_name");
                    //gaugeRaw.Labels(c.InstanceName).Set(c.RawValue);
                }
            }

            public static Metric Create(ICollectorRegistry registry, MetricConfig config)
            {
                var m = Regex.Match(config.Counter, @"\\(?<categoryName>[^(\\]+)(\((?<instanceName>[^\\]+)\))?\\(?<counterName>.+)");

                if (!m.Success)
                {
                    throw new ArgumentOutOfRangeException();
                }

                var categoryName = m.Groups["categoryName"].Value;
                var instanceName = m.Groups["instanceName"].Value;
                var counterName = m.Groups["counterName"].Value;

                var category = new PerformanceCounterCategory(categoryName);

                if (instanceName == "" && category.CategoryType == PerformanceCounterCategoryType.MultiInstance)
                {
                    throw new ArgumentOutOfRangeException("config", "category is MultiInstance but you did not specified a instanceName");
                }

                if (instanceName != "" && category.CategoryType == PerformanceCounterCategoryType.SingleInstance)
                {
                    throw new ArgumentOutOfRangeException("config", "category is SingleInstance but you specified a instanceName");
                }

                var instanceNames = (instanceName == "*" ? category.GetInstanceNames() : new[] {instanceName})
                    .Where(
                        n =>
                        {
                            if (config.CounterInstanceNameFilters == null)
                            {
                                return true;
                            }

                            foreach (var f in config.CounterInstanceNameFilters)
                            {
                                var action = f[0];

                                if (action != '-' && action != '+')
                                {
                                    throw new ArgumentOutOfRangeException("config", "invalid counter_instance_name_filters action");
                                }

                                if (Regex.IsMatch(n, f.Substring(1)))
                                {
                                    if (action == '-')
                                    {
                                        return false;
                                    }
                                }
                            }

                            return true;
                        }
                    ).ToArray();

                var counters = new List<PerformanceCounter>();

                foreach (var n in instanceNames)
                {
                    var counter = new PerformanceCounter(categoryName, counterName, n);

                    // always retreive the first value as most of counters need two samples.
                    counter.NextValue();

                    counters.Add(counter);
                }

                return new Metric(registry)
                {
                    Name = config.Name,
                    Help = config.Help,
                    Counters = counters.ToArray(),
                };
            }

            public void Dispose()
            {
                foreach (var c in Counters)
                {
                    c.Dispose();
                }
            }
        }

        public MetricsCollector(MetricsConfig config)
        {
            _config = config;
        }

        public void Dispose()
        {
            foreach (var m in _metrics)
            {
                m.Dispose();
            }
        }

        public void RegisterMetrics(ICollectorRegistry registry)
        {
            _metrics = _config.Metrics.Select(m => Metric.Create(registry, m)).ToArray();
        }

        public void UpdateMetrics()
        {
            foreach (var m in _metrics)
            {
                m.Update();
            }
        }
    }
}
