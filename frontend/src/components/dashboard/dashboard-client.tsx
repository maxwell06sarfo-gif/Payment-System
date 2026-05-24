"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  AlertCircle,
  Bell,
  CalendarDays,
  Check,
  Crown,
  Gem,
  Loader2,
  LogOut,
  RefreshCw,
  ShieldCheck,
  Sparkles,
  User,
  WalletCards,
} from "lucide-react";
import { api, getApiErrorMessage } from "@/lib/api-client";
import { clearToken, getToken } from "@/lib/token";
import type {
  SubscriptionDuration,
  SubscriptionPlanResponse,
  SubscriptionResponse,
  SubscriptionTier,
  UserProfileResponse,
} from "@/lib/types";

const durations: Array<{ value: SubscriptionDuration; label: string }> = [
  { value: "Monthly", label: "Monthly" },
  { value: "SixMonths", label: "6-month" },
  { value: "Yearly", label: "Yearly" },
];

const tierStyle: Record<SubscriptionTier, { icon: typeof Sparkles; accent: string; badge: string }> = {
  Promotion: {
    icon: Sparkles,
    accent: "border-[#7fb6a0]",
    badge: "bg-[#e7f3ee] text-[#16745a]",
  },
  Gold: {
    icon: Crown,
    accent: "border-[#e2b84b]",
    badge: "bg-[#fff5d6] text-[#8a5d00]",
  },
  Diamond: {
    icon: Gem,
    accent: "border-[#8fa7dd]",
    badge: "bg-[#eef3ff] text-[#2854a3]",
  },
};

