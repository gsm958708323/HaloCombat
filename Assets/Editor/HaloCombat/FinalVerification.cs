using Combat.Unity;
using Combat.Unity.Editor;

namespace Combat.EditorTools
{
    public static class FinalVerification
    {
        public static void Run()
        {
            UnityRuntimeContent.Load();
            HaloCombatDemoSceneBuilder.VerifyAll();
            SeasonThreeSceneBuilder.VerifyAll();
        }
    }
}
