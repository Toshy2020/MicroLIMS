import { useEffect, useState } from "react";
import { Box, Paper, Select, MenuItem, TextField, Button, Typography, Autocomplete } from "@mui/material";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { SearchBar } from "../../components/SearchBar";
import { StatusBadge, CauseBadge, CategoryBadge } from "../../components/StatusBadge";
import { SampleLifecycleBadge } from "../testingWorkspace/SampleLifecycleBadge";
import { ReceiveService } from "./services/ReceiveService";
import { masterDataOptions, SAMPLED_BY_SUGGESTIONS, PRODUCTION_STAGES } from "../../services/masterDataOptions";
import { SampleRecord } from "./types/receivingTypes";
import { brandColors } from "../../theme";

const CATEGORIES = [
  { key: "product", label: "Product", apiCategory: "FinishedProduct" },
  { key: "rm", label: "Raw Material", apiCategory: "RawMaterial" },
  { key: "pm", label: "Packaging Material", apiCategory: "PackagingMaterial" },
  { key: "water", label: "Water", apiCategory: null },
  { key: "em", label: "Environmental Monitoring", apiCategory: null },
  { key: "ac", label: "After Cleaning", apiCategory: null }
];

export function ReceiveSamplePage() {
  const [category, setCategory] = useState("product");
  const [items, setItems] = useState<any[]>([]);
  const [waterPoints, setWaterPoints] = useState<any[]>([]);
  const [departments, setDepartments] = useState<any[]>([]);
  const [machines, setMachines] = useState<any[]>([]);
  const [causes, setCauses] = useState<any[]>([]);

  const [form, setForm] = useState<Record<string, any>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [records, setRecords] = useState<SampleRecord[] | null>(null);
  const [filter, setFilter] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  useEffect(() => {
    masterDataOptions.getCausesOfTesting().then(setCauses);
    masterDataOptions.getWaterSamplingPoints().then(setWaterPoints);
    masterDataOptions.getDepartments().then(setDepartments);
    masterDataOptions.getMachines().then(setMachines);
    loadRecords();
  }, []);

  useEffect(() => {
    const cat = CATEGORIES.find((c) => c.key === category);
    if (cat?.apiCategory) masterDataOptions.getItems(cat.apiCategory).then(setItems);
    setForm({});
    setMessage(null);
  }, [category]);

  const loadRecords = () => ReceiveService.getRecords().then(setRecords);
  const setField = (key: string, value: any) => setForm((f) => ({ ...f, [key]: value }));

  const handleSave = async () => {
    setMessage(null);
    try {
      if (category === "product" || category === "rm" || category === "pm") {
        await ReceiveService.receiveItemBased({
          itemId: form.itemId, causeOfTestingId: form.causeOfTestingId, sampleQuantity: form.sampleQuantity ?? "",
          sampledBy: form.sampledBy ?? "", batchNumber: form.batchNumber ?? "", controlNumber: form.controlNumber ?? "",
          mfgDate: form.mfgDate || null, expDate: form.expDate || null, productionStage: category === "product" ? form.productionStage : null
        });
      } else if (category === "water") {
        await ReceiveService.receiveWater({
          waterSamplingPointId: form.waterSamplingPointId, causeOfTestingId: form.causeOfTestingId,
          sampleQuantity: form.sampleQuantity ?? "", sampledBy: form.sampledBy ?? "", controlNumber: form.controlNumber ?? ""
        });
      } else if (category === "em") {
        await ReceiveService.receiveEM({
          departmentId: form.departmentId, causeOfTestingId: form.causeOfTestingId,
          sampledBy: form.sampledBy ?? "", controlNumber: form.controlNumber ?? ""
        });
      } else if (category === "ac") {
        await ReceiveService.receiveAfterCleaning({
          machineId: form.machineId, causeOfTestingId: form.causeOfTestingId,
          sampledBy: form.sampledBy ?? "", controlNumber: form.controlNumber ?? ""
        });
      }
      setMessage({ text: "Sample received successfully.", ok: true });
      setForm({});
      loadRecords();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Error receiving sample.", ok: false });
    }
  };

  const filteredRecords = records?.filter((r) => {
    if (filter && !Object.values(r).some((v) => String(v).toLowerCase().includes(filter.toLowerCase()))) return false;
    const receivedDate = r.receivedAt.slice(0, 10); // YYYY-MM-DD, matches <input type="date">
    if (fromDate && receivedDate < fromDate) return false;
    if (toDate && receivedDate > toDate) return false;
    return true;
  });

  const itemBased = category === "product" || category === "rm" || category === "pm";

  return (
    <>
      <PageHeader title="Sample Receiving" subtitle="Choose an item type, then log incoming samples for that category." />

      <SectionTitle>1. Choose Item Type</SectionTitle>
      <Box sx={{ display: "flex", gap: 1, mb: 2, flexWrap: "wrap" }}>
        {CATEGORIES.map((c) => (
          <Box
            key={c.key}
            onClick={() => setCategory(c.key)}
            sx={{
              px: 2, py: 0.75, borderRadius: 5, fontSize: 13, fontWeight: 600, cursor: "pointer",
              bgcolor: category === c.key ? brandColors.sectionTitle : "#fff",
              color: category === c.key ? "#fff" : "text.secondary",
              border: `1px solid ${category === c.key ? brandColors.sectionTitle : "#e5e7eb"}`
            }}
          >
            {c.label}
          </Box>
        ))}
      </Box>

      <SectionTitle>2. Entry</SectionTitle>
      {message && (
        <Typography sx={{ mb: 1.5, fontSize: 13, color: message.ok ? brandColors.ok : brandColors.err }}>{message.text}</Typography>
      )}

      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2 }}>
          {itemBased && (
            <>
              <Select displayEmpty value={form.itemId ?? ""} onChange={(e) => setField("itemId", e.target.value)}>
                <MenuItem value=""><em>Select item</em></MenuItem>
                {items.map((i) => <MenuItem key={i.id} value={i.id}>{i.name}</MenuItem>)}
              </Select>
              {category === "product" && (
                <Select displayEmpty value={form.productionStage ?? ""} onChange={(e) => setField("productionStage", e.target.value)}>
                  <MenuItem value=""><em>Production Stage</em></MenuItem>
                  {PRODUCTION_STAGES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
                </Select>
              )}
            </>
          )}

          {category === "water" && (
            <Select displayEmpty value={form.waterSamplingPointId ?? ""} onChange={(e) => setField("waterSamplingPointId", e.target.value)}>
              <MenuItem value=""><em>Sampling point</em></MenuItem>
              {waterPoints.map((p) => <MenuItem key={p.id} value={p.id}>{p.code} — {p.location}</MenuItem>)}
            </Select>
          )}

          {category === "em" && (
            <Select displayEmpty value={form.departmentId ?? ""} onChange={(e) => setField("departmentId", e.target.value)}>
              <MenuItem value=""><em>Department</em></MenuItem>
              {departments.map((d) => <MenuItem key={d.id} value={d.id}>{d.name}</MenuItem>)}
            </Select>
          )}

          {category === "ac" && (
            <Select displayEmpty value={form.machineId ?? ""} onChange={(e) => setField("machineId", e.target.value)}>
              <MenuItem value=""><em>Machine</em></MenuItem>
              {machines.map((m) => <MenuItem key={m.id} value={m.id}>{m.name}</MenuItem>)}
            </Select>
          )}

          <Select displayEmpty value={form.causeOfTestingId ?? ""} onChange={(e) => setField("causeOfTestingId", e.target.value)}>
            <MenuItem value=""><em>Cause of Testing</em></MenuItem>
            {causes.map((c) => <MenuItem key={c.id} value={c.id}>{c.name}</MenuItem>)}
          </Select>

          {category !== "em" && category !== "ac" && (
            <TextField placeholder="Sample Quantity" value={form.sampleQuantity ?? ""} onChange={(e) => setField("sampleQuantity", e.target.value)} />
          )}

          <Autocomplete
            freeSolo
            options={SAMPLED_BY_SUGGESTIONS}
            inputValue={form.sampledBy ?? ""}
            onInputChange={(_, v) => setField("sampledBy", v)}
            renderInput={(params) => <TextField {...params} placeholder="Sampled By" />}
          />

          {itemBased && (
            <TextField placeholder="Batch Number" value={form.batchNumber ?? ""} onChange={(e) => setField("batchNumber", e.target.value)} />
          )}
          <TextField placeholder="Control Number" value={form.controlNumber ?? ""} onChange={(e) => setField("controlNumber", e.target.value)} />

          {itemBased && (
            <>
              <TextField type="date" label="Mfg Date" InputLabelProps={{ shrink: true }} value={form.mfgDate ?? ""} onChange={(e) => setField("mfgDate", e.target.value)} />
              <TextField type="date" label="Exp Date" InputLabelProps={{ shrink: true }} value={form.expDate ?? ""} onChange={(e) => setField("expDate", e.target.value)} />
            </>
          )}
        </Box>

        {(category === "em" || category === "ac") && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
            Rooms/parts and test types are selected in a separate Preparation step after receiving.
          </Typography>
        )}

        <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}>
          <Button variant="contained" onClick={handleSave}>Save Received Sample</Button>
        </Box>
      </Paper>

      <SectionTitle tabs={[{ label: "Refresh", onClick: loadRecords }]}>Records</SectionTitle>
      <SearchBar value={filter} onChange={setFilter} placeholder="Filter records by any field..." />
      <Box sx={{ display: "flex", gap: 1.5, mb: 2.25, mt: -1.25 }}>
        <TextField size="small" type="date" label="From" InputLabelProps={{ shrink: true }} value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        <TextField size="small" type="date" label="To" InputLabelProps={{ shrink: true }} value={toDate} onChange={(e) => setToDate(e.target.value)} />
      </Box>

      {!filteredRecords ? (
        <Typography color="text.secondary">Loading...</Typography>
      ) : filteredRecords.length === 0 ? (
        <Typography sx={{ color: "#9ca3af", fontSize: 13, p: 2 }}>No samples recorded yet.</Typography>
      ) : (
        filteredRecords.map((r) => (
          <Paper key={r.sampleId} sx={{ p: 2, mb: 1.25, display: "grid", gridTemplateColumns: "60px 1.6fr 1.6fr auto", gap: 2 }}>
            <Typography sx={{ color: "text.secondary", fontWeight: 600, fontSize: 13 }}>#{r.sampleId}</Typography>
            <Box>
              <Typography sx={{ fontWeight: 600, fontSize: 13 }}>{r.displayName}{r.batchNumber ? ` — ${r.batchNumber}` : ""}</Typography>
              <Box sx={{ display: "flex", gap: 0.75, mt: 0.75 }}>
                <CategoryBadge category={r.category} />
                <CauseBadge label={r.causeOfTesting} />
                {r.preparationStatus === "NeedsPreparation" && <StatusBadge status="Needs Preparation" />}
                <SampleLifecycleBadge status={r.status} role={null} interactive={false} />
              </Box>
              <Typography sx={{ fontSize: 11, color: "#9ca3af", mt: 0.5 }}>{r.referenceNumber}</Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 11, color: "#9ca3af", mb: 0.5 }}>Test Status</Typography>
              {r.assignedTests.length === 0 ? (
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>—</Typography>
              ) : (
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                  {r.assignedTests.map((t) => (
                    <Box key={t.testOrderId} sx={{ display: "flex", alignItems: "center", gap: 0.5, bgcolor: "#f3e8ff", borderRadius: 1.5, px: 0.75, py: 0.25 }}>
                      <Typography sx={{ fontSize: 11, fontWeight: 600 }}>{t.testCode}</Typography>
                      <StatusBadge status={t.status} />
                    </Box>
                  ))}
                </Box>
              )}
            </Box>
            <Typography sx={{ fontSize: 11, color: "#9ca3af", textAlign: "right" }}>{new Date(r.receivedAt).toLocaleString()}</Typography>
          </Paper>
        ))
      )}
    </>
  );
}
