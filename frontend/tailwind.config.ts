import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        primary: "var(--token-color-primary)",
        "primary-hover": "var(--token-color-primary-hover)",
        "primary-active": "var(--token-color-primary-active)",
        "text-primary": "var(--token-color-text-primary)",
        "text-secondary": "var(--token-color-text-secondary)",
        "text-tertiary": "var(--token-color-text-tertiary)",
        surface: "var(--token-color-bg-surface)",
        page: "var(--token-color-bg-page)",
        success: "var(--token-color-success)",
        warning: "var(--token-color-warning)",
        error: "var(--token-color-error)",
        danger: "var(--token-color-danger)",
        info: "var(--token-color-info)",
        interviewAi: "var(--token-color-interview-ai)",
        interviewUser: "var(--token-color-interview-user)",
      },
      borderRadius: {
        sm: "var(--token-radius-sm)",
        md: "var(--token-radius-md)",
        lg: "var(--token-radius-lg)",
        xl: "var(--token-radius-xl)",
        "2xl": "var(--token-radius-2xl)",
        "3xl": "var(--token-radius-3xl)",
        full: "var(--token-radius-full)",
      },
      boxShadow: {
        card: "var(--token-shadow-card)",
        "card-hover": "var(--token-shadow-card-hover)",
        "card-strong": "var(--token-shadow-card-strong)",
        modal: "var(--token-shadow-modal)",
        "button-brand": "var(--token-shadow-button-brand)",
        "state-success": "var(--token-shadow-state-success)",
        "state-warning": "var(--token-shadow-state-warning)",
      },
      fontFamily: {
        sans: "var(--font-body)",
        mono: "var(--font-mono)",
      },
      maxWidth: {
        app: "var(--token-container-app-max)",
        page: "var(--token-container-page-max)",
        content: "var(--token-container-content-max)",
        form: "var(--token-container-form-max)",
      },
      spacing: {
        18: "var(--primitive-space-18)",
        24: "var(--primitive-space-24)",
      },
    },
  },
};

export default config;
