import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { AxiosError, type AxiosAdapter, type InternalAxiosRequestConfig } from "axios";
import { apiClient } from "./apiClient";
import { i18n } from "../i18n";

// The real apiClient and the real i18n instance; only the network adapter is replaced.
const originalAdapter = apiClient.defaults.adapter;

function refuseWith(status: number, data: unknown): AxiosAdapter {
  return (config: InternalAxiosRequestConfig) => {
    const response = { status, statusText: "", data, headers: {}, config };
    return Promise.reject(new AxiosError("Request failed", "ERR_BAD_REQUEST", config, null, response));
  };
}

interface Refusal {
  message: string;
  errors?: string[];
  fieldErrors?: Array<{ field: string; message: string }>;
}

async function refusal(status: number, data: unknown): Promise<{ thrown: AxiosError; body: Refusal }> {
  apiClient.defaults.adapter = refuseWith(status, data);
  const thrown = await apiClient.get("/anything").then(
    () => null,
    (e: unknown) => e as AxiosError
  );
  if (!thrown) throw new Error("the request should have been refused");
  return { thrown, body: thrown.response?.data as Refusal };
}

const takenBody = {
  success: false,
  message: "Already taken by Dana Lee.",
  errors: ["Already taken by Dana Lee."],
  errorDetails: [{ code: "Test.AlreadyTaken", params: { name: "Dana Lee" }, message: "Already taken by Dana Lee." }]
};

beforeEach(async () => {
  await i18n.changeLanguage("en");
});

afterEach(async () => {
  apiClient.defaults.adapter = originalAdapter;
  await i18n.changeLanguage("en");
});

describe("apiClient coded server messages", () => {
  it("shows the Arabic sentence, parameters filled in, when the language is Arabic", async () => {
    await i18n.changeLanguage("ar");

    const { body } = await refusal(400, takenBody);

    expect(body.message).toBe("تم أخذ الاختبار بالفعل بواسطة Dana Lee.");
    expect(body.errors).toEqual(["تم أخذ الاختبار بالفعل بواسطة Dana Lee."]);
  });

  it("shows the English template in English, with the same parameters", async () => {
    const { body } = await refusal(400, takenBody);

    expect(body.message).toBe("Already taken by Dana Lee.");
  });

  it("falls back to the server's English sentence for a code with no template, even in Arabic", async () => {
    await i18n.changeLanguage("ar");

    const { body } = await refusal(400, {
      success: false,
      message: "Brand new refusal.",
      errorDetails: [{ code: "Future.NoTemplateYet", message: "Brand new refusal." }]
    });

    expect(body.message).toBe("Brand new refusal.");
  });

  it("translates the field error that repeats the coded sentence", async () => {
    await i18n.changeLanguage("ar");
    const english = "A sample with this batch and control number is already registered.";

    const { body } = await refusal(400, {
      success: false,
      message: english,
      errors: [english],
      fieldErrors: [{ field: "controlNumber", message: english }],
      errorDetails: [{ code: "Sample.DuplicateBatchAndControl", message: english }]
    });

    expect(body.fieldErrors?.[0].message).toBe("توجد عينة مسجلة بالفعل بنفس رقم الدفعة ورقم الضبط.");
    expect(body.fieldErrors?.[0].field).toBe("controlNumber");
  });

  it("leaves an uncoded refusal exactly as the server sent it, in Arabic too", async () => {
    await i18n.changeLanguage("ar");
    const sent = { success: false, message: "Insufficient stock.", errors: ["Insufficient stock."] };

    const { body } = await refusal(400, structuredClone(sent));

    expect(body).toEqual(sent);
  });
});