export function DashboardClient() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const checkoutState = searchParams.get("checkout");
  const [profile, setProfile] = useState<UserProfileResponse | null>(null);
  const [plans, setPlans] = useState<SubscriptionPlanResponse[]>([]);
  const [duration, setDuration] = useState<SubscriptionDuration>("Monthly");
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isSubscribing, setIsSubscribing] = useState<SubscriptionTier | null>(null);
  const [toast, setToast] = useState<{ tone: "success" | "error" | "info"; text: string } | null>(() => {
    if (checkoutState === "success") {
      return { tone: "success", text: "Checkout completed." };
    }

    if (checkoutState === "cancel") {
      return { tone: "info", text: "Checkout canceled." };
    }

    return null;
  });

  useEffect(() => {
    let isActive = true;

    if (!getToken()) {
      router.replace("/");
      return;
    }

    getDashboardData()
      .then(({ nextPlans, nextProfile }) => {
        if (!isActive) {
          return;
        }

        setProfile(nextProfile);
        setPlans(nextPlans);
      })
      .catch((error: unknown) => {
        if (!isActive) {
          return;
        }

        setToast({ tone: "error", text: getApiErrorMessage(error) });
        if ((error as { status?: number }).status === 401) {
          clearToken();
          router.replace("/");
        }
      })
      .finally(() => {
        if (isActive) {
          setIsLoading(false);
        }
      });

    return () => {
      isActive = false;
    };
  }, [router]);

  // Clear checkout status from URL to prevent toast persistence on refresh
  useEffect(() => {
    if (checkoutState) {
      const url = new URL(window.location.href);
      url.searchParams.delete("checkout");
      // Use replaceState to keep the history clean without a full navigation
      window.history.replaceState({}, "", url.pathname + url.search);
    }
  }, [checkoutState]);

  const activeSubscription = profile?.activeSubscription ?? null;
  const sortedPlans = useMemo(
    () =>
      [...plans].sort((left, right) => {
        const order: Record<SubscriptionTier, number> = { Promotion: 1, Gold: 2, Diamond: 3 };
        return order[left.tier] - order[right.tier];
      }),
    [plans],
  );

  async function subscribe(tier: SubscriptionTier) {
    setIsSubscribing(tier);
    setToast(null);

    try {
      const result = await api.createSubscription({ tier, duration });

      if (result.checkoutUrl) {
        window.location.assign(result.checkoutUrl);
        return;
      }

      setToast({ tone: "success", text: result.message });
      await refreshDashboard();
    } catch (error) {
      setToast({ tone: "error", text: getApiErrorMessage(error) });
    } finally {
      setIsSubscribing(null);
    }
  }

  function logout() {
    clearToken();
    router.replace("/");
  }

  async function refreshDashboard() {
    if (!getToken()) {
      router.replace("/");
      return;
    }

    setIsRefreshing(true);

    try {
      const { nextPlans, nextProfile } = await getDashboardData();
      setProfile(nextProfile);
      setPlans(nextPlans);
    } catch (error) {
      setToast({ tone: "error", text: getApiErrorMessage(error) });
      if ((error as { status?: number }).status === 401) {
        clearToken();
        router.replace("/");
      }
    } finally {
      setIsRefreshing(false);
    }
  }

  if (isLoading) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-[#f7f8f4] text-[#17201a]">
        <div className="flex items-center gap-3 rounded-lg bg-white px-4 py-3 text-sm font-semibold shadow-sm">
          <Loader2 aria-hidden className="animate-spin text-[#16745a]" size={18} />
          Loading dashboard
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-[#f7f8f4] px-4 py-5 text-[#17201a] sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-5">
        <header className="flex flex-col gap-4 rounded-lg border border-[#dfe5d8] bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-[#17201a] text-[#f0c85a]">
              <WalletCards aria-hidden size={24} />
            </div>
            <div>
              <p className="text-sm font-semibold uppercase tracking-[0.18em] text-[#66716a]">
                PaymentSystem
              </p>
              <h1 className="text-2xl font-semibold">Subscription Dashboard</h1>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              aria-label="Refresh dashboard"
              className="flex h-10 w-10 items-center justify-center rounded-lg border border-[#ccd6cb] bg-white text-[#2a352d] transition hover:bg-[#eef2e9]"
              disabled={isRefreshing}
              onClick={refreshDashboard}
              title="Refresh"
              type="button"
            >
              <RefreshCw aria-hidden className={isRefreshing ? "animate-spin" : ""} size={18} />
            </button>
            <button
              aria-label="Log out"
              className="flex h-10 w-10 items-center justify-center rounded-lg bg-[#17201a] text-white transition hover:bg-[#263328]"
              onClick={logout}
              title="Log out"
              type="button"
            >
              <LogOut aria-hidden size={18} />
            </button>
          </div>
        </header>

        {toast ? (
          <div
            className={`flex items-center gap-3 rounded-lg border px-4 py-3 text-sm shadow-sm ${
              toast.tone === "success"
                ? "border-[#b6dacb] bg-[#e7f3ee] text-[#145b46]"
                : toast.tone === "error"
                  ? "border-[#efc6bf] bg-[#fff1ee] text-[#9b2c1f]"
                  : "border-[#c5d3ef] bg-[#eef3ff] text-[#2854a3]"
            }`}
          >
            {toast.tone === "success" ? <Check aria-hidden size={18} /> : <AlertCircle aria-hidden size={18} />}
            <span>{toast.text}</span>
          </div>
        ) : null}

        <section className="grid gap-5 lg:grid-cols-[340px_1fr]">
          <aside className="flex flex-col gap-5">
            <ProfilePanel profile={profile} />
            <SubscriptionStatus subscription={activeSubscription} />
          </aside>

          <section className="flex flex-col gap-5 rounded-lg border border-[#dfe5d8] bg-white p-4 shadow-sm sm:p-5">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
              <div>
                <p className="text-sm font-semibold uppercase tracking-[0.16em] text-[#66716a]">
                  Plans
                </p>
                <h2 className="mt-1 text-2xl font-semibold">Promotion, Gold, Diamond</h2>
              </div>

              <div className="flex rounded-lg bg-[#eef2e9] p-1">
                {durations.map((item) => (
                  <button
                    key={item.value}
                    className={`h-10 min-w-24 rounded-md px-3 text-sm font-semibold transition ${
                      duration === item.value
                        ? "bg-white text-[#17201a] shadow-sm"
                        : "text-[#5a665f] hover:text-[#17201a]"
                    }`}
                    onClick={() => setDuration(item.value)}
                    type="button"
                  >
                    {item.label}
                  </button>
                ))}
              </div>
            </div>

            <div className="grid gap-4 xl:grid-cols-3">
              {sortedPlans.map((plan) => {
                const style = tierStyle[plan.tier];
                const Icon = style.icon;
                const isCurrent =
                  activeSubscription?.tier === plan.tier && activeSubscription.status === "Active";

                return (
                  <article
                    className={`flex min-h-[360px] flex-col rounded-lg border-2 bg-white p-5 ${style.accent}`}
                    key={plan.tier}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <span className={`inline-flex items-center gap-2 rounded-md px-2.5 py-1 text-xs font-bold ${style.badge}`}>
                          <Icon aria-hidden size={14} />
                          {plan.name}
                        </span>
                        <p className="mt-4 text-sm leading-6 text-[#66716a]">{plan.description}</p>
                      </div>
                    </div>

                    <div className="mt-5">
                      <p className="text-4xl font-semibold">{formatCurrency(priceFor(plan, duration))}</p>
                      <p className="mt-1 text-sm text-[#66716a]">{durationCopy(duration)}</p>
                    </div>

                    <ul className="mt-5 flex flex-1 flex-col gap-3 text-sm text-[#2a352d]">
                      {plan.features.map((feature) => (
                        <li className="flex gap-2" key={feature}>
                          <Check aria-hidden className="mt-0.5 shrink-0 text-[#16745a]" size={16} />
                          <span>{feature}</span>
                        </li>
                      ))}
                    </ul>

                    <button
                      className="mt-6 flex h-11 items-center justify-center gap-2 rounded-lg bg-[#17201a] px-4 text-sm font-semibold text-white transition hover:bg-[#263328] disabled:cursor-not-allowed disabled:bg-[#8b968e]"
                      disabled={Boolean(isSubscribing)}
                      onClick={() => subscribe(plan.tier)}
                      type="button"
                    >
                      {isSubscribing === plan.tier ? (
                        <Loader2 aria-hidden className="animate-spin" size={17} />
                      ) : (
                        <CreditCardIcon />
                      )}
                      {isCurrent ? "Change duration" : "Checkout"}
                    </button>
                  </article>
                );
              })}
            </div>
          </section>
        </section>
      </div>
    </main>
  );
}

