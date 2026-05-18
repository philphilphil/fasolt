const animate = require("tailwindcss-animate")
const typography = require("@tailwindcss/typography")

/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: "class",
  content: [
    './index.html',
    './src/**/*.{ts,js,vue}',
    '../fasolt.Server/Pages/**/*.cshtml',
  ],
  theme: {
    container: {
      center: true,
      padding: "2rem",
      screens: {
        "2xl": "1400px",
      },
    },
    extend: {
      colors: {
        // Map shadcn semantic tokens onto the new paper/ink palette.
        border: "var(--rule-1)",
        input: "var(--rule-1)",
        ring: "var(--accent)",
        background: "var(--paper-0)",
        foreground: "var(--ink-0)",
        primary: {
          DEFAULT: "var(--ink-0)",
          foreground: "var(--paper-0)",
        },
        secondary: {
          DEFAULT: "var(--paper-2)",
          foreground: "var(--ink-0)",
        },
        destructive: {
          DEFAULT: "var(--c-again)",
          foreground: "#ffffff",
        },
        muted: {
          DEFAULT: "var(--paper-2)",
          foreground: "var(--ink-2)",
        },
        accent: {
          DEFAULT: "var(--accent)",
          foreground: "var(--accent-on)",
          soft: "var(--accent-soft)",
          hi: "var(--accent-hi)",
        },
        popover: {
          DEFAULT: "var(--paper-1)",
          foreground: "var(--ink-0)",
        },
        card: {
          DEFAULT: "var(--paper-1)",
          foreground: "var(--ink-0)",
        },
        warning: {
          DEFAULT: "var(--c-hard)",
          foreground: "#ffffff",
        },
        success: {
          DEFAULT: "var(--c-good)",
          foreground: "#ffffff",
        },
        paper: {
          0: "var(--paper-0)",
          1: "var(--paper-1)",
          2: "var(--paper-2)",
        },
        ink: {
          0: "var(--ink-0)",
          1: "var(--ink-1)",
          2: "var(--ink-2)",
          3: "var(--ink-3)",
        },
        rule: {
          1: "var(--rule-1)",
          2: "var(--rule-2)",
        },
      },
      fontFamily: {
        sans: ['Geist', 'system-ui', 'sans-serif'],
        mono: ['Geist Mono', 'ui-monospace', 'JetBrains Mono', 'monospace'],
        serif: ['Instrument Serif', 'Times New Roman', 'serif'],
      },
      borderRadius: {
        lg: "var(--radius)",
        md: "calc(var(--radius) - 2px)",
        sm: "calc(var(--radius) - 4px)",
      },
      keyframes: {
        "accordion-down": {
          from: { height: 0 },
          to: { height: "var(--reka-accordion-content-height)" },
        },
        "accordion-up": {
          from: { height: "var(--reka-accordion-content-height)" },
          to: { height: 0 },
        },
        "fade-in": {
          from: { opacity: "0", transform: "translateY(6px)" },
          to: { opacity: "1", transform: "translateY(0)" },
        },
        "glow-pulse": {
          "0%, 100%": { opacity: "1" },
          "50%": { opacity: "0.4" },
        },
        "card-flip-in": {
          from: { opacity: "0", transform: "rotateX(-12deg)" },
          to: { opacity: "1", transform: "rotateX(0)" },
        },
      },
      animation: {
        "accordion-down": "accordion-down 0.2s ease-out",
        "accordion-up": "accordion-up 0.2s ease-out",
        "fade-in": "fade-in 0.4s ease-out both",
        "glow-pulse": "glow-pulse 2s ease-in-out infinite",
        "card-flip-in": "card-flip-in 0.35s cubic-bezier(.4,0,.2,1) both",
      },
    },
  },
  plugins: [animate, typography],
}
