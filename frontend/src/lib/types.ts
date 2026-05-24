export type SubscriptionTier = "Promotion" | "Gold" | "Diamond";

export type SubscriptionDuration = "Monthly" | "SixMonths" | "Yearly";

export type RegistrationResult = {
  isSuccess: boolean;
  message: string;
};

export type AuthTokenResult = {
  isAuthenticated: boolean;
  token: string | null;
};

export type UserResponse = {
  id: string;
  email: string;
  fullName: string;
  stripeCustomerId: string | null;
};

export type SubscriptionResponse = {
  id: string;
  tier: SubscriptionTier;
  duration: SubscriptionDuration;
  status: string;
  price: number;
  currency: string;
  endsAt: string;
  isExpiringSoon: boolean;
  daysUntilExpiration: number;
  expirationNotice: string | null;
};

export type SubscriptionPlanResponse = {
  tier: SubscriptionTier;
  name: string;
  description: string;
  monthlyPrice: number;
  sixMonthPrice: number;
  yearlyPrice: number;
  features: string[];
};

export type UserProfileResponse = {
  user: UserResponse;
  activeSubscription: SubscriptionResponse | null;
};

export type CreateSubscriptionResponse = {
  isSuccess: boolean;
  message: string;
  checkoutUrl: string | null;
  subscription: SubscriptionResponse | null;
};

export type ApiFieldErrors = Record<string, string[]>;

export type ApiFailure = Error & {
  status?: number;
  fieldErrors?: ApiFieldErrors;
};
