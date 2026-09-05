namespace Combat.Core
{
    public static class CombatGeom
    {
        public static SimVec3 WorldPoint(in SimVec3 pos, float yawDeg, in SimVec3 local)
        {
            double r = yawDeg * System.Math.PI / 180.0;
            float c = (float)System.Math.Cos(r);
            float s = (float)System.Math.Sin(r);
            return new SimVec3(
                pos.X + local.X * c - local.Z * s,
                pos.Y + local.Y,
                pos.Z + local.X * s + local.Z * c
            );
        }

        public static SimVec3 HitboxCenter(TransformComp tf, HitboxComp box) =>
            tf == null || box == null
                ? SimVec3.Zero
                : WorldPoint(tf.Position, tf.YawDegrees, box.LocalOffset);
    }
}
