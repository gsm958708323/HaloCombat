using Combat.Unity.Editor;

namespace Combat.EditorTools
{
    public static class FinalVerification
    {
        public static void Run()
        {
            GenerateDefaultDatabase.GenerateBatch();
            ValidateMenu.Validate();
            HaloCombatDemoSceneBuilder.VerifyAll();
        }
    }
}
