import type { Metadata } from "next";
import DashboardLayout from "#/components/layout/DashboardLayout";

export const metadata: Metadata = {
  title: "Dashboard | Sharp AI",
  description: "Sharp AI",
};

function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return <DashboardLayout>{children}</DashboardLayout>;
}

export default RootLayout;
