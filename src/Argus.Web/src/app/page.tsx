import { ConnectionStatus } from "@/components/connection-status";
import { PortfolioOverview } from "@/components/portfolio-overview";
import { PositionTable } from "@/components/position-table";
import { ConcentrationCharts } from "@/components/concentration-charts";
import { ReconciliationStatus } from "@/components/reconciliation-status";
import { ReplayBanner } from "@/components/replay-banner";
import { ReplayControls } from "@/components/replay-controls";

export default function DashboardPage() {
  return (
    <>
      {/* Replay mode banner - shown above everything when active */}
      <ReplayBanner />

      <main className="min-h-screen bg-background p-6">
        <div className="mx-auto max-w-[1600px] space-y-6">
          {/* Header */}
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-2xl font-bold tracking-tight">
                Argus Risk Dashboard
              </h1>
              <p className="text-sm text-muted-foreground">
                Real-time portfolio risk monitoring
              </p>
            </div>
            <ConnectionStatus />
          </div>

        {/* Portfolio Overview Cards */}
        <PortfolioOverview />

        {/* Concentration Charts */}
        <ConcentrationCharts />

        {/* Reconciliation and Replay Controls */}
        <div className="grid gap-6 lg:grid-cols-2">
          <ReconciliationStatus />
          <ReplayControls />
        </div>

        {/* Positions Grid */}
        <section>
          <h2 className="mb-4 text-lg font-semibold">Open Positions</h2>
          <PositionTable />
        </section>
      </div>
    </main>
    </>
  );
}
