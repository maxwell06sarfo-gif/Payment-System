import axios, { AxiosError } from "axios";
import { clearToken, getToken } from "@/lib/token";
import type {
  ApiFailure,
  AuthTokenResult,
  CreateSubscriptionResponse,
  RegistrationResult,
  SubscriptionDuration,
  SubscriptionPlanResponse,
  SubscriptionTier,
  UserProfileResponse,
} from "@/lib/types";

type BackendErrorPayload = {
  message?: string;
  Message?: string;
  errors?: Record<string, string[]>;
  Errors?: Record<string, string[]>;
  detailed?: string;
  Detailed?: string;
};

export const apiClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5149",
  headers: {
    "Content-Type": "application/json",
  },
});

apiClient.interceptors.request.use((config) => {
  const token = getToken();

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<BackendErrorPayload>) => {
    if (error.response?.status === 401) {
      clearToken();
    }

    return Promise.reject(normalizeApiError(error));
  },
);

export const api = {
  async register(payload: { fullName: string; email: string; password: string }) {
    const response = await apiClient.post<RegistrationResult>("/api/auth/register", payload);
    return response.data;
  },

  async login(payload: { email: string; password: string }) {
    const response = await apiClient.post<AuthTokenResult>("/api/auth/login", payload);
    return response.data;
  },

  async getProfile() {
    const response = await apiClient.get<UserProfileResponse>("/api/users/me");
    return response.data;
  },

  async getPlans() {
    const response = await apiClient.get<SubscriptionPlanResponse[]>("/api/subscriptions/plans");
    return response.data;
  },

  async createSubscription(payload: { tier: SubscriptionTier; duration: SubscriptionDuration }) {
    const response = await apiClient.post<CreateSubscriptionResponse>("/api/subscriptions", payload);
    return response.data;
  },
};

export function getApiErrorMessage(error: unknown) {
  if (error instanceof Error) {
    return error.message;
  }

  return "Something went wrong.";
}

function normalizeApiError(error: AxiosError<BackendErrorPayload>): ApiFailure {
  const payload = error.response?.data;
  const message =
    payload?.message ??
    payload?.Message ??
    payload?.detailed ??
    payload?.Detailed ??
    error.message ??
    "Something went wrong.";

  const normalized = new Error(message) as ApiFailure;
  normalized.status = error.response?.status;
  normalized.fieldErrors = payload?.errors ?? payload?.Errors;
  return normalized;
}
