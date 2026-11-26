import {useLayoutEffect, useRef, useState, type CSSProperties} from "react";
import {NewYearGarlandRoot, NewYearHatRoot, NewYearSnowRoot} from "./newYearStyles";

const snowBandHeight = 900;
const snowflakesPerBand = 44;

export function NewYearSnow() {
  const rootRef = useRef<HTMLDivElement>(null);
  const [bandCount, setBandCount] = useState(1);

  useLayoutEffect(() => {
    const root = rootRef.current;
    if (!root) return;

    const updateBandCount = () => {
      setBandCount(Math.max(1, Math.ceil(root.getBoundingClientRect().height / snowBandHeight)));
    };

    updateBandCount();
    const observer = new ResizeObserver(updateBandCount);
    observer.observe(root);
    return () => observer.disconnect();
  }, []);

  return (
    <NewYearSnowRoot
      ref={rootRef}
      aria-hidden="true"
      style={{"--snow-band-height": `${snowBandHeight}px`} as CSSProperties}
    >
      {Array.from({length: bandCount * snowflakesPerBand}, (_, i) => (
        <span
          key={i}
          style={
            {
              left: `${(i * 37) % 100}%`,
              top: Math.floor(i / snowflakesPerBand) * snowBandHeight - 12,
              "--snow-size": `${3 + (i % 4)}px`,
              "--snow-duration": `${18 + (i % 13)}s`,
              "--snow-delay": `${-i * 2.7}s`,
              "--snow-drift": `${(i % 2 === 0 ? 1 : -1) * (25 + (i % 5) * 12)}px`,
            } as CSSProperties
          }
        />
      ))}
    </NewYearSnowRoot>
  );
}

export function NewYearGarland() {
  return (
    <NewYearGarlandRoot aria-hidden="true">
      {Array.from({length: 24}, (_, i) => (
        <span key={i} style={{"--bulb-delay": `${-(i % 4)}s`} as CSSProperties} />
      ))}
    </NewYearGarlandRoot>
  );
}

export function NewYearHat() {
  return (
    <NewYearHatRoot viewBox="0 0 48 36" aria-hidden="true" focusable="false">
      <path d="M8 27C11 14 18 3 29 4c8 0 11 7 12 15l-8-4-2 13Z" fill="#d93649" />
      <path d="M14 25C17 14 22 8 29 6c-5 6-7 13-7 20Z" fill="#ef5965" />
      <rect x="5" y="24" width="31" height="9" rx="4.5" fill="#fff8ed" />
      <circle cx="41" cy="20" r="5" fill="#fff8ed" />
    </NewYearHatRoot>
  );
}
