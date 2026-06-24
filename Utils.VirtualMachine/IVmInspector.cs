namespace Utils.VirtualMachine;

/// <summary>
/// Optional inspector interface that receives notifications before each instruction is dispatched.
/// Inject an implementation into <see cref="VirtualProcessor{T}.Inspector"/> to enable step-by-step
/// debugging, profiling, or instruction tracing without subclassing the processor.
/// </summary>
/// <typeparam name="T">The context type, constrained to <see cref="Context"/>.</typeparam>
/// <remarks>
/// <see cref="OnBreakpoint"/> is called first (when applicable), then <see cref="BeforeInstruction"/>
/// for the same step. Both callbacks fire before the instruction handler runs, so the context
/// (including <see cref="Context.InstructionPointer"/>) may be modified before dispatch.
/// </remarks>
public interface IVmInspector<in T> where T : Context
{
    /// <summary>
    /// Called before every instruction is dispatched during execution.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <param name="address">The instruction-pointer value at the start of the instruction.</param>
    /// <param name="instructionName">The resolved name of the instruction (as declared in <see cref="InstructionAttribute"/>).</param>
    void BeforeInstruction(T context, int address, string instructionName);

    /// <summary>
    /// Called when execution reaches an address listed in <see cref="VirtualProcessor{T}.Breakpoints"/>,
    /// immediately before <see cref="BeforeInstruction"/> for the same step.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <param name="address">The breakpoint address that was hit.</param>
    /// <param name="instructionName">The resolved name of the instruction at the breakpoint.</param>
    void OnBreakpoint(T context, int address, string instructionName);
}
