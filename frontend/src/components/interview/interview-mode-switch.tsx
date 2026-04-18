"use client";

import { cn } from "@/lib/cn";

interface InterviewModeSwitchProps {
  options: Array<{
    value: string;
    label: string;
  }>;
  value: string;
  onChange: (value: string) => void;
}

export function InterviewModeSwitch({
  options,
  value,
  onChange,
}: InterviewModeSwitchProps) {
  const activeIndex = Math.max(
    0,
    options.findIndex((option) => option.value === value),
  );

  return (
    <div className="interview-mode-switch">
      <span
        aria-hidden="true"
        className="interview-mode-switch__indicator"
        style={{
          width: `${100 / options.length}%`,
          transform: `translateX(${activeIndex * 100}%)`,
        }}
      />
      {options.map((option) => {
        const active = option.value === value;

        return (
          <button
            className={cn(
              "interview-mode-switch__button",
              active && "interview-mode-switch__button--active",
            )}
            key={option.value}
            onClick={() => onChange(option.value)}
            type="button"
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
