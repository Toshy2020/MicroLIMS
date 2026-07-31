import { Button } from "@mui/material";

// Used on every Inventory list page. Relies on the global .no-print
// rule in index.html - the topbar, subnav, and anything else marked
// no-print (forms, action columns) disappear from the printed page,
// leaving just the title and table.
export function PrintButton({ label = "Print" }: { label?: string }) {
  return (
    <Button className="no-print" variant="outlined" onClick={() => window.print()}>
      {label}
    </Button>
  );
}
