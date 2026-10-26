import { useEffect, useRef, useState } from 'react'

/** Tracks an element's rendered width so SVG charts can draw at 1:1 pixel scale. */
export function useWidth<T extends HTMLElement>(fallback = 600) {
  const ref = useRef<T>(null)
  const [width, setWidth] = useState(fallback)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    setWidth(el.clientWidth || fallback)
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width || fallback))
    observer.observe(el)
    return () => observer.disconnect()
  }, [fallback])

  return [ref, width] as const
}
