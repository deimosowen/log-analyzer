(() => {
    const charts = new Map();
    let echartsLoadPromise = null;

    const levelSeries = [
        { key: "fatal", name: "Fatal", color: "#7f1d1d" },
        { key: "error", name: "Error", color: "#c3392c" },
        { key: "warn", name: "Warn", color: "#d38a0d" },
        { key: "info", name: "Info", color: "#0c8f71" },
        { key: "other", name: "Other", color: "#64748b" }
    ];

    const escapeHtml = (value) => String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;")
        .replaceAll("'", "&#039;");

    const formatNumber = (value) => new Intl.NumberFormat("ru-RU").format(value ?? 0);

    const loadEcharts = () => {
        if (window.echarts) {
            return Promise.resolve(true);
        }

        if (echartsLoadPromise) {
            return echartsLoadPromise;
        }

        echartsLoadPromise = new Promise((resolve) => {
            const existingScript = document.querySelector("script[data-log-analyzer-echarts]");
            if (existingScript) {
                existingScript.addEventListener("load", () => resolve(Boolean(window.echarts)), { once: true });
                existingScript.addEventListener("error", () => resolve(false), { once: true });
                return;
            }

            const script = document.createElement("script");
            script.src = "https://cdn.jsdelivr.net/npm/echarts@5.5.1/dist/echarts.min.js";
            script.async = true;
            script.dataset.logAnalyzerEcharts = "true";
            script.onload = () => resolve(Boolean(window.echarts));
            script.onerror = () => resolve(false);
            document.head.appendChild(script);
        });

        return echartsLoadPromise;
    };

    const createEntry = (elementId, element) => {
        element.replaceChildren();
        const chart = window.echarts.init(element, null, { renderer: "canvas" });
        const resizeObserver = new ResizeObserver(() => chart.resize());
        resizeObserver.observe(element);

        const entry = { chart, resizeObserver, dotNetReference: null };
        charts.set(elementId, entry);
        return entry;
    };

    const buildOption = (payload) => {
        const buckets = payload?.buckets ?? [];
        const labels = buckets.map((bucket, index) => `${index}|${bucket.label}`);
        const anchorIndex = buckets.findIndex((bucket) => bucket.anchor);
        const hasSlider = buckets.length > 18;

        const series = levelSeries.map((level, index) => ({
            name: level.name,
            type: "bar",
            stack: "events",
            data: buckets.map((bucket) => bucket[level.key] ?? 0),
            barMaxWidth: 28,
            emphasis: { focus: "series" },
            itemStyle: {
                color: level.color,
                borderRadius: index === levelSeries.length - 1 ? [5, 5, 0, 0] : 0
            },
            markLine: index === 0 && anchorIndex >= 0
                ? {
                    silent: true,
                    symbol: "none",
                    lineStyle: {
                        color: "#111827",
                        width: 2,
                        type: "solid"
                    },
                    label: {
                        color: "#111827",
                        fontSize: 11,
                        fontWeight: 700,
                        formatter: "выбрано",
                        position: "insideEndTop"
                    },
                    data: [{ xAxis: labels[anchorIndex] }]
                }
                : undefined
        }));

        return {
            animationDuration: 240,
            color: levelSeries.map((level) => level.color),
            grid: {
                top: 52,
                right: 18,
                bottom: hasSlider ? 62 : 34,
                left: 48,
                containLabel: true
            },
            legend: {
                top: 4,
                left: 8,
                itemWidth: 10,
                itemHeight: 10,
                textStyle: {
                    color: "#354052",
                    fontSize: 12,
                    fontWeight: 700
                }
            },
            toolbox: {
                right: 6,
                top: 0,
                itemSize: 14,
                feature: {
                    dataZoom: {
                        yAxisIndex: "none",
                        title: {
                            zoom: "Приблизить",
                            back: "Назад"
                        }
                    },
                    restore: {
                        title: "Сброс"
                    }
                }
            },
            tooltip: {
                trigger: "axis",
                axisPointer: {
                    type: "shadow",
                    shadowStyle: {
                        color: "rgba(15, 107, 143, 0.08)"
                    }
                },
                borderWidth: 0,
                padding: 12,
                backgroundColor: "rgba(18, 24, 32, 0.94)",
                textStyle: {
                    color: "#fff",
                    fontFamily: "Manrope, Segoe UI, sans-serif"
                },
                formatter(items) {
                    const index = items?.[0]?.dataIndex ?? 0;
                    const bucket = buckets[index] ?? {};
                    const rows = items
                        .filter((item) => item.value > 0)
                        .map((item) => `${item.marker}${escapeHtml(item.seriesName)}: <b>${formatNumber(item.value)}</b>`)
                        .join("<br>");

                    const emptyText = rows.length === 0 ? "Событий нет" : rows;
                    const selected = bucket.anchor ? "<div class=\"chart-tooltip-anchor\">Выбранное событие внутри bucket</div>" : "";

                    return `
                        <div class="chart-tooltip">
                            <div class="chart-tooltip-title">${escapeHtml(bucket.fullLabel ?? bucket.label)}</div>
                            <div class="chart-tooltip-total">Всего: ${formatNumber(bucket.total)}</div>
                            ${selected}
                            <div class="chart-tooltip-rows">${emptyText}</div>
                            <div class="chart-tooltip-hint">Клик по столбцу сфокусирует окно корреляции</div>
                        </div>`;
                }
            },
            xAxis: {
                type: "category",
                data: labels,
                axisLine: { lineStyle: { color: "rgba(116, 131, 153, 0.38)" } },
                axisTick: { show: false },
                axisLabel: {
                    color: "#718096",
                    fontWeight: 700,
                    hideOverlap: true,
                    margin: 12,
                    formatter(value) {
                        return String(value).split("|").slice(1).join("|");
                    }
                }
            },
            yAxis: {
                type: "value",
                minInterval: 1,
                splitLine: {
                    lineStyle: {
                        color: "rgba(116, 131, 153, 0.14)"
                    }
                },
                axisLabel: {
                    color: "#718096",
                    fontWeight: 700
                }
            },
            dataZoom: [
                {
                    type: "inside",
                    xAxisIndex: 0,
                    filterMode: "none",
                    moveOnMouseMove: true,
                    zoomOnMouseWheel: true
                },
                {
                    type: "slider",
                    show: hasSlider,
                    xAxisIndex: 0,
                    height: 24,
                    bottom: 14,
                    borderColor: "transparent",
                    fillerColor: "rgba(15, 107, 143, 0.13)",
                    backgroundColor: "rgba(237, 242, 246, 0.92)",
                    handleSize: 16,
                    moveHandleSize: 12,
                    textStyle: {
                        color: "#718096",
                        fontWeight: 700
                    }
                }
            ],
            series
        };
    };

    window.logAnalyzerTimeline = {
        async render(elementId, payload, dotNetReference) {
            const element = document.getElementById(elementId);
            if (!element) {
                return false;
            }

            if (!await loadEcharts()) {
                element.innerHTML = "<div class=\"incident-chart-fallback\">График не загрузился. Проверьте доступность ECharts.</div>";
                return true;
            }

            await new Promise((resolve) => requestAnimationFrame(resolve));

            const entry = charts.get(elementId) ?? createEntry(elementId, element);
            entry.dotNetReference = dotNetReference;

            const buckets = payload?.buckets ?? [];
            entry.chart.setOption(buildOption(payload), true);
            entry.chart.resize();
            requestAnimationFrame(() => entry.chart.resize());
            entry.chart.off("click");
            entry.chart.on("click", (params) => {
                if (params.componentType !== "series") {
                    return;
                }

                const bucket = buckets[params.dataIndex];
                if (!bucket || bucket.total <= 0 || !entry.dotNetReference) {
                    return;
                }

                entry.dotNetReference.invokeMethodAsync("FocusTimelineBucketFromChart", bucket.bucketUtc);
            });

            return true;
        },

        dispose(elementId) {
            const entry = charts.get(elementId);
            if (!entry) {
                return;
            }

            entry.chart.dispose();
            entry.resizeObserver.disconnect();
            charts.delete(elementId);
        }
    };
})();
