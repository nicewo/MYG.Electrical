using MYG.Electrical.Core.Devices;

namespace MYG.Electrical.Core.Tests;

public class PinNamingTests
{
    [Theory]
    [InlineData("X0", "X1")]
    [InlineData("x9", "x10")]
    [InlineData("I0.0", "I0.1")]
    [InlineData("I0.7", "I1.0")]        // Siemens bayt.bit
    [InlineData("Q3.7", "Q4.0")]
    [InlineData("%IX0.7", "%IX1.0")]
    [InlineData("X0.7", "X0.8")]        // Delta AS: 16 bit
    [InlineData("X0.15", "X1.0")]
    [InlineData("Y0.11", "Y0.12")]
    [InlineData("DI07", "DI08")]        // baştaki sıfır korunur
    [InlineData("DI09", "DI10")]
    [InlineData("1", "2")]
    [InlineData("L+", "L+")]           // sayısız ad değişmez
    [InlineData("A1", "A2")]
    public void Next_name_follows_eplan_style_sequences(string current, string expected) =>
        Assert.Equal(expected, PinNaming.Next(current));

    [Theory]
    [InlineData("X7", PinSequence.Octal, "X10")]
    [InlineData("X17", PinSequence.Octal, "X20")]
    [InlineData("X0.7", PinSequence.Byte8, "X1.0")]
    [InlineData("I0.9", PinSequence.Word16, "I0.10")]
    [InlineData("X9", PinSequence.Decimal, "X10")]
    public void Explicit_sequence_overrides_detection(string current, PinSequence seq, string expected) =>
        Assert.Equal(expected, PinNaming.Next(current, seq));

    [Fact]
    public void Sequence_lists_consecutive_names()
    {
        Assert.Equal(["X0", "X1", "X2", "X3"], PinNaming.Sequence("X0", 4));
        Assert.Equal(["I0.6", "I0.7", "I1.0", "I1.1"], PinNaming.Sequence("I0.6", 4));
    }
}
