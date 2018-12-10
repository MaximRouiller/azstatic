using System;
using System.Collections.Generic;

namespace azstatic.ConsoleComponents
{
    public static class CliPicker
    {
        // copied from: https://stackoverflow.com/a/39820564/24975
        public static TOption SelectFromList<TOption>(List<TOption> options, Func<TOption, string> optionRender, ConsoleColor selectedColor = ConsoleColor.Cyan)
        {
            int selected = 0;
            bool done = false;
            
            while (!done)
            {

                for (int i = 0; i < options.Count; i++)
                {
                    if (selected == i)
                    {
                        Console.ForegroundColor = selectedColor;
                        Console.Write("> ");
                    }
                    else
                    {
                        Console.Write("  ");
                    }
                    Console.WriteLine(optionRender(options[i]));
                    Console.ResetColor();
                }

                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.UpArrow:
                        selected = Math.Max(0, selected - 1);
                        break;
                    case ConsoleKey.DownArrow:
                        selected = Math.Min(options.Count - 1, selected + 1);
                        break;
                    case ConsoleKey.Enter:
                        done = true;
                        break;
                }

                if (!done)
                    Console.CursorTop = Console.CursorTop - options.Count;
            }

            return options[selected];
        }
    }
}
