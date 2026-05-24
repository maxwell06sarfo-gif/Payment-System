const tokenKey = "payment_system_token";
const authCookieName = "ps_auth";

export function getToken() {
  if (typeof window === "undefined") {
    return null;
  }

  return window.localStorage.getItem(tokenKey);
}

export function saveToken(token: string) {
  window.localStorage.setItem(tokenKey, token);
  document.cookie = `${authCookieName}=1; path=/; max-age=604800; SameSite=Lax`;
}

export function clearToken() {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(tokenKey);
  document.cookie = `${authCookieName}=; path=/; max-age=0; SameSite=Lax`;
}
