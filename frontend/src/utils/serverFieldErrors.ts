/**
 * Extracts per-field validation messages from an API error response.
 * Supports ASP.NET ValidationProblemDetails (`errors: { Field: ["msg"] }`),
 * flat maps (`errors` / `fieldErrors: { field: "msg" }`), and `ApiResponse.Fail`
 * `errors: string[]` when a message matcher is provided.
 */
export function extractServerFieldErrors<K extends string>(
  err: unknown,
  keyMap: Record<string, K>,
  matchMessage?: (text: string) => K | null
): Partial<Record<K, string>> {
  const data = (err as { response?: { data?: unknown } } | null)?.response?.data as
    | { errors?: unknown; fieldErrors?: unknown }
    | undefined;
  if (!data || typeof data !== "object") return {};

  const result: Partial<Record<K, string>> = {};
  const mapSource = (src: unknown) => {
    if (!src || typeof src !== "object" || Array.isArray(src)) return;
    for (const [rawKey, rawVal] of Object.entries(src as Record<string, unknown>)) {
      const mapped = keyMap[rawKey.trim().toLowerCase()];
      if (!mapped) continue;
      if (typeof rawVal === "string" && rawVal.trim()) {
        result[mapped] = rawVal.trim();
      } else if (Array.isArray(rawVal) && typeof rawVal[0] === "string" && rawVal[0].trim()) {
        result[mapped] = rawVal[0].trim();
      }
    }
  };

  mapSource(data.fieldErrors);
  mapSource(data.errors);
  if (Object.keys(result).length > 0) return result;

  if (Array.isArray(data.errors) && matchMessage) {
    for (const item of data.errors) {
      if (typeof item !== "string" || !item.trim()) continue;
      const mapped = matchMessage(item);
      if (mapped && !result[mapped]) result[mapped] = item.trim();
    }
  }

  return result;
}
