interface Props {
  caption: string
  columns: string[]
  rows: string[][]
}

/** The accessible alternative to a chart: every plotted value, no hover needed. */
export function ChartTable({ caption, columns, rows }: Props) {
  return (
    <div className="chart-table-wrap">
      <table className="chart-table">
        <caption className="sr-only">{caption}</caption>
        <thead>
          <tr>
            {columns.map((c) => (
              <th key={c} scope="col">
                {c}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r, i) => (
            <tr key={i}>
              {r.map((cell, j) => (
                <td key={j}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
