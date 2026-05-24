import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "PaymentSystem",
  description: "Subscription billing dashboard for Promotion, Gold, and Diamond plans.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="h-full antialiased">
      <body className="min-h-full">{children}</body>
    </html>
  );
}
