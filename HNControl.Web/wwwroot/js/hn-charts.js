// HN Control - Chart.js theme helpers (2026 refresh)
(function () {
  if (!window.Chart) return;

  function cssVar(name, fallback) {
    try {
      const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
      return v || fallback;
    } catch { return fallback; }
  }

  function hexToRgba(hex, alpha) {
    const h = (hex || "").trim();
    if (!h) return `rgba(37,99,235,${alpha})`;
    // supports #rgb or #rrggbb
    const raw = h.startsWith("#") ? h.slice(1) : h;
    const full = raw.length === 3 ? raw.split("").map(c => c + c).join("") : raw;
    const int = parseInt(full, 16);
    const r = (int >> 16) & 255;
    const g = (int >> 8) & 255;
    const b = int & 255;
    return `rgba(${r},${g},${b},${alpha})`;
  }

  const palette = [
    cssVar("--hn-blue", "#2563eb"),
    cssVar("--hn-cyan", "#06b6d4"),
    cssVar("--hn-purple", "#7c3aed"),
    cssVar("--hn-green", "#22c55e"),
    cssVar("--hn-amber", "#f59e0b"),
  ];

  const primary = palette[0];
  const grid = "rgba(15,23,42,.08)";

  Chart.defaults.font.family = 'Inter, system-ui, -apple-system, "Segoe UI", Roboto, Arial, sans-serif';
  Chart.defaults.color = cssVar("--hn-muted", "#64748b");
  Chart.defaults.borderColor = grid;

  Chart.defaults.interaction.mode = "index";
  Chart.defaults.interaction.intersect = false;

  Chart.defaults.plugins.legend.position = "bottom";
  Chart.defaults.plugins.legend.labels.usePointStyle = true;
  Chart.defaults.plugins.legend.labels.boxWidth = 8;
  Chart.defaults.plugins.legend.labels.boxHeight = 8;
  Chart.defaults.plugins.tooltip.padding = 10;

  // line defaults
  Chart.defaults.elements.line.borderWidth = 2;
  Chart.defaults.elements.line.tension = 0.35;
  Chart.defaults.elements.line.borderColor = primary;
  Chart.defaults.elements.line.backgroundColor = hexToRgba(primary, 0.12);
  Chart.defaults.elements.point.radius = 0;
  Chart.defaults.elements.point.hoverRadius = 4;

  // bar defaults
  Chart.defaults.datasets.bar.borderRadius = 10;
  Chart.defaults.datasets.bar.borderSkipped = false;

  // doughnut defaults
  Chart.defaults.datasets.doughnut.borderWidth = 0;
  Chart.defaults.datasets.pie.borderWidth = 0;

  function decorate(config) {
    if (!config) return config;

    config.options = config.options || {};
    config.options.maintainAspectRatio = config.options.maintainAspectRatio ?? false;

    // tidy scales
    const scales = (config.options.scales = config.options.scales || {});
    for (const k of Object.keys(scales)) {
      const s = scales[k] || (scales[k] = {});
      s.grid = s.grid || {};
      s.grid.color = s.grid.color || grid;
      s.ticks = s.ticks || {};
      s.ticks.maxTicksLimit = s.ticks.maxTicksLimit || 8;
    }

    // dataset colors
    if (config.data && Array.isArray(config.data.datasets)) {
      config.data.datasets.forEach((ds, i) => {
        const c = palette[i % palette.length];
        if (!ds.borderColor) ds.borderColor = c;
        if (!ds.backgroundColor) {
          ds.backgroundColor = (config.type === "line")
            ? hexToRgba(c, 0.12)
            : hexToRgba(c, 0.18);
        }
        if (config.type === "line") {
          ds.fill = ds.fill ?? true;
          ds.pointRadius = ds.pointRadius ?? 0;
          ds.pointHoverRadius = ds.pointHoverRadius ?? 4;
          ds.borderWidth = ds.borderWidth ?? 2;
        }
      });
    }

    return config;
  }

  window.hnCharts = { decorate, palette, hexToRgba };
})();