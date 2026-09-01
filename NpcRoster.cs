namespace Mhodume;

/// <summary>
/// The full set of talkable NPCs in the game, as "&lt;map&gt;:BP_NPC_Dialog_C_&lt;n&gt;"
/// keys — the same shape the save records under NPCInteractedWith.
///
/// Captured from a 100%-complete save (every NPC spoken to), so it is the
/// reference roster: whatever is here and NOT in a given save is an NPC that
/// player has still to find. The game's own asset list would be the other way
/// to get this, but the pak is encrypted, so a known-complete save is it. The
/// index gaps (an NPC #1 that never appears) are real — those slots do not
/// exist in the game, so the roster is exactly this set, not a range.
/// </summary>
public static class NpcRoster
{
    public static readonly string[] All =
    {
        "Demo03_Sewers00:BP_NPC_Dialog_C_3",
        "Demo04_Sewers01:BP_NPC_Dialog_C_0",
        "Demo07_Showers00:BP_NPC_Dialog_C_0",
        "Gold_Apogee_00:BP_NPC_Dialog_C_0",
        "Gold_Apogee_01:BP_NPC_Dialog_C_0",
        "Gold_Apogee_01:BP_NPC_Dialog_C_1",
        "Gold_Apogee_02:BP_NPC_Dialog_C_1",
        "Gold_ConaptProject04:BP_NPC_Dialog_C_0",
        "Gold_ConaptProject04:BP_NPC_Dialog_C_1",
        "Gold_ConaptProject05:BP_NPC_Dialog_C_0",
        "Gold_ConaptProject05:BP_NPC_Dialog_C_1",
        "Gold_ConaptProject06:BP_NPC_Dialog_C_0",
        "Gold_ConaptProject08:BP_NPC_Dialog_C_0",
        "Gold_ConaptProject08:BP_NPC_Dialog_C_1",
        "Gold_ConaptProject09:BP_NPC_Dialog_C_0",
        "Gold_Foire:BP_NPC_Dialog_C_0",
        "Gold_Foire:BP_NPC_Dialog_C_1",
        "Gold_OpenOffice_00:BP_NPC_Dialog_C_0",
        "Gold_OpenOffice_00:BP_NPC_Dialog_C_2",
        "Gold_OpenOffice_00:BP_NPC_Dialog_C_3",
        "Gold_OpenOffice_00:BP_NPC_Dialog_C_4",
        "Gold_OpenOffice_00:BP_NPC_Dialog_C_6",
        "Gold_OpenOffice_01_alt:BP_NPC_Dialog_C_0",
        "Gold_OpenOffice_01_alt:BP_NPC_Dialog_C_1",
        "Gold_OpenOffice_01_alt:BP_NPC_Dialog_C_2",
        "Gold_OpenOffice_01_alt:BP_NPC_Dialog_C_3",
        "Gold_OpenOffice_01_alt:BP_NPC_Dialog_C_5",
        "Gold_fastlife02:BP_NPC_Dialog_C_0",
        "Gold_fastlife02:BP_NPC_Dialog_C_1",
        "Gold_fastlife02:BP_NPC_Dialog_C_2",
        "Gold_joan00:BP_NPC_Dialog_C_0",
        "Gold_racetrack:BP_NPC_Dialog_C_0",
        "Gold_test00:BP_NPC_Dialog_C_0",
        "Gold_test01:BP_NPC_Dialog_C_0",
        "Gold_test01:BP_NPC_Dialog_C_1",
        "Gold_test01:BP_NPC_Dialog_C_2",
        "Gold_test02:BP_NPC_Dialog_C_1",
        "Gold_test02:BP_NPC_Dialog_C_2",
        "Gold_test02:BP_NPC_Dialog_C_3",
        "Gold_test02:BP_NPC_Dialog_C_5",
        "Gold_test04:BP_NPC_Dialog_C_0",
    };

    public static int Total => All.Length;

    /// <summary>The map an NPC key belongs to (the part before the colon).</summary>
    public static string MapOf(string key)
    {
        var i = key.IndexOf(':');
        return i > 0 ? key[..i] : key;
    }

    /// <summary>The NPC's own id on its map, e.g. "#2" from "…_C_2".</summary>
    public static string ShortId(string key)
    {
        var u = key.LastIndexOf('_');
        return u >= 0 && u + 1 < key.Length ? "#" + key[(u + 1)..] : key;
    }
}
