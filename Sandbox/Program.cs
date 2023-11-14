namespace Sandbox;

using Corvus.Expressions.SourceGenerator;

partial class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(StringLengthBonsai());
        Console.WriteLine();
        Console.WriteLine(SayHelloBonsai());
        Console.WriteLine();
        Console.WriteLine(SpanLengthBonsai());
        Console.WriteLine();
    }


    [GenerateBonsai]
    public static Func<string, int> StringLength => static message => message.Length;


    [GenerateBonsai]
    public static Func<string, string> SayHello => static message => "Hello " + message;

    [GenerateBonsai]
    public static DoTheThing SpanLength => static message => message.Length;


    public delegate int DoTheThing(ReadOnlySpan<char> message);
}