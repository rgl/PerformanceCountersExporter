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
	cd PerformanceCountersExporter/bin/Release && \
		../../../tmp/libz.exe inject-dll \
			--assembly PerformanceCountersExporter.exe \
			--include '*.dll' \
			--move
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
