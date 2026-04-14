"use client";

import dynamic from "next/dynamic";
import { useMemo } from "react";
import type { EChartsOption } from "echarts";

const ReactECharts = dynamic(() => import("echarts-for-react"), { ssr: false });

function readColor(variableName: string, fallback: string): string {
  if (typeof window === "undefined") {
    return fallback;
  }

  return (
    getComputedStyle(document.documentElement).getPropertyValue(variableName).trim() ||
    fallback
  );
}

export function GrowthChart({
  points,
}: {
  points: Array<{ date: string; score: number }>;
}) {
  const option = useMemo<EChartsOption>(() => {
    const primary = readColor("--token-color-primary", "#0066ff");
    const cyan = readColor("--token-color-interview-user", "#06b6d4");
    const grid = readColor("--token-color-border-default", "#e5e7eb");
    const text = readColor("--token-color-text-secondary", "#4b5563");

    return {
      grid: {
        left: 24,
        right: 24,
        top: 24,
        bottom: 24,
      },
      tooltip: {
        trigger: "axis",
      },
      xAxis: {
        type: "category",
        data: points.map((point) => point.date),
        axisLine: {
          lineStyle: {
            color: grid,
          },
        },
        axisLabel: {
          color: text,
        },
      },
      yAxis: {
        type: "value",
        min: 0,
        max: 100,
        axisLine: {
          show: false,
        },
        splitLine: {
          lineStyle: {
            color: grid,
          },
        },
        axisLabel: {
          color: text,
        },
      },
      series: [
        {
          type: "line",
          smooth: true,
          data: points.map((point) => point.score),
          lineStyle: {
            width: 3,
            color: primary,
          },
          itemStyle: {
            color: cyan,
          },
          areaStyle: {
            color: {
              type: "linear",
              x: 0,
              y: 0,
              x2: 0,
              y2: 1,
              colorStops: [
                { offset: 0, color: "rgba(0, 102, 255, 0.18)" },
                { offset: 1, color: "rgba(6, 182, 212, 0.02)" },
              ],
            },
          },
        },
      ],
    };
  }, [points]);

  return <ReactECharts option={option} style={{ height: 320, width: "100%" }} />;
}
