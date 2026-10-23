namespace SharpAI.Serve
{
    internal static class ServeVersion
    {
        internal static string Current { get { return typeof(ServeVersion).Assembly.GetName().Version!.ToString(3); } }
    }
}
