import type { ChartSeriesInput } from '../components/BaseChart.vue'
import type { CutTotalsPoint, PeriodAverage, TotalsBand, TotalKey } from '../types/dashboard'

/**
 * DASH-1/DASH-11: raw per-cut lifetime series → BaseChart-ready datasets.
 * One line per selected concept, unaggregated and unfiltered by period
 * containment (design.md Decision 9 — the picker recombines client-side
 * without refetch; DASH-11's exclusion only applies to the band, never here).
 */
export function buildLifetimeSeries(
  points: CutTotalsPoint[],
  selectedKeys: TotalKey[],
  labelFor: (key: TotalKey) => string,
): ChartSeriesInput[] {
  return selectedKeys.map((key) => ({
    key,
    label: labelFor(key),
    data: points.map((p) => p[key]),
  }))
}

/**
 * Blends a CSS color (hex, oklch, DaisyUI CSS var value, ...) toward
 * transparent via `color-mix()` — works for any valid CSS color string,
 * avoiding hex-only alpha math that would break on non-hex theme colors.
 */
export function withAlpha(color: string, alpha: number): string {
  const pct = Math.round(Math.min(1, Math.max(0, alpha)) * 100)
  return `color-mix(in srgb, ${color} ${pct}%, transparent)`
}

/**
 * DASH-2/3/11: period-averaged min/max deviation band.
 *
 * Chosen rendering technique (design.md leaves the exact Chart.js approach
 * open — "fill-between-datasets or similar"): per selected concept, 3
 * datasets in this exact order — `min` (invisible, anchors the fill),
 * `max` (`fill: '-1'`, shading the area back to `min` — Chart.js's
 * fill-between-datasets technique), and `avg` (solid line on top). Per
 * design.md's response shape, `band.<key>` is a SINGLE aggregate
 * {avg,min,max} — not a per-period series — so the shaded band is flat
 * across every period; only the `avg` line varies per period, plotting
 * `periods[].avg[key]`. The flat band gives visual context for how far the
 * period-average trend strays from the budget's overall historical range.
 */
export function buildBandChartSeries(
  periods: PeriodAverage[],
  band: TotalsBand,
  selectedKeys: TotalKey[],
  labelFor: (key: TotalKey) => string,
  palette: string[],
): ChartSeriesInput[] {
  const result: ChartSeriesInput[] = []

  selectedKeys.forEach((key, index) => {
    const color = palette[index % palette.length] ?? '#3b82f6'
    const bandValue = band[key]
    const label = labelFor(key)

    result.push({
      key: `${key}:min`,
      label: `${label} (min)`,
      data: periods.map(() => bandValue.min),
      borderColor: 'transparent',
      backgroundColor: 'transparent',
      pointRadius: 0,
      fill: false,
    })
    result.push({
      key: `${key}:max`,
      label: `${label} (max)`,
      data: periods.map(() => bandValue.max),
      borderColor: 'transparent',
      backgroundColor: withAlpha(color, 0.18),
      pointRadius: 0,
      fill: '-1',
    })
    result.push({
      key: `${key}:avg`,
      label,
      data: periods.map((p) => p.avg[key]),
      borderColor: color,
      backgroundColor: color,
      pointRadius: 2,
      fill: false,
    })
  })

  return result
}
