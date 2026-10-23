namespace SharpAI.Serve
{
    using SyslogLogging;

    internal static class ServeBanner
    {
        internal const string Text = """
       __                 ___   ____
  ___ / /  ___ ________  / _ | /  _/
 (_-</ _ \/ _ `/ __/ _ \/ __ |_/ /
/___/_//_/\_,_/_/ / .__/_/ |_/___/
                 /_/
""";
        internal static void Write(LoggingModule logging)
        {
            foreach (string line in Text.Split('\n')) logging.Log(Severity.Info, line.TrimEnd('\r'));
            logging.Log(Severity.Info, "SharpAI.Serve " + ServeVersion.Current);
        }
    }
}
