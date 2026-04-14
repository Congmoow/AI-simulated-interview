import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        primary: "var(--token-color-primary)",
        "primary-hover": "var(--token-color-primary-hover)",
        "text-primary": "var(--token-color-text-primary)",
        "text-secondary": "var(--token-color-text-secondary)",
        surface: "var(--token-color-bg-surface)",
        page: "var(--token-color-bg-page)",
        success: "var(--token-color-success)",
        warning: "var(--token-color-warning)",
        error: "var(--token-color-error)",
        interviewAi: "var(--token-color-interview-ai)",
        interviewUser: "var(--token-color-interview-user)",
      },
      borderRadius: {
        sm: "var(--token-radius-sm)",
        md: "var(--token-radius-md)",
        lg: "var(--token-radius-lg)",
        xl: "var(--token-radius-xl)",
        "2xl": "var(--token-radius-2xl)",
      },
      boxShadow: {
        card: "var(--token-shadow-card)",
        modal: "var(--token-shadow-modal)",
      },
      fontFamily: {
        sans: "var(--font-body)",
        mono: "var(--font-mono)",
      },
    },
  },
};

export default config;
