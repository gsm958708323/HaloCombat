using System.Linq;

namespace Combat.Config
{
    public static class SkillPreviewDefaults
    {
        public static PreviewDocument Create()
        {
            var positions=new[]{(1f,-2f),(2f,1f),(5f,3f),(-.3f,-4f),(-3f,3f),(2f,7f),(6f,0f),(0f,-2f)};
            return new PreviewDocument{Caster=new PreviewActorDocument{Id="caster",X=-9,Hp=1000,Atk=80},
                Targets=positions.Select((p,i)=>new PreviewActorDocument{Id="target-"+(i+1),X=p.Item1,Z=p.Item2,Hp=1000,Atk=0}).ToArray(),
                Obstacles=new[]{new PreviewObstacleDocument{Id="obstacle-1",X=-2,Z=4,Width=.8f,Depth=2.8f}}};
        }
    }
}
