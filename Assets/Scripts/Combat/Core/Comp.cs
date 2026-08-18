namespace Combat.Core
{
    /// <summary>
    /// 所有逻辑组件的唯一基类。
    /// 只保留生命周期表面；业务 API 写在派生类上。
    /// </summary>
    public abstract class Comp
    {
        /// <summary>挂载后的宿主；Detach 后为 null。</summary>
        protected Actor Self { get; private set; }

        /// <summary>是否参与每帧本地 Tick。默认 false，避免空转。</summary>
        public virtual bool WantsTick => false;

        internal void Attach(Actor actor)
        {
            Self = actor;
            OnAttach();
        }

        internal void Detach()
        {
            OnDetach();
            Self = null;
        }

        /// <summary>
        /// 子类重写：解析同 Actor 依赖、缓存引用。
        /// 专属外部依赖请在构造函数注入，不要塞进基类参数。
        /// </summary>
        protected virtual void OnAttach() { }

        /// <summary>子类重写：清回调、断引用。</summary>
        protected virtual void OnDetach() { }

        /// <summary>仅当 WantsTick==true 时由 Actor 调用。</summary>
        public virtual void Tick(float dt) { }
    }
}
