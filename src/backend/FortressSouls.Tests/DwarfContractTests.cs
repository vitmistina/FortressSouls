namespace FortressSouls.Tests;

using System.Text.Json;
using FortressSouls.Domain;

public class DwarfContractTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("12x")]
    [InlineData("123456789012345678901")]
    public void DwarfIdParseRejectsInvalidValues(string input)
    {
        Assert.Throws<ArgumentException>(() => DwarfId.Parse(input));
    }

    [Fact]
    public void DwarfIdSerializesAsJsonString()
    {
        var result = JsonSerializer.Serialize(DwarfId.Parse("6603"), SerializerOptions);

        Assert.Equal("\"6603\"", result);
    }

    [Fact]
    public void DwarfContractsSerializeDeterministically()
    {
        var list = new DwarfListResult(
            SchemaVersion: DwarfSchemaVersions.List,
            Source: new DwarfListSourceMetadata(
                WorldLoaded: true,
                SiteLoaded: true,
                MapLoaded: true),
            Items:
            [
                new DwarfSummary(
                    Id: DwarfId.Parse("6597"),
                    DisplayName: "Melbil Keskalmeden \"Shottribe\", Miner",
                    ProfessionName: "Miner",
                    ProfessionToken: "MINER",
                    CurrentJobType: "PickupEquipment",
                    StressCategory: 3,
                    StressCategoryScale: "0-most-stressed-6-least-stressed",
                    SoulPresent: true,
                    Flags: new DwarfStatusFlags(
                        IsActive: true,
                        IsAlive: true,
                        IsCitizen: true,
                        IsResident: false,
                        IsSane: true))
            ]);

        var snapshot = new DwarfSnapshot(
            SchemaVersion: DwarfSchemaVersions.Snapshot,
            Source: new DwarfSnapshotSourceMetadata(
                WorldLoaded: true,
                SiteLoaded: true,
                MapLoaded: true,
                SoulPresent: true),
            RequestedDwarfId: DwarfId.Parse("6597"),
            Identity: new DwarfIdentity(
                Id: DwarfId.Parse("6597"),
                ReadableName: "Melbil Keskalmeden \"Shottribe\", Miner",
                ProfessionName: "Miner",
                ProfessionToken: "MINER",
                CreatureId: "DWARF",
                CasteId: "FEMALE"),
            Work: new DwarfWork(CurrentJobType: "PickupEquipment"),
            Stress: new DwarfStress(
                Raw: 0,
                Longterm: 0,
                Category: 3,
                CategoryScale: "0-most-stressed-6-least-stressed"),
            Skills: new DwarfSkillCollection(
                Count: 1,
                Items:
                [
                    new DwarfSkill(
                        Token: "MINING",
                        Rating: 5,
                        Effective: 5,
                        Nominal: 5,
                        Experience: 0,
                        TotalExperience: 3500,
                        Rust: 0)
                ]),
            Personality: new DwarfPersonality(
                Present: true,
                Traits: new DwarfTraitCollection(
                    Count: 1,
                    Items:
                    [
                        new DwarfPersonalityTrait(
                            Token: "BASHFUL",
                            Value: 99,
                            DeviationFromNeutral50: 49,
                            AbsDeviationFromNeutral50: 49)
                    ]),
                Values: new DwarfValueCollection(
                    Count: 1,
                    Items:
                    [
                        new DwarfValue(
                            Token: "FAMILY",
                            Type: 2,
                            Strength: -25)
                    ]),
                Needs: new DwarfNeedCollection(
                    Count: 1,
                    Items:
                    [
                        new DwarfNeed(
                            Token: "DrinkAlcohol",
                            Id: 1,
                            DeityId: -1,
                            FocusLevel: 0,
                            NeedLevel: 10,
                            IsUnmet: false,
                            IsDeeplyUnmet: false)
                    ]),
                Mannerisms: new DwarfMannerismCollection(
                    Count: 1,
                    Items:
                    [
                        new DwarfMannerism(
                            Token: "TONGUE_STICKS_OUT",
                            SituationToken: "WHEN_THINKING")
                    ])),
            PromptCandidates: new DwarfPromptCandidates(
                TopSkills:
                [
                    new DwarfSkill(
                        Token: "MINING",
                        Rating: 5,
                        Effective: 5,
                        Nominal: 5,
                        Experience: 0,
                        TotalExperience: 3500,
                        Rust: 0)
                ],
                ExtremeTraits:
                [
                    new DwarfPersonalityTrait(
                        Token: "BASHFUL",
                        Value: 99,
                        DeviationFromNeutral50: 49,
                        AbsDeviationFromNeutral50: 49)
                ],
                StrongValues:
                [
                    new DwarfValue(
                        Token: "FAMILY",
                        Type: 2,
                        Strength: -25)
                ],
                StrongNeeds:
                [
                    new DwarfNeed(
                        Token: "DrinkAlcohol",
                        Id: 1,
                        DeityId: -1,
                        FocusLevel: 0,
                        NeedLevel: 10,
                        IsUnmet: false,
                        IsDeeplyUnmet: false)
                ],
                Mannerisms:
                [
                    new DwarfMannerism(
                        Token: "TONGUE_STICKS_OUT",
                        SituationToken: "WHEN_THINKING")
                ]));

        var listJson = JsonSerializer.Serialize(list, SerializerOptions);
        var snapshotJson = JsonSerializer.Serialize(snapshot, SerializerOptions);

        Assert.Equal(
            "{\"schemaVersion\":\"fortress-souls-dwarf-list.v0.1\",\"source\":{\"worldLoaded\":true,\"siteLoaded\":true,\"mapLoaded\":true},\"items\":[{\"id\":\"6597\",\"displayName\":\"Melbil Keskalmeden \\u0022Shottribe\\u0022, Miner\",\"professionName\":\"Miner\",\"professionToken\":\"MINER\",\"currentJobType\":\"PickupEquipment\",\"stressCategory\":3,\"stressCategoryScale\":\"0-most-stressed-6-least-stressed\",\"soulPresent\":true,\"flags\":{\"isActive\":true,\"isAlive\":true,\"isCitizen\":true,\"isResident\":false,\"isSane\":true}}]}",
            listJson);
        Assert.Equal(
            "{\"schemaVersion\":\"fortress-souls-dwarf-snapshot.v0.1\",\"source\":{\"worldLoaded\":true,\"siteLoaded\":true,\"mapLoaded\":true,\"soulPresent\":true},\"requestedDwarfId\":\"6597\",\"identity\":{\"id\":\"6597\",\"readableName\":\"Melbil Keskalmeden \\u0022Shottribe\\u0022, Miner\",\"professionName\":\"Miner\",\"professionToken\":\"MINER\",\"creatureId\":\"DWARF\",\"casteId\":\"FEMALE\"},\"work\":{\"currentJobType\":\"PickupEquipment\"},\"stress\":{\"raw\":0,\"longterm\":0,\"category\":3,\"categoryScale\":\"0-most-stressed-6-least-stressed\"},\"skills\":{\"count\":1,\"items\":[{\"token\":\"MINING\",\"rating\":5,\"effective\":5,\"nominal\":5,\"experience\":0,\"totalExperience\":3500,\"rust\":0}]},\"personality\":{\"present\":true,\"traits\":{\"count\":1,\"items\":[{\"token\":\"BASHFUL\",\"value\":99,\"deviationFromNeutral50\":49,\"absDeviationFromNeutral50\":49}]},\"values\":{\"count\":1,\"items\":[{\"token\":\"FAMILY\",\"type\":2,\"strength\":-25}]},\"needs\":{\"count\":1,\"items\":[{\"token\":\"DrinkAlcohol\",\"id\":1,\"deityId\":-1,\"focusLevel\":0,\"needLevel\":10,\"isUnmet\":false,\"isDeeplyUnmet\":false}]},\"mannerisms\":{\"count\":1,\"items\":[{\"token\":\"TONGUE_STICKS_OUT\",\"situationToken\":\"WHEN_THINKING\"}]}},\"promptCandidates\":{\"topSkills\":[{\"token\":\"MINING\",\"rating\":5,\"effective\":5,\"nominal\":5,\"experience\":0,\"totalExperience\":3500,\"rust\":0}],\"extremeTraits\":[{\"token\":\"BASHFUL\",\"value\":99,\"deviationFromNeutral50\":49,\"absDeviationFromNeutral50\":49}],\"strongValues\":[{\"token\":\"FAMILY\",\"type\":2,\"strength\":-25}],\"strongNeeds\":[{\"token\":\"DrinkAlcohol\",\"id\":1,\"deityId\":-1,\"focusLevel\":0,\"needLevel\":10,\"isUnmet\":false,\"isDeeplyUnmet\":false}],\"mannerisms\":[{\"token\":\"TONGUE_STICKS_OUT\",\"situationToken\":\"WHEN_THINKING\"}]}}",
            snapshotJson);
    }
}
