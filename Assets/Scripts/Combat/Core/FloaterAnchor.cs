using System;

namespace Combat.Core
{
    /// Resolves where a floating combat text should appear.
    ///
    /// Kept in Core so the anchor policy (prefer the view's Head anchor, fall back to a
    /// fixed offset above the body) is testable without Unity. The presentation layer
    /// only supplies the delegate that can look up anchors on a bound view.
    public static class FloaterAnchor
    {
        /// Anchor key the view exposes on the character's head bone.
        public const string HeadKey = "Head";
        /// Height above the head anchor so the number clears the model.
        public const float HeadOffsetY = .35f;
        /// Legacy fallback height above the logic position.
        public const float BodyOffsetY = 1.6f;

        public static SimVec3 Resolve(in SimVec3 body, Func<EntityId, string, SimVec3?> anchorResolver,
            EntityId id)
        {
            if (anchorResolver != null)
            {
                var head = anchorResolver(id, HeadKey);
                if (head.HasValue)
                {
                    var p = head.Value;
                    p.Y += HeadOffsetY;
                    return p;
                }
            }
            var fallback = body;
            fallback.Y += BodyOffsetY;
            return fallback;
        }
    }
}
