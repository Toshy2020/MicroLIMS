namespace MicroLIMS.Domain.Enums;

// Where an Item's diluent or neutralizer comes from (Test Preparation).
// ContainedInMedium is for the neutralizer only: the culture medium itself
// carries it, so there is nothing separate to pick.
public enum PreparationMaterialSource
{
    MediaProduct,       // a culture media product; the analyst picks a released media lot
    SolutionRecipe,     // a general solution recipe (Diluent / Reagent / Neutralizer); the analyst picks a prepared solution
    MaterialEntry,      // a material master entry; the analyst picks a stock lot
    ContainedInMedium   // neutralizer only: contained in the selected medium, no lot to pick
}
