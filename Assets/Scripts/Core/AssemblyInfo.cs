using System.Runtime.CompilerServices;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
[assembly: InternalsVisibleTo("AbsoluteZero.EditorTests")]
[assembly: InternalsVisibleTo("AbsoluteZero.ActionFixture")]
#endif
