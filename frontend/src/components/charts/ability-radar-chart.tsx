"use client";

import dynamic from "next/dynamic";
import { useMemo } from "react";
import type { EChartsOption } from "echarts";
import type { DashboardInsightsDetail } from "@/types/api";

const ReactECharts = dynamic(() => import("echarts-for-react"), { ssr: false });

const RAW_DIMENSION_LABELS: Record<string, string> = {
  clarity: "清晰度",
  fluency: "流畅度",
  technicalAccuracy: "技术正确性",
  knowledgeDepth: "知识深度",
  projectAuthenticity: "项目真实性",
  logicalThinking: "逻辑严谨性",
  confidence: "自信度",
  positionMatch: "岗位匹配度",
};

function readColor(variableName: string, fallback: string): string {
  if (typeof window === "undefined") {
    return fallback;
  }

  return (
    getComputedStyle(document.documentElement).getPropertyValue(variableName).trim() ||
    fallback
  );
}

export function AbilityRadarChart({
  dimensions,
}: {
  dimensions: DashboardInsightsDetail["abilityDimensions6"];
}) {
  const option = useMemo<EChartsOption>(() => {
    const primary = readColor("--token-color-primary", "#0066ff");
    const cyan = readColor("--token-color-interview-user", "#06b6d4");
    const grid = readColor("--token-color-border-default", "#e5e7eb");
    const text = readColor("--token-color-text-secondary", "#4b5563");

    return {
      tooltip: {
        trigger: "item",
        formatter: (params: unknown) => {
          const payload = Array.isArray(params)
            ? (params[0] as { value?: number[] } | undefined)
            : (params as { value?: number[] } | undefined);
          const values = Array.isArray(payload?.value) ? payload.value : [];
          const lines = dimensions.map((item, index) => {
            const score = values[index] ?? item.score;
            const rawDims = item.sourceDimensions
              .map((sourceDimension) => RAW_DIMENSION_LABELS[sourceDimension] ?? sourceDimension)
              .join(" / ");
            return `${item.name}：${score}<br/>来源维度：${rawDims}`;
          });
          return lines.join("<br/><br/>");
        },
      },
      radar: {
        radius: "66%",
        center: ["50%", "54%"],
        splitNumber: 5,
        axisName: {
          color: text,
          fontSize: 12,
        },
        splitLine: {
          lineStyle: {
            color: grid,
          },
        },
        splitArea: {
          areaStyle: {
            color: [
              "rgba(255,255,255,0.92)",
              "rgba(248,250,252,0.86)",
            ],
          },
        },
        axisLine: {
          lineStyle: {
            color: "rgba(17,24,39,0.08)",
          },
        },
        indicator: dimensions.map((item) => ({
          name: item.name,
          max: 100,
        })),
      },
      series: [
        {
          type: "radar",
          symbol: "circle",
          symbolSize: 8,
          itemStyle: {
            color: cyan,
          },
          lineStyle: {
            width: 3,
            color: primary,
          },
          areaStyle: {
            color: "rgba(0, 102, 255, 0.14)",
          },
          data: [
            {
              value: dimensions.map((item) => item.score),
            },
          ],
        },
      ],
    };
  }, [dimensions]);

  return <ReactECharts option={option} style={{ height: 340, width: "100%" }} />;
}
