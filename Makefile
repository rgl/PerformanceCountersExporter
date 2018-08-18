dist: dist/PerformanceCountersExporter.zip

dist/PerformanceCountersExporter.zip: dist/PerformanceCountersExporter.exe dist/metrics.yml dist/grafana-dashboard.json
	cd dist && \
		rm -f PerformanceCountersExporter.zip && \
		zip -9 PerformanceCountersExporter.zip \
			PerformanceCountersExporter.exe{,.config} \
			metrics.yml \
			grafana-dashboard.json && \
		unzip -l PerformanceCountersExporter.zip && \
		sha256sum PerformanceCountersExporter.zip

dist/PerformanceCountersExporter.exe: PerformanceCountersExporter/bin/Release/PerformanceCountersExporter.exe tmp/libz.exe
	mkdir -p dist
	# NB to be able to load Serilog.Sinks.File from .config we need to use Scenario 4 as
	#    described at https://github.com/MiloszKrajewski/LibZ/blob/master/doc/scenarios.md
	cd PerformanceCountersExporter/bin/Release && \
		../../../tmp/libz add --libz PerformanceCountersExporter.libz --include '*.dll' --move && \
		../../../tmp/libz inject-libz --assembly PerformanceCountersExporter.exe --libz PerformanceCountersExporter.libz --move && \
		../../../tmp/libz instrument --assembly PerformanceCountersExporter.exe --libz-resources
	cp PerformanceCountersExporter/bin/Release/PerformanceCountersExporter.exe* dist

dist/metrics.yml: PerformanceCountersExporter/metrics.yml
	mkdir -p dist
	cp $< $@

dist/grafana-dashboard.json: PerformanceCountersExporter/grafana-dashboard.json
	mkdir -p dist
	cp $< $@

tmp/libz.exe:
	mkdir -p tmp
	wget -Otmp/libz-1.2.0.0-tool.zip https://github.com/MiloszKrajewski/LibZ/releases/download/1.2.0.0/libz-1.2.0.0-tool.zip
	unzip -d tmp tmp/libz-1.2.0.0-tool.zip

PerformanceCountersExporter/bin/Release/PerformanceCountersExporter.exe: PerformanceCountersExporter/*
	dotnet msbuild -m -p:Configuration=Release -t:restore -t:build

clean:
	dotnet msbuild -m -p:Configuration=Release -t:clean
	rm -rf tmp dist

.PHONY: dist clean
