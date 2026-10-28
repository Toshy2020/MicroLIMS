import { useEffect, useState } from "react";
import { Autocomplete, Box, FormControl, FormHelperText, InputLabel, MenuItem, Select, TextField } from "@mui/material";
import { FormDialog } from "../../../components/lab";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";
import type { OrganismOption } from "../../../hooks/useOrganisms";
import {
  ReferenceOrganismService,
  ReferenceOrganism,
  CultureCollection,
  CULTURE_COLLECTIONS,
  REFERENCE_ORGANISM_CODE_PREFIX
} from "./services/ReferenceOrganismService";

interface ReferenceOrganismDialogProps {
  open: boolean;
  // Null to add a strain.
  strain: ReferenceOrganism | null;
  species: OrganismOption[];
  sections: LaboratorySection[];
  onClose: () => void;
  onSaved: (message: string) => void;
}

// Add / edit a reference strain. Once a stock lot or cryovial uses it (hasStock) the server freezes
// the code, species, collection and strain number, so they are read-only here.
export function ReferenceOrganismDialog({ open, strain, species, sections, onClose, onSaved }: ReferenceOrganismDialogProps) {
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [code, setCode] = useState("");
  const [organismId, setOrganismId] = useState<number | null>(null);
  const [collection, setCollection] = useState<CultureCollection>("ATCC");
  const [strainNumber, setStrainNumber] = useState("");
  const [sectionId, setSectionId] = useState<number | "">("");
  const [errors, setErrors] = useState<Partial<Record<"code" | "species" | "section", string>>>({});
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    getMySections().then((s) => setMySections(Array.isArray(s) ? s : [])).catch(() => setMySections([]));
  }, [open]);

  useEffect(() => {
    if (!open) return;
    setCode(strain?.code ?? "");
    setOrganismId(strain?.organismId ?? null);
    setCollection(strain?.cultureCollection ?? "ATCC");
    setStrainNumber(strain?.strainNumber ?? "");
    setSectionId(strain?.ownerSectionId ?? "");
    setErrors({});
    setError(null);
  }, [open, strain]);

  // A single-lab user's strain goes to that lab without asking.
  useEffect(() => {
    if (open && !strain && mySections.length === 1) setSectionId(mySections[0].sectionId);
  }, [open, strain, mySections]);

  const frozen = Boolean(strain?.hasStock);
  const selectedSpecies = species.find((o) => o.id === organismId) ?? null;
  const labOptions = !strain && mySections.length > 1 ? mySections : sections;

  const handleSave = async () => {
    const trimmedCode = code.trim().toUpperCase();
    const next: typeof errors = {};
    if (!trimmedCode) next.code = "Code is required.";
    else if (!strain && !trimmedCode.startsWith(REFERENCE_ORGANISM_CODE_PREFIX)) next.code = `Code must start with "${REFERENCE_ORGANISM_CODE_PREFIX}".`;
    if (!organismId) next.species = "Species is required.";
    if (!strain && mySections.length > 1 && !sectionId) next.section = "Owner lab is required.";
    setErrors(next);
    if (Object.keys(next).length > 0) return;

    const payload = {
      code: trimmedCode,
      organismId: organismId!,
      cultureCollection: collection,
      strainNumber: strainNumber.trim() || null,
      ownerSectionId: !strain && sectionId ? Number(sectionId) : null
    };
    setSaving(true);
    setError(null);
    try {
      if (strain) await ReferenceOrganismService.update(strain.id, payload, strain.version);
      else await ReferenceOrganismService.create(payload);
      onSaved(`Reference organism "${trimmedCode}" ${strain ? "updated" : "created"}.`);
      onClose();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not save the reference organism.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <FormDialog
      open={open}
      title={strain ? `Edit Reference Organism: ${strain.code}` : "Add Reference Organism"}
      onClose={onClose}
      onSubmit={handleSave}
      submitLabel={saving ? "Saving..." : strain ? "Save Changes" : "Create Strain"}
      submitting={saving}
      error={error}
      maxWidth="sm"
    >
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
        <TextField
          label="Code"
          value={code}
          onChange={(e) => { setErrors((x) => ({ ...x, code: undefined })); setCode(e.target.value.toUpperCase()); }}
          required
          fullWidth
          size="small"
          placeholder={`e.g. ${REFERENCE_ORGANISM_CODE_PREFIX}001`}
          disabled={frozen}
          error={!!errors.code}
          helperText={
            errors.code ??
            (frozen
              ? "This strain has stock; use Change code to change it."
              : `Must start with "${REFERENCE_ORGANISM_CODE_PREFIX}" and be unique across all labs`)
          }
        />
        <Autocomplete
          size="small"
          options={species}
          value={selectedSpecies}
          disabled={frozen}
          getOptionLabel={(o) => o.scientificName}
          isOptionEqualToValue={(o, v) => o.id === v.id}
          onChange={(_e, v) => { setErrors((x) => ({ ...x, species: undefined })); setOrganismId(v?.id ?? null); }}
          renderInput={(params) => (
            <TextField {...params} label="Species" required error={!!errors.species} helperText={errors.species ?? (frozen ? "Frozen: the strain has stock." : undefined)} />
          )}
        />
        <FormControl size="small" fullWidth disabled={frozen}>
          <InputLabel id="reforg-collection-label">Culture collection</InputLabel>
          <Select labelId="reforg-collection-label" label="Culture collection" value={collection} onChange={(e) => setCollection(e.target.value as CultureCollection)}>
            {CULTURE_COLLECTIONS.map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
          </Select>
        </FormControl>
        <TextField
          label="Strain number"
          value={strainNumber}
          onChange={(e) => setStrainNumber(e.target.value)}
          fullWidth
          size="small"
          placeholder="e.g. 6538"
          disabled={frozen}
          helperText={collection === "Other" ? "Optional for in-house or other sources" : undefined}
        />
        {(strain || mySections.length > 1) && (
          <FormControl fullWidth size="small" required={!strain} error={!!errors.section} disabled={Boolean(strain)}>
            <InputLabel id="reforg-section-label">Owner Lab</InputLabel>
            <Select
              labelId="reforg-section-label"
              label="Owner Lab"
              value={sectionId}
              onChange={(e) => { setErrors((x) => ({ ...x, section: undefined })); setSectionId(e.target.value as number); }}
            >
              {labOptions.map((s) => <MenuItem key={s.sectionId} value={s.sectionId}>{s.sectionName}</MenuItem>)}
            </Select>
            {errors.section && <FormHelperText>{errors.section}</FormHelperText>}
          </FormControl>
        )}
      </Box>
    </FormDialog>
  );
}
