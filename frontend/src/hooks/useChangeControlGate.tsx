import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { useTranslation } from "../i18n";
import { ChangeControlPrompt } from "../components/ChangeControlPrompt";
import { changeControlGateRefusal } from "../services/changeControlGate";
import { changeControlLookupService } from "../services/changeControlLookupService";

// The person closed the prompt without choosing a change control, so nothing was saved.
export class ChangeControlPromptCancelled extends Error {
  constructor(message: string) {
    super(message);
    this.name = "ChangeControlPromptCancelled";
  }
}

interface Options {
  // The laboratory (section) that owns the record being changed; null when it is not known.
  sectionId: number | null | undefined;
  // Identifies the record being edited. The chosen number is remembered for that record only.
  recordKey: string | number | null;
}

interface PendingPrompt {
  sectionId: number | null;
  refusal: string | null;
  initialNumber: string;
  settle: (result: { number: string } | { cancelled: true }) => void;
}

interface Remembered {
  recordKey: string | number | null;
  number: string;
}

export interface ChangeControlScope {
  // The laboratory for this one save, when it is only known at save time (e.g. a test being created).
  sectionId?: number | null;
}

export interface ChangeControlGate {
  // Runs one save. `send` is called with the change control number to send as X-Change-Control (or
  // undefined). When the server refuses the save because the laboratory needs an approved change
  // control, the prompt opens and, once a number is chosen, `send` is called again - the same closure,
  // so the same body and version. Nothing is repeated without the person choosing. Any other error
  // is thrown as it came.
  run: <T>(send: (changeControl?: string) => Promise<T>, scope?: ChangeControlScope) => Promise<T>;
  // Ends the editing session: the remembered number is forgotten. Call it when the editor closes.
  reset: () => void;
  // Render this once in the screen that owns the editing session.
  prompt: ReactNode;
}

// The change-control gate (CONF-227) for one editing session of one record: asks before the first
// save when the laboratory is known to require it, opens on the server's refusal otherwise, and
// remembers the accepted number for that record until the session ends.
export function useChangeControlGate({ sectionId, recordKey }: Options): ChangeControlGate {
  const { t } = useTranslation("changeControl");
  const [pending, setPending] = useState<PendingPrompt | null>(null);
  const remembered = useRef<Remembered | null>(null);
  const settingLookup = useRef<{ sectionId: number; required: Promise<boolean> } | null>(null);
  const pendingRef = useRef<PendingPrompt | null>(null);
  const cancelledMessage = t("prompt.cancelled");

  useEffect(() => {
    // Another record is being edited: a number chosen for the previous one is not carried over.
    remembered.current = null;
  }, [recordKey]);

  useEffect(() => {
    // Leaving the screen with the prompt open ends the save.
    return () => pendingRef.current?.settle({ cancelled: true });
  }, []);

  const isRequired = useCallback((section: number | null): Promise<boolean> => {
    if (section == null) return Promise.resolve(false);
    if (settingLookup.current?.sectionId !== section) {
      // A person who cannot read the setting is simply not asked up front; the server still decides.
      const required = Promise.resolve()
        .then(() => changeControlLookupService.getSectionSetting(section, new AbortController().signal))
        .then((setting) => setting?.changeControlRequired === true)
        .catch(() => false);
      settingLookup.current = { sectionId: section, required };
    }
    return settingLookup.current.required;
  }, []);

  const ask = useCallback(
    (section: number | null, refusal: string | null, initialNumber: string): Promise<string> =>
      new Promise<string>((resolve, reject) => {
        const entry: PendingPrompt = {
          sectionId: section,
          refusal,
          initialNumber,
          settle: (result) => {
            pendingRef.current = null;
            setPending(null);
            if ("number" in result) resolve(result.number);
            else reject(new ChangeControlPromptCancelled(cancelledMessage));
          }
        };
        pendingRef.current = entry;
        setPending(entry);
      }),
    [cancelledMessage]
  );

  const run = useCallback(
    async <T,>(send: (changeControl?: string) => Promise<T>, scope?: ChangeControlScope): Promise<T> => {
      const section = scope?.sectionId !== undefined ? scope.sectionId : (sectionId ?? null);
      const known = remembered.current?.recordKey === recordKey ? remembered.current.number : undefined;
      let number = known;
      if (!number && (await isRequired(section))) number = await ask(section, null, "");

      for (;;) {
        try {
          const result = await send(number);
          if (number) remembered.current = { recordKey, number };
          return result;
        } catch (error) {
          const refusal = changeControlGateRefusal(error);
          if (refusal === null) {
            // The gate let the request through (it failed for another reason): the number was fine.
            if (number) remembered.current = { recordKey, number };
            throw error;
          }
          if (remembered.current?.number === number) remembered.current = null;
          number = await ask(section, refusal, number ?? "");
        }
      }
    },
    [ask, isRequired, recordKey, sectionId]
  );

  const reset = useCallback(() => {
    remembered.current = null;
    settingLookup.current = null;
  }, []);

  const prompt = pending ? (
    <ChangeControlPrompt
      open
      sectionId={pending.sectionId}
      refusal={pending.refusal}
      initialNumber={pending.initialNumber}
      onSubmit={(number) => pending.settle({ number })}
      onCancel={() => pending.settle({ cancelled: true })}
    />
  ) : null;

  return { run, reset, prompt };
}
