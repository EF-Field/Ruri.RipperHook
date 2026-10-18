using System;

namespace Ruri.Hook.Attributes
{
    /// <summary>
    /// Marks a static parameterless method returning <c>IEnumerable&lt;MethodBase&gt;</c>: the upstream methods hooks of
    /// this assembly retarget from code rather than by <see cref="RetargetMethodAttribute"/>, so they can be reserved against
    /// inlining before the first hook is applied (see <c>HookInlining</c>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RetargetSourcesAttribute : Attribute
    {
    }
}
