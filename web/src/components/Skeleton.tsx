/** Placeholder blocks shown while a page loads, in the rough shape of what is coming. */
export function PageSkeleton() {
  return (
    <div className="page" role="status" aria-label="Loading">
      <div className="skeleton" style={{ height: 32, width: '40%' }} />
      <div className="tiles">
        <div className="skeleton" style={{ height: 104 }} />
        <div className="skeleton" style={{ height: 104 }} />
      </div>
      <div className="skeleton" style={{ height: 260 }} />
    </div>
  )
}
