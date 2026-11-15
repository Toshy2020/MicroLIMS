import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { createEmptyTestFormModel } from "./testFormModel";
import { AnalystPreview, previewModel } from "./AnalystPreview";

afterEach(cleanup);

describe("AnalystPreview & previewModel", () => {
  it("orders steps by stepOrder and resolves media names and incubation windows from the unsaved model", () => {
    const model = createEmptyTestFormModel({
      lab: "micro",
      workflowType: "CountTest",
      displayName: "Total Aerobic Count",
      steps: [
        {
          stepOrder: 2,
          stepName: "Plate Count",
          isFinalStep: true,
          stepType: "PlateCount",
          targetOrganismId: null,
          stepMedia: [
            {
              mediaProductId: 12,
              mediaIncubationConditionId: 102,
              isRequired: true,
              displayOrder: 0
            }
          ],
          requiresIncubationTransfer: true,
          stage2TempMin: "20",
          stage2TempMax: "25",
          stage2IncubationMinHours: "48",
          stage2IncubationMaxHours: "72",
          phenotypicTestType: null,
          phenotypicTestTypes: []
        },
        {
          stepOrder: 1,
          stepName: "Enrichment Broth",
          isFinalStep: false,
          stepType: "BrothEnrichment",
          targetOrganismId: null,
          stepMedia: [
            {
              mediaProductId: 11,
              mediaIncubationConditionId: 101,
              isRequired: true,
              displayOrder: 0
            }
          ],
          requiresIncubationTransfer: false,
          phenotypicTestType: null,
          phenotypicTestTypes: []
        }
      ]
    });

    const preview = previewModel(model, {
      mediaProducts: [
        { id: 11, name: "Buffered Peptone Water" },
        { id: 12, name: "Tryptic Soy Agar" }
      ],
      conditions: [
        {
          id: 101,
          mediaProductId: 11,
          temperatureMin: 35,
          temperatureMax: 37,
          incubationMinHours: 18,
          incubationMaxHours: 24,
          configurationCount: 0,
          stepMediaCount: 0
        },
        {
          id: 102,
          mediaProductId: 12,
          temperatureMin: 30,
          temperatureMax: 35,
          incubationMinHours: 48,
          incubationMaxHours: 72,
          configurationCount: 0,
          stepMediaCount: 0
        }
      ]
    });

    expect(preview).toHaveLength(2);
    expect(preview[0]).toMatchObject({
      order: 1,
      name: "Enrichment Broth",
      media: ["Buffered Peptone Water"],
      incubation: { minHours: 18, maxHours: 24, tempMin: 35, tempMax: 37 }
    });
    expect(preview[0].analystEnters.length).toBeGreaterThan(0);

    expect(preview[1]).toMatchObject({
      order: 2,
      name: "Plate Count",
      media: ["Tryptic Soy Agar"],
      incubation: { minHours: 48, maxHours: 72, tempMin: 30, tempMax: 35 }
    });
    expect(preview[1].analystEnters).toEqual(
      expect.arrayContaining([
        expect.stringMatching(/Plate counts/i),
        expect.stringMatching(/Stage 2 incubator/i)
      ])
    );
  });

  it("derives analyst inputs and stages for physicochemical models", () => {
    const model = createEmptyTestFormModel({
      lab: "fp",
      workflowType: "Gravimetric",
      equationType: "GravimetricLoss",
      displayName: "Loss on Drying",
      replicateCount: "3",
      usesTare: true,
      conditionFields: "Temperature (°C),Time (h)",
      stages: [
        { role: "Bulk", sampleReplicates: "2" },
        { role: "Finished", sampleReplicates: "3" }
      ]
    });

    const preview = previewModel(model);
    expect(preview).toHaveLength(1);
    expect(preview[0].stages).toEqual(["Bulk: 2", "Finished: 3"]);
    expect(preview[0].analystEnters).toEqual(
      expect.arrayContaining([
        "Container tare weight (3 replicates)",
        "Initial & final sample weights (3 replicates)",
        "Temperature (°C)"
      ])
    );
  });

  it("renders 'Preview — configuration only' banner, StepChainStrip, and zero input controls", () => {
    const steps = previewModel(
      createEmptyTestFormModel({
        lab: "micro",
        workflowType: "Observation",
        displayName: "Salmonella",
        steps: [
          {
            stepOrder: 1,
            stepName: "Pre-enrichment",
            isFinalStep: false,
            stepType: "BrothEnrichment",
            targetOrganismId: null,
            stepMedia: [
              {
                mediaProductId: 11,
                mediaProductName: "Lactose Broth",
                mediaIncubationConditionId: "",
                incubationMinHours: 18,
                incubationMaxHours: 24,
                tempMin: 35,
                tempMax: 37,
                isRequired: true,
                displayOrder: 0
              }
            ],
            requiresIncubationTransfer: false,
            phenotypicTestType: null,
            phenotypicTestTypes: []
          },
          {
            stepOrder: 2,
            stepName: "Confirmation",
            isFinalStep: true,
            stepType: "BiochemicalTest",
            targetOrganismId: null,
            stepMedia: [],
            requiresIncubationTransfer: false,
            phenotypicTestType: null,
            phenotypicTestTypes: ["Oxidase", "Catalase"]
          }
        ]
      })
    );

    const { container } = render(
      <PinnedLightTheme>
        <AnalystPreview steps={steps} />
      </PinnedLightTheme>
    );

    const previewRegion = screen.getByLabelText("Analyst preview");
    expect(within(previewRegion).getByText("Preview — configuration only")).toBeTruthy();
    expect(within(previewRegion).getByRole("navigation", { name: "Workflow progress" })).toBeTruthy();
    expect(within(previewRegion).getByText(/1\. Pre-enrichment/)).toBeTruthy();
    expect(within(previewRegion).getByText(/Lactose Broth/)).toBeTruthy();
    expect(within(previewRegion).getByText(/18–24 h at 35–37 °C/)).toBeTruthy();
    expect(within(previewRegion).getByText(/2\. Confirmation/)).toBeTruthy();

    expect(container.querySelectorAll("input, select, textarea")).toHaveLength(0);
    expect(screen.queryAllByRole("textbox")).toHaveLength(0);
    expect(screen.queryAllByRole("combobox")).toHaveLength(0);
    expect(screen.queryAllByRole("checkbox")).toHaveLength(0);
    expect(screen.queryAllByRole("switch")).toHaveLength(0);
    expect(screen.queryAllByRole("spinbutton")).toHaveLength(0);
  });
});