async function getDashboardData() {
  const [nextProfile, nextPlans] = await Promise.all([api.getProfile(), api.getPlans()]);
  return { nextPlans, nextProfile };
}

function ProfilePanel({ profile }: { profile: UserProfileResponse | null }) {
  const name = profile?.user.fullName ?? "Account";

  return (
    <section className="rounded-lg border border-[#dfe5d8] bg-white p-5 shadow-sm">
      <div className="flex items-center gap-3">
        <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-[#eef3ff] text-[#2854a3]">
          <User aria-hidden size={22} />
        </div>
        <div className="min-w-0">
          <h2 className="truncate text-lg font-semibold">{name}</h2>
          <p className="truncate text-sm text-[#66716a]">{profile?.user.email}</p>
        </div>
      </div>

      <div className="mt-5 grid gap-3 text-sm">
        <div className="flex items-center justify-between rounded-lg bg-[#f7f8f4] px-3 py-3">
          <span className="text-[#66716a]">Stripe customer</span>
          <span className="font-semibold">{profile?.user.stripeCustomerId ? "Linked" : "Pending"}</span>
        </div>
        <div className="flex items-center justify-between rounded-lg bg-[#f7f8f4] px-3 py-3">
          <span className="text-[#66716a]">Security</span>
          <span className="inline-flex items-center gap-1 font-semibold text-[#16745a]">
            <ShieldCheck aria-hidden size={15} />
            JWT
          </span>
        </div>
      </div>
    </section>
  );
}

function SubscriptionStatus({ subscription }: { subscription: SubscriptionResponse | null }) {
  return (
    <section className="rounded-lg border border-[#dfe5d8] bg-white p-5 shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.16em] text-[#66716a]">Status</p>
          <h2 className="mt-1 text-xl font-semibold">
            {subscription ? `${subscription.tier} ${subscription.status}` : "No subscription"}
          </h2>
        </div>
        <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-[#fff5d6] text-[#8a5d00]">
          <Bell aria-hidden size={21} />
        </div>
      </div>

      {subscription ? (
        <div className="mt-5 space-y-3">
          <div className="flex items-center justify-between rounded-lg bg-[#f7f8f4] px-3 py-3 text-sm">
            <span className="text-[#66716a]">Renews / expires</span>
            <span className="font-semibold">{formatDate(subscription.endsAt)}</span>
          </div>
          <div className="flex items-center justify-between rounded-lg bg-[#f7f8f4] px-3 py-3 text-sm">
            <span className="text-[#66716a]">Duration</span>
            <span className="font-semibold">{durationLabel(subscription.duration)}</span>
          </div>
          <div className="flex items-center justify-between rounded-lg bg-[#f7f8f4] px-3 py-3 text-sm">
            <span className="text-[#66716a]">Charge</span>
            <span className="font-semibold">{formatCurrency(subscription.price)}</span>
          </div>
          {subscription.expirationNotice ? (
            <div className="flex gap-3 rounded-lg border border-[#e2b84b] bg-[#fff8e6] p-3 text-sm text-[#7a5200]">
              <CalendarDays aria-hidden className="mt-0.5 shrink-0" size={17} />
              <span>{subscription.expirationNotice}</span>
            </div>
          ) : (
            <div className="flex gap-3 rounded-lg border border-[#b6dacb] bg-[#e7f3ee] p-3 text-sm text-[#145b46]">
              <Bell aria-hidden className="mt-0.5 shrink-0" size={17} />
              <span>Renewal alerts active.</span>
            </div>
          )}
        </div>
      ) : (
        <p className="mt-4 text-sm leading-6 text-[#66716a]">
          Choose a Promotion, Gold, or Diamond plan to activate billing.
        </p>
      )}
    </section>
  );
}

function CreditCardIcon() {
  return <WalletCards aria-hidden size={17} />;
}

function priceFor(plan: SubscriptionPlanResponse, duration: SubscriptionDuration) {
  if (duration === "SixMonths") {
    return plan.sixMonthPrice;
  }

  if (duration === "Yearly") {
    return plan.yearlyPrice;
  }

  return plan.monthlyPrice;
}

function durationCopy(duration: SubscriptionDuration) {
  if (duration === "SixMonths") {
    return "billed every 6 months";
  }

  if (duration === "Yearly") {
    return "billed yearly";
  }

  return "billed monthly";
}

function durationLabel(duration: SubscriptionDuration) {
  if (duration === "SixMonths") {
    return "6-month";
  }

  if (duration === "Yearly") {
    return "Yearly";
  }

  return "Monthly";
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(value);
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
  }).format(new Date(value));
}
