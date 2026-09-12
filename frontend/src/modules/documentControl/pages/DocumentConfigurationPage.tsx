import { monospaceFontFamily } from "../../../theme/palette";
import { compactChipSx } from "../documentControlStyles";
import { useState, useEffect } from "react";
import {
  Box,
  Typography,
  Paper,
  Tabs,
  Tab,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Switch,
  Chip,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Alert,
  CircularProgress,
  IconButton,
  Tooltip,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { toast } from "sonner";

import { PageHeader } from "../../../components/PageHeader";
import { tableHeadSx } from "../../../theme";
import { useAuth } from "../../../contexts/AuthContext";
import { documentControlService } from "../services/documentControlService";
import type {
  DocumentTypeDto,
  DocumentDepartmentDto,
  DocumentNumberingConfigDto,
  ConfigurationSettingDto
} from "../types/documentControlTypes";

export function DocumentConfigurationPage() {
  const theme = useTheme();
  const { role } = useAuth();
  const isAdmin = role === "SystemAdministrator";

  const [currentTab, setCurrentTab] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Config data
  const [types, setTypes] = useState<DocumentTypeDto[]>([]);
  const [departments, setDepartments] = useState<DocumentDepartmentDto[]>([]);
  const [numbering, setNumbering] = useState<DocumentNumberingConfigDto | null>(null);
  const [settings, setSettings] = useState<ConfigurationSettingDto[]>([]);

  // Dialog states
  const [typeDialogOpen, setTypeDialogOpen] = useState(false);
  const [editingType, setEditingType] = useState<DocumentTypeDto | null>(null);
  const [typeForm, setTypeForm] = useState({ code: "", name: "", cycle: 24, isActive: true });

  const [deptDialogOpen, setDeptDialogOpen] = useState(false);
  const [editingDept, setEditingDept] = useState<DocumentDepartmentDto | null>(null);
  const [deptForm, setDeptForm] = useState({ code: "", name: "", isActive: true });

  const [sectionDialogOpen, setSectionDialogOpen] = useState(false);
  const [targetDeptId, setTargetDeptId] = useState<number>(0);
  const [editingSectionId, setEditingSectionId] = useState<number | null>(null);
  const [sectionForm, setSectionForm] = useState({ name: "", isActive: true });

  const [numberingForm, setNumberingForm] = useState({ prefix: "DOC-", format: "0000000", isEnabled: true });
  const [savingNumbering, setSavingNumbering] = useState(false);

  const [settingDialogOpen, setSettingDialogOpen] = useState(false);
  const [editingSetting, setEditingSetting] = useState<ConfigurationSettingDto | null>(null);
  const [settingValue, setSettingValue] = useState("");

  const loadAll = async () => {
    setLoading(true);
    setError(null);
    try {
      const [tRes, dRes, nRes, sRes] = await Promise.all([
        documentControlService.getTypes(true),
        documentControlService.getDepartments(true),
        documentControlService.getNumberingConfig(),
        documentControlService.getSettings()
      ]);
      setTypes(tRes);
      setDepartments(dRes);
      setNumbering(nRes);
      setNumberingForm({
        prefix: nRes.prefix,
        format: nRes.numberFormat,
        isEnabled: nRes.isEnabled
      });
      setSettings(sRes);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to load configuration.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAll();
  }, []);

  if (!isAdmin) {
    return (
      <Box sx={{ p: 4, maxWidth: 800, mx: "auto" }}>
        <Alert severity="error">
          <strong>Access Denied:</strong> Only System Administrators can access and modify Document Control module configuration.
        </Alert>
      </Box>
    );
  }

  // Type actions
  const handleSaveType = async () => {
    try {
      if (editingType) {
        await documentControlService.updateType(editingType.id, {
          name: typeForm.name,
          defaultReviewCycleMonths: Number(typeForm.cycle),
          isActive: typeForm.isActive
        });
      } else {
        await documentControlService.createType({
          code: typeForm.code,
          name: typeForm.name,
          defaultReviewCycleMonths: Number(typeForm.cycle)
        });
      }
      setTypeDialogOpen(false);
      loadAll();
      toast.success("Document Type saved successfully.");
    } catch (err: any) {
      toast.error("Failed to save Document Type: " + (err.response?.data?.message || err.message));
    }
  };

  // Department actions
  const handleSaveDept = async () => {
    try {
      if (editingDept) {
        await documentControlService.updateDepartment(editingDept.id, {
          name: deptForm.name,
          isActive: deptForm.isActive
        });
      } else {
        await documentControlService.createDepartment({
          code: deptForm.code,
          name: deptForm.name
        });
      }
      setDeptDialogOpen(false);
      loadAll();
      toast.success("Department saved successfully.");
    } catch (err: any) {
      toast.error("Failed to save Department: " + (err.response?.data?.message || err.message));
    }
  };

  // Section actions
  const handleSaveSection = async () => {
    try {
      if (editingSectionId) {
        await documentControlService.updateSection(editingSectionId, {
          name: sectionForm.name,
          isActive: sectionForm.isActive
        });
      } else {
        await documentControlService.createSection({
          departmentId: targetDeptId,
          name: sectionForm.name
        });
      }
      setSectionDialogOpen(false);
      loadAll();
      toast.success("Section saved successfully.");
    } catch (err: any) {
      toast.error("Failed to save Section: " + (err.response?.data?.message || err.message));
    }
  };

  // Numbering actions
  const handleSaveNumbering = async () => {
    setSavingNumbering(true);
    try {
      const updated = await documentControlService.updateNumberingConfig({
        prefix: numberingForm.prefix,
        numberFormat: numberingForm.format,
        isEnabled: numberingForm.isEnabled
      });
      setNumbering(updated);
      toast.success("Numbering configuration updated successfully.");
    } catch (err: any) {
      toast.error("Failed to update numbering: " + (err.response?.data?.message || err.message));
    } finally {
      setSavingNumbering(false);
    }
  };

  // Setting actions
  const handleSaveSetting = async () => {
    if (!editingSetting) return;
    try {
      await documentControlService.updateSetting(editingSetting.settingKey, settingValue);
      setSettingDialogOpen(false);
      loadAll();
      toast.success("Setting updated successfully.");
    } catch (err: any) {
      toast.error("Failed to update setting: " + (err.response?.data?.message || err.message));
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
      <PageHeader
        title="Document Control Configuration"
        subtitle="System Master Data, Numbering Schemes, Governance Rules, and Module Settings"
      />

      {error && <Alert severity="error" sx={{ mb: 3 }}>{error}</Alert>}

      <Paper sx={{ mb: 3 }}>
        <Tabs
          value={currentTab}
          onChange={(_, val) => setCurrentTab(val)}
          indicatorColor="primary"
          textColor="primary"
          sx={{ borderBottom: 1, borderColor: "divider", px: 2 }}
        >
          <Tab label={`Document Types (${types.length})`} />
          <Tab label={`Departments & Sections (${departments.length})`} />
          <Tab label="Numbering Configuration" />
          <Tab label={`Module Settings (${settings.length})`} />
        </Tabs>

        {loading ? (
          <Box sx={{ p: 6, display: "flex", justifyContent: "center" }}>
            <CircularProgress />
          </Box>
        ) : (
          <>
            {/* Tab 0: Document Types */}
            {currentTab === 0 && (
              <Box sx={{ p: 3 }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
                  <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                    Document Types Master
                  </Typography>
                  <Button
                    size="small"
                    variant="contained"
                    startIcon={<AddIcon />}
                    onClick={() => {
                      setEditingType(null);
                      setTypeForm({ code: "", name: "", cycle: 24, isActive: true });
                      setTypeDialogOpen(true);
                    }}
                  >
                    Add Document Type
                  </Button>
                </Box>

                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead sx={tableHeadSx(theme)}>
                      <TableRow>
                        <TableCell sx={{ fontWeight: 700 }}>Code</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Name</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Default Review Cycle</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
                        <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>Actions</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {types.map((t) => (
                        <TableRow key={t.id} hover sx={{ opacity: t.isActive ? 1 : 0.6 }}>
                          <TableCell sx={{ fontWeight: 700 }}>{t.code}</TableCell>
                          <TableCell>{t.name}</TableCell>
                          <TableCell>{t.defaultReviewCycleMonths} Months</TableCell>
                          <TableCell>
                            {t.isActive ? (
                              <Chip label="ACTIVE" size="small" color="success" sx={compactChipSx} />
                            ) : (
                              <Chip label="DEACTIVATED" size="small" color="default" sx={compactChipSx} />
                            )}
                          </TableCell>
                          <TableCell align="right">
                            <Tooltip title="Edit Type">
                              <IconButton aria-label={`Edit document type ${t.code}`}
                                size="small"
                                onClick={() => {
                                  setEditingType(t);
                                  setTypeForm({ code: t.code, name: t.name, cycle: t.defaultReviewCycleMonths, isActive: t.isActive });
                                  setTypeDialogOpen(true);
                                }}
                              >
                                <EditOutlinedIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </Box>
            )}

            {/* Tab 1: Departments & Sections */}
            {currentTab === 1 && (
              <Box sx={{ p: 3 }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
                  <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                    Departments and Laboratory Sections
                  </Typography>
                  <Button
                    size="small"
                    variant="contained"
                    startIcon={<AddIcon />}
                    onClick={() => {
                      setEditingDept(null);
                      setDeptForm({ code: "", name: "", isActive: true });
                      setDeptDialogOpen(true);
                    }}
                  >
                    Add Department
                  </Button>
                </Box>

                <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
                  {departments.map((dept) => (
                    <Paper key={dept.id} variant="outlined" sx={{ p: 2, opacity: dept.isActive ? 1 : 0.6 }}>
                      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
                          <Typography variant="subtitle1" sx={{ fontWeight: 700, color: "primary.main" }}>
                            {dept.code} — {dept.name}
                          </Typography>
                          {dept.isActive ? (
                            <Chip label="ACTIVE" size="small" color="success" sx={compactChipSx} />
                          ) : (
                            <Chip label="DEACTIVATED" size="small" color="default" sx={compactChipSx} />
                          )}
                        </Box>

                        <Box sx={{ display: "flex", gap: 1 }}>
                          <Button
                            size="small"
                            variant="outlined"
                            startIcon={<AddIcon />}
                            onClick={() => {
                              setTargetDeptId(dept.id);
                              setEditingSectionId(null);
                              setSectionForm({ name: "", isActive: true });
                              setSectionDialogOpen(true);
                            }}
                          >
                            Add Section
                          </Button>
                          <IconButton aria-label={`Edit department ${dept.code}`}
                            size="small"
                            onClick={() => {
                              setEditingDept(dept);
                              setDeptForm({ code: dept.code, name: dept.name, isActive: dept.isActive });
                              setDeptDialogOpen(true);
                            }}
                          >
                            <EditOutlinedIcon fontSize="small" />
                          </IconButton>
                        </Box>
                      </Box>

                      {/* Sections Table */}
                      <TableContainer component={Paper} variant="outlined">
                        <Table size="small">
                          <TableHead sx={tableHeadSx(theme)}>
                            <TableRow>
                              <TableCell sx={{ fontWeight: 700 }}>Section Name</TableCell>
                              <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
                              <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>Action</TableCell>
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {dept.sections.map((sec) => (
                              <TableRow key={sec.id} hover sx={{ opacity: sec.isActive ? 1 : 0.6 }}>
                                <TableCell>{sec.name}</TableCell>
                                <TableCell>
                                  {sec.isActive ? (
                                    <Chip label="ACTIVE" size="small" color="success" sx={compactChipSx} />
                                  ) : (
                                    <Chip label="DEACTIVATED" size="small" color="default" sx={compactChipSx} />
                                  )}
                                </TableCell>
                                <TableCell align="right">
                                  <IconButton aria-label={`Edit section ${sec.name}`}
                                    size="small"
                                    onClick={() => {
                                      setTargetDeptId(dept.id);
                                      setEditingSectionId(sec.id);
                                      setSectionForm({ name: sec.name, isActive: sec.isActive });
                                      setSectionDialogOpen(true);
                                    }}
                                  >
                                    <EditOutlinedIcon fontSize="small" />
                                  </IconButton>
                                </TableCell>
                              </TableRow>
                            ))}
                            {dept.sections.length === 0 && (
                              <TableRow>
                                <TableCell colSpan={3} align="center" sx={{ py: 1.5, color: "text.secondary" }}>
                                  No sections registered under this department.
                                </TableCell>
                              </TableRow>
                            )}
                          </TableBody>
                        </Table>
                      </TableContainer>
                    </Paper>
                  ))}
                </Box>
              </Box>
            )}

            {/* Tab 2: Numbering Configuration */}
            {currentTab === 2 && numbering && (
              <Box sx={{ p: 3, maxWidth: 650 }}>
                <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
                  MicroLIMS Document ID Numbering Scheme
                </Typography>
                <Alert severity="info" sx={{ mb: 3 }}>
                  This configuration formats the system-assigned <strong>MicroLIMS Document ID</strong> (e.g. <code>DOC-0000001</code>). It does not replace or modify company-specific document codes. Sequence drawing is strictly managed by PostgreSQL sequences and cannot be reset through the application to protect data integrity.
                </Alert>

                <Paper variant="outlined" sx={{ p: 2.5, mb: 3 }}>
                  <Typography
                    variant="caption"
                    sx={{
                      color: "text.secondary",
                      fontWeight: 700,
                      textTransform: "uppercase"
                    }}>
                    Sample Generated Document ID
                  </Typography>
                  <Typography variant="h5" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700, color: "primary.main", my: 1 }}>
                    {numbering.sampleNextId}
                  </Typography>
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>
                    Last modified by: {numbering.modifiedByUserName} on {new Date(numbering.modifiedAt).toLocaleString()}
                  </Typography>
                </Paper>

                <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
                  <TextField
                    label="Prefix *"
                    value={numberingForm.prefix}
                    onChange={(e) => setNumberingForm({ ...numberingForm, prefix: e.target.value })}
                    helperText="E.g. DOC-, SOP-, QA-"
                  />

                  <TextField
                    label="Number Format (Zero-Padded Pattern) *"
                    value={numberingForm.format}
                    onChange={(e) => setNumberingForm({ ...numberingForm, format: e.target.value })}
                    helperText="E.g. 0000000 (7 digits), 00000 (5 digits)"
                  />

                  <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", p: 1.5, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50", borderRadius: 1 }}>
                    <Box>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>Enable Automated Sequence Numbering</Typography>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>Active numbering profile</Typography>
                    </Box>
                    <Switch
                      checked={numberingForm.isEnabled}
                      onChange={(e) => setNumberingForm({ ...numberingForm, isEnabled: e.target.checked })}
                    />
                  </Box>

                  <Button
                    variant="contained"
                    onClick={handleSaveNumbering}
                    disabled={savingNumbering || !numberingForm.prefix.trim() || !numberingForm.format.trim()}
                  >
                    {savingNumbering ? "Updating Scheme..." : "Save Numbering Configuration"}
                  </Button>
                </Box>
              </Box>
            )}

            {/* Tab 3: Module Settings */}
            {currentTab === 3 && (
              <Box sx={{ p: 3 }}>
                <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
                  Document Control Module Governance Settings
                </Typography>

                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead sx={tableHeadSx(theme)}>
                      <TableRow>
                        <TableCell sx={{ fontWeight: 700 }}>Group</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Setting Key</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Value</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Data Type</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Modified By</TableCell>
                        <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>Action</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {settings.map((s) => (
                        <TableRow key={s.id} hover>
                          <TableCell sx={{ fontWeight: 600 }}>{s.settingGroup}</TableCell>
                          <TableCell sx={{ fontFamily: monospaceFontFamily }}>{s.settingKey}</TableCell>
                          <TableCell>
                            <Chip label={s.settingValue} size="small" variant="outlined" />
                          </TableCell>
                          <TableCell>{s.dataType}</TableCell>
                          <TableCell>
                            <Typography variant="caption" sx={{
                              display: "block"
                            }}>{s.modifiedByUserName || "System"}</Typography>
                            <Typography variant="caption" sx={{
                              color: "text.secondary"
                            }}>{new Date(s.modifiedAt).toLocaleDateString()}</Typography>
                          </TableCell>
                          <TableCell align="right">
                            <IconButton aria-label={`Edit setting ${s.settingKey}`}
                              size="small"
                              onClick={() => {
                                setEditingSetting(s);
                                setSettingValue(s.settingValue);
                                setSettingDialogOpen(true);
                              }}
                            >
                              <EditOutlinedIcon fontSize="small" />
                            </IconButton>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </Box>
            )}
          </>
        )}
      </Paper>

      {/* Document Type Dialog */}
      <Dialog open={typeDialogOpen} onClose={() => setTypeDialogOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{editingType ? "Edit Document Type" : "Add Document Type"}</DialogTitle>
        <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {!editingType && (
            <TextField
              label="Type Code *"
              value={typeForm.code}
              onChange={(e) => setTypeForm({ ...typeForm, code: e.target.value })}
              placeholder="e.g. SOP, POL, VAL"
            />
          )}
          <TextField
            label="Type Name *"
            value={typeForm.name}
            onChange={(e) => setTypeForm({ ...typeForm, name: e.target.value })}
            placeholder="e.g. Standard Operating Procedure"
          />
          <TextField
            label="Default Review Cycle (Months) *"
            type="number"
            value={typeForm.cycle}
            onChange={(e) => setTypeForm({ ...typeForm, cycle: Number(e.target.value) })}
          />
          {editingType && (
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <Typography variant="body2">Active Status</Typography>
              <Switch
                checked={typeForm.isActive}
                onChange={(e) => setTypeForm({ ...typeForm, isActive: e.target.checked })}
              />
            </Box>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setTypeDialogOpen(false)} color="inherit">Cancel</Button>
          <Button onClick={handleSaveType} variant="contained">Save</Button>
        </DialogActions>
      </Dialog>

      {/* Department Dialog */}
      <Dialog open={deptDialogOpen} onClose={() => setDeptDialogOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{editingDept ? "Edit Department" : "Add Department"}</DialogTitle>
        <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {!editingDept && (
            <TextField
              label="Department Code *"
              value={deptForm.code}
              onChange={(e) => setDeptForm({ ...deptForm, code: e.target.value })}
              placeholder="e.g. QC, QA, PROD"
            />
          )}
          <TextField
            label="Department Name *"
            value={deptForm.name}
            onChange={(e) => setDeptForm({ ...deptForm, name: e.target.value })}
            placeholder="e.g. Quality Control"
          />
          {editingDept && (
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <Typography variant="body2">Active Status</Typography>
              <Switch
                checked={deptForm.isActive}
                onChange={(e) => setDeptForm({ ...deptForm, isActive: e.target.checked })}
              />
            </Box>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setDeptDialogOpen(false)} color="inherit">Cancel</Button>
          <Button onClick={handleSaveDept} variant="contained">Save</Button>
        </DialogActions>
      </Dialog>

      {/* Section Dialog */}
      <Dialog open={sectionDialogOpen} onClose={() => setSectionDialogOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{editingSectionId ? "Edit Section" : "Add Section"}</DialogTitle>
        <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <TextField
            label="Section Name *"
            value={sectionForm.name}
            onChange={(e) => setSectionForm({ ...sectionForm, name: e.target.value })}
            placeholder="e.g. Sterility Testing Lab"
          />
          {editingSectionId && (
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <Typography variant="body2">Active Status</Typography>
              <Switch
                checked={sectionForm.isActive}
                onChange={(e) => setSectionForm({ ...sectionForm, isActive: e.target.checked })}
              />
            </Box>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setSectionDialogOpen(false)} color="inherit">Cancel</Button>
          <Button onClick={handleSaveSection} variant="contained">Save</Button>
        </DialogActions>
      </Dialog>

      {/* Setting Dialog */}
      <Dialog open={settingDialogOpen} onClose={() => setSettingDialogOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Edit Configuration Setting</DialogTitle>
        <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <Typography variant="caption" sx={{
            color: "text.secondary"
          }}>
            Key: <strong>{editingSetting?.settingKey}</strong>
          </Typography>
          <TextField
            label="Setting Value *"
            value={settingValue}
            onChange={(e) => setSettingValue(e.target.value)}
            fullWidth
          />
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setSettingDialogOpen(false)} color="inherit">Cancel</Button>
          <Button onClick={handleSaveSetting} variant="contained">Save Setting</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
