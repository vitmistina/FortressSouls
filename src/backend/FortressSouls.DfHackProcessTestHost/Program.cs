namespace FortressSouls.DfHackProcessTestHost;

using System.Text;

public static class Marker
{
}

public static class Program
{
    public static int Main(string[] args)
    {
        var mode = Environment.GetEnvironmentVariable("FORTRESS_SOULS_DFHACK_TEST_MODE") ?? "success";
        var command = args.Length > 0 ? args[0] : string.Empty;

        return mode switch
        {
            "success" => WriteSuccess(command, args.Skip(1).ToArray()),
            "invalid_json" => WriteRaw("{"),
            "failed" => WriteFailure(),
            "crashed" => -1073741819,
            "oversize" => WriteRaw(new string('x', 200_000)),
            "hang" => Hang(),
            _ => WriteRaw("""{"schemaVersion":"fortress-souls-dwarf-list.v0.1","worldLoaded":true,"siteLoaded":true,"mapLoaded":true,"count":0,"items":[]}""")
        };
    }

    private static int WriteSuccess(string command, string[] args)
    {
        if (string.Equals(command, "fortress-souls/list-dwarves", StringComparison.Ordinal))
        {
            return WriteRaw(
                """
                {"schemaVersion":"fortress-souls-dwarf-list.v0.1","worldLoaded":true,"siteLoaded":true,"mapLoaded":true,"count":1,"items":[{"id":"6597","displayName":"Dwarf One","professionName":"Miner","professionToken":"MINER","currentJobType":"Dig","stressCategory":3,"stressCategoryScale":"0-most-stressed-6-least-stressed","soulPresent":true,"flags":{"isActive":true,"isAlive":true,"isCitizen":true,"isResident":false,"isSane":true}}]}
                """);
        }

        if (string.Equals(command, "fortress-souls/get-dwarf-snapshot", StringComparison.Ordinal))
        {
            var unitId = args.FirstOrDefault() ?? "6597";
            var snapshot =
                """
                {"schemaVersion":"fortress-souls-dwarf-snapshot.v0.1","requestedUnitId":"__UNIT_ID__","worldLoaded":true,"siteLoaded":true,"mapLoaded":true,"soulPresent":true,"identity":{"id":"__UNIT_ID__","readableName":"Dwarf One","professionName":"Miner","professionToken":"MINER","creatureId":"DWARF","casteId":"MALE"},"stress":{"raw":0,"longterm":0,"category":3,"categoryScale":"0-most-stressed-6-least-stressed"},"skills":{"count":1,"items":[{"token":"MINING","rating":5,"effective":5,"nominal":5,"experience":0,"totalExperience":0,"rust":0}]},"personality":{"present":true,"traits":{"count":1,"items":[{"token":"CHEER_PROPENSITY","value":60,"deviationFromNeutral50":10,"absDeviationFromNeutral50":10}]},"values":{"count":1,"items":[{"token":"FAMILY","type":1,"strength":25}]},"needs":{"count":1,"items":[{"token":"DrinkAlcohol","id":1,"deityId":-1,"focusLevel":0,"needLevel":10,"isUnmet":false,"isDeeplyUnmet":false}]},"mannerisms":{"count":0,"items":[]}},"promptCandidates":{"topSkills":[{"token":"MINING","rating":5,"effective":5,"nominal":5,"experience":0,"totalExperience":0,"rust":0}],"extremeTraits":[{"token":"CHEER_PROPENSITY","value":60,"deviationFromNeutral50":10,"absDeviationFromNeutral50":10}],"strongValues":[{"token":"FAMILY","type":1,"strength":25}],"strongNeeds":[{"token":"DrinkAlcohol","id":1,"deityId":-1,"focusLevel":0,"needLevel":10,"isUnmet":false,"isDeeplyUnmet":false}],"mannerisms":[]}}
                """;
            return WriteRaw(
                snapshot.Replace("__UNIT_ID__", unitId, StringComparison.Ordinal));
        }

        return WriteRaw("""{"schemaVersion":"fortress-souls-diagnose.v0.1","worldLoaded":true,"siteLoaded":true,"mapLoaded":true}""");
    }

    private static int WriteFailure()
    {
        Console.Out.WriteLine("script failed");
        Console.Error.WriteLine("stderr failed");
        return 3;
    }

    private static int WriteRaw(string value)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.Out.Write(value);
        return 0;
    }

    private static int Hang()
    {
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }
}
