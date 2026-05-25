"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowRight,
  BadgeCheck,
  CreditCard,
  Loader2,
  LockKeyhole,
  ShieldCheck,
  Sparkles,
} from "lucide-react";
import { api, getApiErrorMessage } from "@/lib/api-client";
import { getToken, saveToken, clearToken } from "@/lib/token";
import type { ApiFailure } from "@/lib/types";

type AuthMode = "login" | "register";

type AuthForm = {
  fullName: string;
  email: string;
  password: string;
};

const emptyForm: AuthForm = {
  fullName: "",
  email: "",
  password: "",
};

export function AuthPanel() {
  const router = useRouter();
  const [mode, setMode] = useState<AuthMode>("login");
  const [form, setForm] = useState<AuthForm>(emptyForm);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]> | undefined>();

  const title = mode === "login" ? "Welcome back" : "Create account";
  const submitLabel = mode === "login" ? "Sign in" : "Create account";

  const passwordHint = useMemo(() => {
    if (form.password.length === 0) {
      return "8+ characters";
    }

    return form.password.length >= 8 ? "Ready" : `${8 - form.password.length} more`;
  }, [form.password.length]);

  useEffect(() => {
    const token = getToken();
    if (!token) return;

    // Token exists in storage — verify it is still accepted by the API before
    // redirecting. A stale or expired token must land back on the login screen,
    // not silently break the dashboard.
    api.getProfile()
      .then(() => router.replace("/dashboard"))
      .catch(() => clearToken());
  }, [router]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setMessage(null);
    setFieldErrors(undefined);

    try {
      if (mode === "register") {
        const registration = await api.register(form);

        if (!registration.isSuccess) {
          throw new Error(registration.message);
        }

        // Show success message and switch to login mode
        setMessage("Account created successfully! Please sign in.");
        setMode("login");
        return;
      }

      const login = await api.login({
        email: form.email,
        password: form.password,
      });

      if (!login.isAuthenticated || !login.token) {
        throw new Error("Invalid email or password.");
      }

      saveToken(login.token);
      router.replace("/dashboard");
    } catch (error) {
      const apiError = error as ApiFailure;
      setMessage(getApiErrorMessage(error));
      setFieldErrors(apiError.fieldErrors);
    } finally {
      setIsSubmitting(false);
    }
  }

  function switchMode(nextMode: AuthMode) {
    setMode(nextMode);
    setMessage(null);
    setFieldErrors(undefined);
  }

  return (
    <main className="grid min-h-screen bg-[#f7f8f4] text-[#17201a] lg:grid-cols-[0.95fr_1.05fr]">
      <section className="flex min-h-[320px] flex-col justify-between bg-[#17201a] p-6 text-white sm:p-8 lg:min-h-screen">
        <div className="flex items-center gap-3">
          <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-[#f0c85a] text-[#17201a]">
            <CreditCard aria-hidden size={22} />
          </div>
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-[#f0c85a]">
              PaymentSystem
            </p>
            <p className="text-sm text-white/68">Promotion billing</p>
          </div>
        </div>

        <div className="max-w-xl py-10">
          <div className="mb-5 inline-flex items-center gap-2 rounded-md border border-white/15 bg-white/8 px-3 py-2 text-sm text-white/82">
            <ShieldCheck aria-hidden size={17} />
            JWT secured dashboard
          </div>
          <h1 className="max-w-lg text-4xl font-semibold leading-tight sm:text-5xl">
            Manage paid promotion subscriptions with a clean Stripe flow.
          </h1>
        </div>

        <div className="grid gap-3 sm:grid-cols-3">
          {[
            ["Promotion", "starter reach"],
            ["Gold", "priority reach"],
            ["Diamond", "premium reach"],
          ].map(([name, value]) => (
            <div key={name} className="rounded-lg border border-white/12 bg-white/7 p-4">
              <p className="text-sm font-semibold">{name}</p>
              <p className="mt-1 text-sm text-white/62">{value}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="flex items-center justify-center px-4 py-8 sm:px-6">
        <div className="w-full max-w-[520px] rounded-lg border border-[#dfe5d8] bg-white p-5 shadow-sm sm:p-7">
          <div className="mb-6 flex rounded-lg bg-[#eef2e9] p-1">
            {(["login", "register"] as const).map((entry) => (
              <button
                key={entry}
                type="button"
                onClick={() => switchMode(entry)}
                className={`h-11 flex-1 rounded-md text-sm font-semibold transition ${
                  mode === entry
                    ? "bg-white text-[#17201a] shadow-sm"
                    : "text-[#5a665f] hover:text-[#17201a]"
                }`}
              >
                {entry === "login" ? "Login" : "Register"}
              </button>
            ))}
          </div>

          <div className="mb-6">
            <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-lg bg-[#e7f3ee] text-[#16745a]">
              {mode === "login" ? <LockKeyhole aria-hidden size={23} /> : <Sparkles aria-hidden size={23} />}
            </div>
            <h2 className="text-3xl font-semibold">{title}</h2>
            <p className="mt-2 text-sm leading-6 text-[#66716a]">
              {mode === "login"
                ? "Use your account credentials to continue."
                : "Your account opens the subscription dashboard next."}
            </p>
          </div>

          {message ? (
            <div className="mb-5 flex gap-3 rounded-lg border border-[#efc6bf] bg-[#fff1ee] p-3 text-sm text-[#9b2c1f]">
              <AlertCircle aria-hidden className="mt-0.5 shrink-0" size={18} />
              <span>{message}</span>
            </div>
          ) : null}

          <form className="space-y-4" onSubmit={handleSubmit}>
            {mode === "register" ? (
              <Field
                error={getFieldError(fieldErrors, "FullName")}
                label="Full name"
                name="fullName"
                onChange={(value) => setForm((current) => ({ ...current, fullName: value }))}
                type="text"
                value={form.fullName}
              />
            ) : null}

            <Field
              error={getFieldError(fieldErrors, "Email")}
              label="Email"
              name="email"
              onChange={(value) => setForm((current) => ({ ...current, email: value }))}
              type="email"
              value={form.email}
            />

            <div>
              <Field
                autoComplete={mode === "register" ? "new-password" : "current-password"}
                error={getFieldError(fieldErrors, "Password")}
                label="Password"
                name="password"
                onChange={(value) => setForm((current) => ({ ...current, password: value }))}
                type="password"
                value={form.password}
              />
              <div className="mt-2 flex items-center gap-2 text-xs text-[#66716a]">
                <BadgeCheck aria-hidden size={14} />
                <span>{passwordHint}</span>
              </div>
            </div>

            <button
              className="mt-2 flex h-12 w-full items-center justify-center gap-2 rounded-lg bg-[#17201a] px-4 text-sm font-semibold text-white transition hover:bg-[#263328] disabled:cursor-not-allowed disabled:bg-[#8b968e]"
              disabled={isSubmitting}
              type="submit"
            >
              {isSubmitting ? <Loader2 aria-hidden className="animate-spin" size={18} /> : <ArrowRight aria-hidden size={18} />}
              {submitLabel}
            </button>
          </form>
        </div>
      </section>
    </main>
  );
}

function Field({
  autoComplete,
  error,
  label,
  name,
  onChange,
  type,
  value,
}: {
  autoComplete?: string;
  error?: string;
  label: string;
  name: string;
  onChange: (value: string) => void;
  type: string;
  value: string;
}) {
  // Derive a sensible default when the caller does not specify autoComplete.
  // Password fields default to "current-password"; everything else falls back
  // to the field name so the browser can match saved form data.
  const resolvedAutoComplete = autoComplete ?? (type === "password" ? "current-password" : name);

  return (
    <label className="block" htmlFor={name}>
      <span className="mb-2 block text-sm font-semibold text-[#2a352d]">{label}</span>
      <input
        autoComplete={resolvedAutoComplete}
        className={`h-12 w-full rounded-lg border bg-white px-3 text-sm outline-none transition ${
          error ? "border-[#d4513f]" : "border-[#ccd6cb] focus:border-[#16745a]"
        }`}
        id={name}
        name={name}
        onChange={(event) => onChange(event.target.value)}
        required
        type={type}
        value={value}
      />
      {error ? <span className="mt-2 block text-xs font-medium text-[#9b2c1f]">{error}</span> : null}
    </label>
  );
}

function getFieldError(errors: Record<string, string[]> | undefined, name: string) {
  return errors?.[name]?.[0] ?? errors?.[name.toLowerCase()]?.[0];
}
