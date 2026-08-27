using System;
using Combat.Demos;

namespace Combat
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            string which = (args != null && args.Length > 0) ? args[0] : "season";
            switch (which)
            {
                case "tag": TagInputDemo.Run(); break;
                case "attr": AttributeDemo.Run(); break;
                case "buff": BuffDemo.Run(); break;
                case "motor": ActivityMotorDemo.Run(); break;
                case "clip": ClipPayloadDemo.Run(); break;
                case "melee": MeleeDamageDemo.Run(); break;
                case "proj": ProjectileAoeDemo.Run(); break;
                case "season":
                default:
                    SeasonOneDemo.Run();
                    break;
            }

            return 0;
        }
    }
}
