using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Fortified;

public class ModExt_EnvironmentalBill : DefModExtension
{
    public bool OnlyInCleanliness = false;
    public bool OnlyInDarkness = false;
    public bool OnlyInVacuum = false;
    public bool OnlyInMicroGravity = false;

    public IEnumerable<StatDrawEntry> SpecialDisplayStats()
    {
        List<string> reportTexts = new();
        if (OnlyInCleanliness)
            reportTexts.Add("Clean");
        if (OnlyInDarkness)
            reportTexts.Add("Darkness");
        if (OnlyInVacuum)
            reportTexts.Add("Vacuum");
        if (OnlyInMicroGravity)
            reportTexts.Add("MicroGravity");

        yield return new StatDrawEntry(
            StatCategoryDefOf.Basics,
            "FFF.EnvironmentRestriction".Translate(),
            string.Join(' ', reportTexts),
            null, 1145);
    }
}