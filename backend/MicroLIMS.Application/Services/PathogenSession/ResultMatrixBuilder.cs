using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public sealed record ResultMatrix(
    List<MatrixCellResultDto> Matrix,
    List<MissingResultDto> Missing,
    int Completed,
    int Available,
    int Locked);

// Builds the location x test result grid of a pathogen testing session.
// Pure: everything it reads is passed in.
public static class ResultMatrixBuilder
{
    public static ResultMatrix Build(
        IReadOnlyList<SessionLocationDto> groupedLocations,
        IReadOnlyList<SessionAssignedTestDto> assignedTestDtos,
        Sample sample,
        IReadOnlyDictionary<(int SampleLocationId, int TestOrderId), LocationPathogenObservation> primaryObsMap,
        Func<int?, string> UserName)
    {
        // Build Result Matrix with Tri-State Cell Model & Confirmation Details
        var matrix = new List<MatrixCellResultDto>();
        var missingResults = new List<MissingResultDto>();
        int completedCount = 0;
        int availableCount = 0;
        int lockedCount = 0;

        foreach (var loc in groupedLocations)
        {
            foreach (var test in assignedTestDtos)
            {
                var isQuantitative = test.WorkflowType.Equals("CountTest", StringComparison.OrdinalIgnoreCase) ||
                                     test.TestCode.Contains("TAMC", StringComparison.OrdinalIgnoreCase) ||
                                     test.TestCode.Contains("TYMC", StringComparison.OrdinalIgnoreCase);
                var resultType = isQuantitative ? "Quantitative" : "Qualitative";

                loc.TestLocationMap.TryGetValue(test.TestCode, out var slocId);
                var sloc = sample.Locations.FirstOrDefault(l => l.Id == slocId);

                string? resCode = null;
                string? resDisplay = null;
                decimal? numVal = null;
                string? status = null;
                DateTime? enteredAt = null;
                string? enteredByUser = null;
                string cellState;
                string? cellLockReason = null;

                int? primaryObsId = null;
                string? primaryObsStr = null;
                bool isEligibleForConfirmation = false;
                string? confirmationStatus = null;
                List<ConfirmatoryPlateObservationDetailDto>? confirmatoryPlates = null;

                if (sloc != null && primaryObsMap.TryGetValue((sloc.Id, test.TestOrderId), out var pObs))
                {
                    primaryObsId = pObs.Id;
                    primaryObsStr = pObs.GrowthObservation.ToString();
                    isEligibleForConfirmation = pObs.GrowthObservation != GrowthObservation.NoGrowth;

                    if (pObs.ConfirmatoryPlateObservations.Count > 0)
                    {
                        confirmatoryPlates = pObs.ConfirmatoryPlateObservations
                            .OrderBy(p => p.MediumIndex)
                            .Select(p => new ConfirmatoryPlateObservationDetailDto(
                                p.Id,
                                p.MediumIndex,
                                p.MaterialId,
                                p.Material?.MaterialName,
                                p.Observation.ToString(),
                                p.ExpectedAppearanceSnapshot,
                                p.RecordedAtUtc,
                                UserName(p.RecordedByUserId)))
                            .ToList();
                    }

                    if (pObs.GrowthObservation == GrowthObservation.NoGrowth)
                    {
                        confirmationStatus = "NotApplicable";
                    }
                    else if (confirmatoryPlates != null && confirmatoryPlates.Count >= test.ConfirmatoryMediaCount)
                    {
                        confirmationStatus = "Completed";
                    }
                    else if (isEligibleForConfirmation)
                    {
                        confirmationStatus = "Eligible";
                    }
                }

                if (sloc != null && (!string.IsNullOrWhiteSpace(sloc.ReportedResult) || sloc.CFUResult.HasValue) && sloc.Status != ResultStatus.PendingConfirmation)
                {
                    resDisplay = sloc.ReportedResult;
                    numVal = sloc.CFUResult ?? sloc.CalculatedResult;
                    status = sloc.Status?.ToString() ?? "Entered";
                    enteredAt = sloc.EnteredAt;
                    enteredByUser = UserName(sloc.EnteredByUserId);

                    if (isQuantitative)
                    {
                        resCode = numVal?.ToString() ?? sloc.ReportedResult;
                    }
                    else
                    {
                        resCode = (sloc.ReportedResult?.Contains("Detected (+)") == true || sloc.ReportedResult?.Equals(nameof(ResultStatus.Detected), StringComparison.OrdinalIgnoreCase) == true)
                            ? "DETECTED"
                            : "NOT_DETECTED";
                    }

                    cellState = "COMPLETED";
                    completedCount++;
                }
                else
                {
                    if (test.IsResultEntryAllowed)
                    {
                        cellState = "AVAILABLE";
                        availableCount++;
                        missingResults.Add(new MissingResultDto(loc.LocationName, test.TestCode, test.DisplayName));
                    }
                    else
                    {
                        cellState = "LOCKED_PREREQUISITE";
                        cellLockReason = test.LockReason ?? "Locked until prerequisite is complete";
                        lockedCount++;
                        missingResults.Add(new MissingResultDto(loc.LocationName, test.TestCode, test.DisplayName));
                    }
                }

                matrix.Add(new MatrixCellResultDto(
                    slocId != 0 ? slocId : loc.PrimarySampleLocationId,
                    test.TestCode,
                    loc.LocationName,
                    resCode,
                    resDisplay,
                    numVal,
                    resultType,
                    status,
                    enteredAt,
                    enteredByUser,
                    IsEditable: cellState == "AVAILABLE",
                    CellState: cellState,
                    LockReason: cellLockReason,
                    PrimaryObservationId: primaryObsId,
                    PrimaryObservation: primaryObsStr,
                    IsEligibleForConfirmation: isEligibleForConfirmation,
                    ConfirmationStatus: confirmationStatus,
                    ConfirmatoryPlates: confirmatoryPlates));
            }
        }

        return new ResultMatrix(matrix, missingResults, completedCount, availableCount, lockedCount);
    }
}
