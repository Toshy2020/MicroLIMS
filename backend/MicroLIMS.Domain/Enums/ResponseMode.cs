namespace MicroLIMS.Domain.Enums;

// How a Standard-Comparison assay measures its response (SOP STM-PC-013 §6.9.2.5 / STM-PC-023).
public enum ResponseMode
{
    PeakArea = 0,        // HPLC: Response = PA_test / PA_std
    TitrationVolume = 1  // Titration: Response = (EP_test - EP_blank) / (EP_std - EP_blank), mL
}
