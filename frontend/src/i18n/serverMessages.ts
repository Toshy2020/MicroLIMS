import { i18n } from "./index";

// A refusal the server sent with a message code (F4 Task 4): the code, the values
// its sentence needs, and the English sentence the server wrote.
export interface ServerErrorDetail {
  code: string;
  params?: Record<string, string>;
  message: string;
}

export const SERVER_MESSAGES_NAMESPACE = "serverMessages";

interface ErrorBody {
  message?: string;
  errors?: string[];
  fieldErrors?: Array<{ field?: string; message?: string; row?: number | null }>;
  errorDetails?: ServerErrorDetail[];
}

function isDetail(value: unknown): value is ServerErrorDetail {
  return (
    typeof value === "object" &&
    value !== null &&
    typeof (value as ServerErrorDetail).code === "string" &&
    typeof (value as ServerErrorDetail).message === "string"
  );
}

// The sentence for one coded refusal in the interface language: the template for
// its code with the parameters filled in, or the server's English sentence when
// the code has no template yet.
export function translateServerDetail(detail: ServerErrorDetail): string {
  return i18n.t(detail.code, {
    ns: SERVER_MESSAGES_NAMESPACE,
    keySeparator: false, // codes are "Module.Reason", a flat key, not a path
    defaultValue: detail.message,
    ...detail.params
  });
}

// Rewrites the sentence screens read (data.message, data.errors, the field
// errors that carry the same sentence) to the interface language. A response
// without errorDetails is returned untouched, so an uncoded refusal behaves
// exactly as before. Returns the translated primary sentence, or null.
export async function translateServerErrorBody(body: unknown): Promise<string | null> {
  if (typeof body !== "object" || body === null) return null;
  const data = body as ErrorBody;
  const details = Array.isArray(data.errorDetails) ? data.errorDetails.filter(isDetail) : [];
  if (details.length === 0) return null;

  await i18n.loadNamespaces(SERVER_MESSAGES_NAMESPACE);
  const english = data.message;
  const translated = details.map(translateServerDetail);

  // A response can use one coded sentence or join several in validation order.
  // Only replace a multi-message summary when it matches those sentences.
  const primary = details.length === 1 || english === details.map((detail) => detail.message).join(" ")
    ? translated.join(" ")
    : null;
  if (primary !== null) {
    data.message = primary;
    if (Array.isArray(data.errors)) data.errors = data.errors.map((e) => (e === english ? primary : e));
    if (Array.isArray(data.fieldErrors)) {
      data.fieldErrors = data.fieldErrors.map((f) => (f.message === english ? { ...f, message: primary } : f));
    }
  }
  return primary;
}

export function hasServerErrorCodePrefix(err: unknown, prefix: string): boolean {
  const data = (err as { response?: { data?: ErrorBody } } | null)?.response?.data;
  const details = Array.isArray(data?.errorDetails) ? data.errorDetails.filter(isDetail) : [];
  return details.some((d) => d.code.startsWith(prefix));
}
