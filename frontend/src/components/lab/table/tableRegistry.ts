import registryJson from "../../../tables/tableRegistry.json";

export interface TableColumnDefinition {
  key: string;
  label: string;
  defaultVisible: boolean;
}

export interface TableDefinition {
  key: string;
  title: string;
  idColumn: string;
  columns: TableColumnDefinition[];
  filters: string[];
}

const definitions = registryJson as TableDefinition[];

export const TABLES: Record<string, TableDefinition> = Object.fromEntries(
  definitions.map((table) => [table.key, table])
);

export function getTable(key: string): TableDefinition {
  const table = TABLES[key];
  if (!table) {
    throw new Error(`Unknown table key: "${key}"`);
  }
  return table;
}
