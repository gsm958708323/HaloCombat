using System;
using Combat.Core;
using Combat.Demos;

namespace Combat
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            NoPopup.Arm();
            CombatLog.SetSink(new ConsoleLogSink());
            string which;
            string category;
            int parseResult = ParseArguments(args, out which, out category);
            if (parseResult != 0)
                return parseResult;

            CombatLog.SetCategoryFilter(category);
            try
            {
                return RunDemo(which);
            }
            catch (Exception e)
            {
                // A failing demo must fail through the log and the exit code. An
                // unhandled exception makes the CLR abort the process, which pops a
                // Windows Error Reporting dialog (0xE0434352) and hangs unattended runs.
                Console.Error.WriteLine("DEMO FAILED: " + e);
                return 1;
            }
        }

        static int RunDemo(string which)
        {
            switch (which)
            {
                case "tag": TagInputDemo.Run(); break;
                case "attr": AttributeDemo.Run(); break;
                case "buff": BuffDemo.Run(); break;
                case "motor": ActivityMotorDemo.Run(); break;
                case "clip": ClipPayloadDemo.Run(); break;
                case "melee": MeleeDamageDemo.Run(); break;
                case "proj": ProjectileAoeDemo.Run(); break;
                case "knock": KnockdownDemo.Run(); break;
                case "dodge": DodgeHitstopDemo.Run(); break;
                case "aura": AuraHomingDemo.Run(); break;
                case "bt": BehaviorTreeDemo.Run(); break;
                case "perc": PerceptionDemo.Run(); break;
                case "enemy": EnemyAiDemo.Run(); break;
                case "summon": SummonDemo.Run(); break;
                case "clock": ClockDemo.Run(); break;
                case "spawn": SpawnEventDemo.Run(); break;
                case "lesson": SeasonThreeLessonDemo.Run(); break;
                case "season2":
                case "s2":
                    SeasonTwoDemo.Run();
                    break;
                case "present-season":
                case "regress":
                    SeasonTwoDemo.Regression();
                    break;
                case "all":
                    SeasonTwoDemo.Regression();
                    break;
                case "season":
                    SeasonOneDemo.Run();
                    break;
                default:
                    Console.Error.WriteLine("Unknown demo '" + which + "'.");
                    return 2;
            }

            return 0;
        }

        static int ParseArguments(string[] args, out string which, out string category)
        {
            which = "season";
            category = null;
            bool hasDemo = false;

            if (args == null)
                return 0;

            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                if (argument == "--category")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("--category requires a category name or All.");
                        return 2;
                    }

                    category = args[++i];
                    continue;
                }

                const string categoryPrefix = "--category=";
                if (argument != null && argument.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    category = argument.Substring(categoryPrefix.Length);
                    continue;
                }

                if (!hasDemo)
                {
                    which = argument;
                    hasDemo = true;
                    continue;
                }

                Console.Error.WriteLine("Only one demo argument is supported.");
                return 2;
            }

            if (string.IsNullOrWhiteSpace(category) ||
                string.Equals(category.Trim(), "All", StringComparison.OrdinalIgnoreCase))
            {
                category = null;
                return 0;
            }

            string canonical = CombatCategories.Canonicalize(category);
            if (string.IsNullOrEmpty(canonical))
            {
                Console.Error.WriteLine("Unknown category '" + category + "'. Use All, TagInput, Attribute, Buff, ActivityMotor, ClipPayload, MeleeDamage, ProjectileAoe, SeasonOne, Knockdown, DodgeHitstop, AuraHoming, BehaviorTree, Perception, EnemyAi, Summon, or SeasonTwo.");
                return 2;
            }

            category = canonical;
            return 0;
        }
    }

    /// <summary>
    /// Windows-only crash-dialog guard for unattended demo runs. A managed exception
    /// that escapes the entry point, or a native fault, otherwise raises the JIT
    /// debugger prompt and the "application error" box, which hangs automation until
    /// somebody clicks it. Failures must show up as log lines plus a non-zero exit
    /// code instead.
    /// </summary>
    internal static class NoPopup
    {
        const uint SemFailCriticalErrors = 0x0001;
        const uint SemNoGpFaultErrorBox = 0x0002;
        const uint SemNoOpenFileErrorBox = 0x8000;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern uint SetErrorMode(uint mode);

        internal static void Arm()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.Windows))
                return;
            try
            {
                SetErrorMode(SemFailCriticalErrors | SemNoGpFaultErrorBox | SemNoOpenFileErrorBox);
            }
            catch (Exception)
            {
                // Best effort: the guard itself must never break a demo run.
            }
        }
    }
}
