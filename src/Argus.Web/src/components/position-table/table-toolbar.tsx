"use client";

import { Input } from "@/components/ui/input";

interface TableToolbarProps {
  searchValue: string;
  onSearchChange: (value: string) => void;
}

export function TableToolbar({ searchValue, onSearchChange }: TableToolbarProps) {
  return (
    <div className="flex items-center gap-3 pb-4">
      <Input
        placeholder="Search symbols..."
        value={searchValue}
        onChange={(e) => onSearchChange(e.target.value)}
        className="max-w-xs"
      />
    </div>
  );
}
