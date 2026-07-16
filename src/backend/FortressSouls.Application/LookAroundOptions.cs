namespace FortressSouls.Application;

public sealed class LookAroundOptions
{
    public const string ConfigurationSectionPath = "FortressSouls:Perception:LookAround";
    public const int MaximumSupportedRadius = 16;

    public int DefaultRadius { get; set; } = 1;

    public int MaxRadius { get; set; } = 2;

    public LookAroundOptions Validate()
    {
        if (DefaultRadius < 1
            || MaxRadius < DefaultRadius
            || MaxRadius > MaximumSupportedRadius)
        {
            throw new ArgumentException(
                $"Look-around radius must be between 1 and {MaximumSupportedRadius}, with MaxRadius at least DefaultRadius.");
        }

        return this;
    }

    public static LookAroundOptions CreateDefault() => new();
}
