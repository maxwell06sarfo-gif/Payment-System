export default function DashboardLoading() {
  return (
    <main className="min-h-screen bg-[#f7f8f4] p-6 text-[#17201a]">
      <div className="mx-auto flex max-w-7xl flex-col gap-5">
        <div className="h-16 rounded-lg bg-white shadow-sm" />
        <div className="grid gap-5 lg:grid-cols-[340px_1fr]">
          <div className="h-72 rounded-lg bg-white shadow-sm" />
          <div className="h-72 rounded-lg bg-white shadow-sm" />
        </div>
      </div>
    </main>
  );
}
