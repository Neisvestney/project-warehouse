namespace ProjectWarehouse.Ops.Ui;

public static class Transient
{
    /// Runs a prompt and wipes the lines it left behind. A secret is masked while it is typed but
    /// the prompt itself stays in the scrollback, naming the key or the host it was asked for.
    public static T Ask<T>(Func<T> ask)
    {
        if (Console.IsOutputRedirected)
            return ask();

        int top;
        try
        {
            top = Console.CursorTop;
        }
        catch (IOException)
        {
            return ask();
        }

        var answer = ask();
        Erase(top);

        return answer;
    }

    private static void Erase(int fromLine)
    {
        try
        {
            // Read before the loop: writing a blank line moves the cursor, so a bound taken from
            // CursorTop each round would end the loop after the first line.
            var lastLine = Console.CursorTop;
            var blank = new string(' ', Math.Max(1, Console.BufferWidth - 1));

            for (var line = fromLine; line <= lastLine; line++)
            {
                Console.SetCursorPosition(0, line);
                Console.Write(blank);
            }

            Console.SetCursorPosition(0, fromLine);
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            // The screen scrolled out from under the recorded line, or there is no console to
            // position on. The prompt stays visible, which is the harmless outcome.
        }
    }
}
