"use client";

import { useMemo, useRef, useState } from "react";
import {
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  useReactTable,
  type SortingState,
} from "@tanstack/react-table";
import { useVirtualizer } from "@tanstack/react-virtual";
import { useRisk } from "@/providers/risk-provider";
import type { PositionRisk } from "@/types/domain";
import { Skeleton } from "@/components/ui/skeleton";
import { columns } from "./columns";
import { TableToolbar } from "./table-toolbar";

const ROW_HEIGHT = 40;

export function PositionTable() {
  const { snapshot } = useRisk();
  const [sorting, setSorting] = useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  const parentRef = useRef<HTMLDivElement>(null);

  const data: PositionRisk[] = snapshot?.positions ?? [];

  const totalMarketValue = useMemo(
    () =>
      data.reduce(
        (sum, p) => sum + Math.abs(p.currentPrice * p.quantity),
        0
      ),
    [data]
  );

  const table = useReactTable({
    data,
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    globalFilterFn: (row, _columnId, filterValue: string) => {
      return row.original.symbol
        .toLowerCase()
        .includes(filterValue.toLowerCase());
    },
    meta: { totalMarketValue },
  });

  const { rows } = table.getRowModel();

  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => ROW_HEIGHT,
    overscan: 10,
  });

  if (!snapshot) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-10 w-64" />
        {Array.from({ length: 8 }).map((_, i) => (
          <Skeleton key={i} className="h-10 w-full" />
        ))}
      </div>
    );
  }

  return (
    <div>
      <TableToolbar searchValue={globalFilter} onSearchChange={setGlobalFilter} />

      <div className="rounded-md border border-border">
        {/* Sticky header */}
        <div className="sticky top-0 z-10 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-b border-border">
          {table.getHeaderGroups().map((headerGroup) => (
            <div key={headerGroup.id} className="flex">
              {headerGroup.headers.map((header) => (
                <div
                  key={header.id}
                  className="flex-shrink-0 px-3 py-2 text-xs font-medium text-muted-foreground uppercase tracking-wider cursor-pointer select-none hover:text-foreground"
                  style={{ width: header.getSize() }}
                  onClick={header.column.getToggleSortingHandler()}
                >
                  {flexRender(
                    header.column.columnDef.header,
                    header.getContext()
                  )}
                  {{
                    asc: " ↑",
                    desc: " ↓",
                  }[header.column.getIsSorted() as string] ?? ""}
                </div>
              ))}
            </div>
          ))}
        </div>

        {/* Virtualised rows */}
        <div
          ref={parentRef}
          className="overflow-auto"
          style={{ height: Math.min(rows.length * ROW_HEIGHT, 600) }}
        >
          <div
            style={{
              height: virtualizer.getTotalSize(),
              position: "relative",
              width: "100%",
            }}
          >
            {virtualizer.getVirtualItems().map((virtualRow) => {
              const row = rows[virtualRow.index];
              return (
                <div
                  key={row.id}
                  className="absolute flex w-full border-b border-border/50 hover:bg-muted/50 transition-colors"
                  style={{
                    height: ROW_HEIGHT,
                    transform: `translateY(${virtualRow.start}px)`,
                  }}
                >
                  {row.getVisibleCells().map((cell) => (
                    <div
                      key={cell.id}
                      className="flex-shrink-0 flex items-center px-3 text-sm"
                      style={{ width: cell.column.getSize() }}
                    >
                      {flexRender(
                        cell.column.columnDef.cell,
                        cell.getContext()
                      )}
                    </div>
                  ))}
                </div>
              );
            })}
          </div>
        </div>
      </div>

      <p className="mt-2 text-xs text-muted-foreground">
        {rows.length} position{rows.length !== 1 ? "s" : ""}
        {globalFilter && ` matching "${globalFilter}"`}
      </p>
    </div>
  );
}
