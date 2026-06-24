using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine;

// â”€â”€ Minimal test VM (opcodes assigned only here, not in the framework) â”€â”€â”€â”€â”€â”€â”€â”€

public class ControlFlowTestMachine : VirtualProcessor<ControlFlowContext>
{
    [Instruction("PUSH_INT", 0x01)]
    void PushInt(ControlFlowContext ctx, int value) => ctx.Stack.Push(value);

    [Instruction("HALT", 0xFF)]
    void Halt(ControlFlowContext ctx) => ctx.InstructionPointer = ctx.Data.Length;

    // â”€â”€ Conditionals â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// IF_FALSE endAddr [elseAddr] â€” pops a bool; if false jumps to elseAddr (or endAddr).
    /// Pushes a ConditionalBlock regardless so ENDIF can pop it correctly.
    /// </summary>
    [Instruction("IF_FALSE", 0x10)]
    void IfFalse(ControlFlowContext ctx, int endAddress, int elseAddress)
    {
        bool condition = (bool)ctx.Stack.Pop();
        ctx.ControlFlow.PushConditional(ctx.InstructionPointer, endAddress, elseAddress);
        if (!condition)
            ctx.InstructionPointer = elseAddress;
    }

    [Instruction("ENDIF", 0x11)]
    void Endif(ControlFlowContext ctx) => ctx.ControlFlow.Pop();

    // â”€â”€ Loops â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>LOOP_START endAddr â€” marks the loop header; execution falls through into body.</summary>
    [Instruction("LOOP_START", 0x20)]
    void LoopStart(ControlFlowContext ctx, int endAddress)
        => ctx.ControlFlow.PushLoop(ctx.InstructionPointer, endAddress);

    /// <summary>LOOP_END â€” jumps back to loop StartAddress without popping the block.</summary>
    [Instruction("LOOP_END", 0x21)]
    void LoopEnd(ControlFlowContext ctx)
        => ctx.InstructionPointer = ((LoopBlock)ctx.ControlFlow.CurrentBlock!).StartAddress;

    [Instruction("BREAK", 0x22)]
    void Break(ControlFlowContext ctx) => ctx.ControlFlow.Break(ctx);

    [Instruction("CONTINUE", 0x23)]
    void Continue(ControlFlowContext ctx) => ctx.ControlFlow.Continue(ctx);

    // â”€â”€ Exceptions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>TRY catchAddr â€” opens an ExceptionBlock with the given catch handler address.</summary>
    [Instruction("TRY", 0x30)]
    void Try(ControlFlowContext ctx, int catchAddress)
        => ctx.ControlFlow.PushException(ctx.InstructionPointer, catchAddress, null);

    /// <summary>THROW â€” pops a value from the operand stack and throws it.</summary>
    [Instruction("THROW", 0x31)]
    void Throw(ControlFlowContext ctx)
    {
        var value = ctx.Stack.Pop();
        if (!ctx.ControlFlow.Throw(ctx, value))
            throw new InvalidOperationException($"Unhandled throw: {value}");
    }

    /// <summary>ENDTRY â€” closes the exception block.</summary>
    [Instruction("ENDTRY", 0x32)]
    void EndTry(ControlFlowContext ctx) => ctx.ControlFlow.Pop();
}

// â”€â”€ Unit tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[TestClass]
public class ControlFlowStackTests
{
    // â”€â”€ ConditionalBlock â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void PushConditional_IncreasesDepth()
    {
        var cfs = new ControlFlowStack();
        cfs.PushConditional(0, 20);
        Assert.AreEqual(1, cfs.Depth);
        Assert.IsInstanceOfType<ConditionalBlock>(cfs.CurrentBlock);
    }

    [TestMethod]
    public void ConditionalBlock_Properties_AreStored()
    {
        var cfs = new ControlFlowStack();
        cfs.PushConditional(startAddress: 0, endAddress: 20, elseAddress: 10);
        var block = (ConditionalBlock)cfs.CurrentBlock!;
        Assert.AreEqual(0, block.StartAddress);
        Assert.AreEqual(20, block.EndAddress);
        Assert.AreEqual(10, block.ElseAddress);
    }

    [TestMethod]
    public void ConditionalBlock_NoElse_ElseAddressIsNull()
    {
        var cfs = new ControlFlowStack();
        cfs.PushConditional(0, 20);
        Assert.IsNull(((ConditionalBlock)cfs.CurrentBlock!).ElseAddress);
    }

    [TestMethod]
    public void Pop_ConditionalBlock_DecreasesDepth()
    {
        var cfs = new ControlFlowStack();
        cfs.PushConditional(0, 20);
        cfs.Pop();
        Assert.AreEqual(0, cfs.Depth);
    }

    // â”€â”€ LoopBlock â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void PushLoop_IncreasesDepth()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 50);
        Assert.AreEqual(1, cfs.Depth);
        Assert.IsInstanceOfType<LoopBlock>(cfs.CurrentBlock);
    }

    [TestMethod]
    public void LoopBlock_Properties_AreStored()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(startAddress: 5, endAddress: 40);
        var block = (LoopBlock)cfs.CurrentBlock!;
        Assert.AreEqual(5, block.StartAddress);
        Assert.AreEqual(40, block.EndAddress);
    }

    [TestMethod]
    public void Break_JumpsToEndAddress_AndPopsLoop()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(startAddress: 0, endAddress: 50);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Break(ctx);
        Assert.AreEqual(50, ctx.InstructionPointer);
        Assert.AreEqual(0, cfs.Depth);
    }

    [TestMethod]
    public void Break_UnwindsNestedConditionals_ThenPopsLoop()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 50);
        cfs.PushConditional(10, 30); // nested IF inside the loop
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Break(ctx);
        Assert.AreEqual(50, ctx.InstructionPointer);
        Assert.AreEqual(0, cfs.Depth); // both blocks gone
    }

    [TestMethod]
    public void Break_OutsideLoop_Throws()
    {
        var cfs = new ControlFlowStack();
        var ctx = new ControlFlowContext(new byte[0]);
        Assert.ThrowsException<InvalidOperationException>(() => cfs.Break(ctx));
    }

    [TestMethod]
    public void Continue_JumpsToStartAddress_KeepsLoop()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(startAddress: 5, endAddress: 40);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Continue(ctx);
        Assert.AreEqual(5, ctx.InstructionPointer);
        Assert.AreEqual(1, cfs.Depth); // loop stays on stack
    }

    [TestMethod]
    public void Continue_UnwindsNestedConditionals_KeepsLoop()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(5, 40);
        cfs.PushConditional(10, 30);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Continue(ctx);
        Assert.AreEqual(5, ctx.InstructionPointer);
        Assert.AreEqual(1, cfs.Depth); // only loop remains
        Assert.IsInstanceOfType<LoopBlock>(cfs.CurrentBlock);
    }

    [TestMethod]
    public void Continue_OutsideLoop_Throws()
    {
        var cfs = new ControlFlowStack();
        var ctx = new ControlFlowContext(new byte[0]);
        Assert.ThrowsException<InvalidOperationException>(() => cfs.Continue(ctx));
    }

    // â”€â”€ ExceptionBlock â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void PushException_IncreasesDepth()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: 10, finallyAddress: null);
        Assert.AreEqual(1, cfs.Depth);
        Assert.IsInstanceOfType<ExceptionBlock>(cfs.CurrentBlock);
    }

    [TestMethod]
    public void ExceptionBlock_Properties_AreStored()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: 10, finallyAddress: 20);
        var block = (ExceptionBlock)cfs.CurrentBlock!;
        Assert.AreEqual(10, block.CatchAddress);
        Assert.AreEqual(20, block.FinallyAddress);
    }

    [TestMethod]
    public void ExceptionBlock_NoCatchAndNoFinally_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new ExceptionBlock(0, catchAddress: null, finallyAddress: null));
    }

    [TestMethod]
    public void Throw_JumpsToCatchAddress_SetsThrownValue()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: 42, finallyAddress: null);
        var ctx = new ControlFlowContext(new byte[0]);
        bool handled = cfs.Throw(ctx, "error");
        Assert.IsTrue(handled);
        Assert.AreEqual(42, ctx.InstructionPointer);
        var ex = (ExceptionBlock)cfs.CurrentBlock!;
        Assert.AreEqual("error", ex.ThrownValue);
    }

    [TestMethod]
    public void Throw_NoFinallyFallsBackToCatch_WhenCatchAddressNull_JumpsToFinally()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: null, finallyAddress: 99);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Throw(ctx, 42);
        Assert.AreEqual(99, ctx.InstructionPointer);
    }

    [TestMethod]
    public void Throw_ExceptionBlockRemainsOnStack_ForHandlerBody()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: 10, finallyAddress: null);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Throw(ctx, "oops");
        Assert.AreEqual(1, cfs.Depth);
        Assert.IsInstanceOfType<ExceptionBlock>(cfs.CurrentBlock);
    }

    [TestMethod]
    public void Throw_UnwindsNestedBlocks_ThenFindsCatch()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: 50, finallyAddress: null);
        cfs.PushLoop(5, 30);        // nested inside try
        cfs.PushConditional(10, 20); // nested inside loop
        var ctx = new ControlFlowContext(new byte[0]);
        bool handled = cfs.Throw(ctx, "e");
        Assert.IsTrue(handled);
        Assert.AreEqual(50, ctx.InstructionPointer);
        Assert.AreEqual(1, cfs.Depth); // only the ExceptionBlock stays
    }

    [TestMethod]
    public void Throw_NoHandler_ReturnsFalse()
    {
        var cfs = new ControlFlowStack();
        var ctx = new ControlFlowContext(new byte[0]);
        Assert.IsFalse(cfs.Throw(ctx, "e"));
    }

    // â”€â”€ Pop underflow â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void Pop_EmptyStack_Throws()
    {
        var cfs = new ControlFlowStack();
        Assert.ThrowsException<InvalidOperationException>(() => cfs.Pop());
    }

    // â”€â”€ Typed Pop<T> â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void PopTyped_CorrectType_ReturnsBlock()
    {
        var cfs = new ControlFlowStack();
        cfs.PushConditional(0, 20, 10);
        var block = cfs.Pop<ConditionalBlock>();
        Assert.IsNotNull(block);
        Assert.AreEqual(0, block.StartAddress);
        Assert.AreEqual(0, cfs.Depth);
    }

    [TestMethod]
    public void PopTyped_WrongType_ThrowsVirtualProcessorException()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 50); // LoopBlock is on top
        Assert.ThrowsException<VirtualProcessorException>(() => cfs.Pop<ConditionalBlock>());
    }

    [TestMethod]
    public void PopTyped_WrongType_BlockNotConsumed()
    {
        // Pop<T> peeks before popping: on type mismatch the block stays on the stack.
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 50);
        try { cfs.Pop<ConditionalBlock>(); } catch (VirtualProcessorException) { }
        Assert.AreEqual(1, cfs.Depth);
        Assert.IsInstanceOfType<LoopBlock>(cfs.CurrentBlock);
    }

    [TestMethod]
    public void PopTyped_EmptyStack_Throws()
    {
        var cfs = new ControlFlowStack();
        Assert.ThrowsException<InvalidOperationException>(() => cfs.Pop<LoopBlock>());
    }

    // â”€â”€ FullContext â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void FullContext_DefaultCtor_HasCallStackAndControlFlow()
    {
        var ctx = new FullContext(new byte[0]);
        Assert.IsNotNull(ctx.CallStack);
        Assert.IsNotNull(ctx.ControlFlow);
        Assert.IsInstanceOfType<CallStack>(ctx.CallStack);
    }

    [TestMethod]
    public void FullContext_CustomCallStack_IsUsed()
    {
        var simple = new SimpleCallStack();
        var ctx = new FullContext(new byte[0], simple);
        Assert.AreSame(simple, ctx.CallStack);
    }

    [TestMethod]
    public void FullContext_NullCallStack_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new FullContext(new byte[0], null!));
    }

    [TestMethod]
    public void FullContext_CallAndControlFlow_WorkIndependently()
    {
        var ctx = new FullContext(new byte[0]);
        ctx.CallStack.Call(42);
        ctx.ControlFlow.PushLoop(0, 100);

        Assert.AreEqual(1, ctx.CallStack.Depth);
        Assert.AreEqual(1, ctx.ControlFlow.Depth);

        Assert.AreEqual(42, ctx.CallStack.Return());
        ctx.ControlFlow.Pop();

        Assert.IsTrue(ctx.CallStack.IsEmpty);
        Assert.AreEqual(0, ctx.ControlFlow.Depth);
    }

    [TestMethod]
    public void FullContext_ThreeArgCtor_UsesBothProvidedInstances()
    {
        var callStack = new SimpleCallStack();
        var controlFlow = new ControlFlowStack();
        controlFlow.PushLoop(0, 10);
        var ctx = new FullContext(new byte[0], callStack, controlFlow);
        Assert.AreSame(callStack, ctx.CallStack);
        Assert.AreSame(controlFlow, ctx.ControlFlow);
        Assert.AreEqual(1, ctx.ControlFlow.Depth);
    }

    [TestMethod]
    public void FullContext_ThreeArgCtor_NullControlFlow_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new FullContext(new byte[0], new SimpleCallStack(), null!));
    }

    // â”€â”€ Integration: BREAK exits loop, PUSH_INT 99 is never reached â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void Integration_Break_ExitsLoop()
    {
        // 0: PUSH_INT 42       [0x01, 42,0,0,0]
        // 5: LOOP_START 16     [0x20, 16,0,0,0]   end=16
        //10: BREAK             [0x22]              jumps to 16, pops loop
        //11: PUSH_INT 99       [0x01, 99,0,0,0]   never reached
        //16: HALT              [0xFF]
        byte[] program =
        [
            0x01, 42, 0, 0, 0,    // PUSH_INT 42
            0x20, 16, 0, 0, 0,    // LOOP_START 16
            0x22,                  // BREAK
            0x01, 99, 0, 0, 0,    // PUSH_INT 99 (dead code)
            0xFF                   // HALT
        ];

        var ctx = new ControlFlowContext(program);
        new ControlFlowTestMachine().Execute(ctx);

        Assert.AreEqual(0, ctx.ControlFlow.Depth);
        Assert.AreEqual(42, (int)ctx.Stack.Peek());
    }

    [TestMethod]
    public void Integration_Throw_JumpsToCatchHandler()
    {
        // 0: TRY 11            [0x30, 11,0,0,0]   catch=11
        // 5: PUSH_INT 7        [0x01, 7,0,0,0]
        //10: THROW             [0x31]              â†’ IP=11
        //11: ENDTRY            [0x32]
        //12: HALT              [0xFF]
        // Expected: ExceptionBlock.ThrownValue = 7 (then ENDTRY pops it)
        byte[] program =
        [
            0x30, 11, 0, 0, 0,   // TRY 11
            0x01, 7, 0, 0, 0,    // PUSH_INT 7
            0x31,                 // THROW
            0x32,                 // ENDTRY
            0xFF                  // HALT
        ];

        var ctx = new ControlFlowContext(program);
        new ControlFlowTestMachine().Execute(ctx);

        Assert.AreEqual(0, ctx.ControlFlow.Depth); // ENDTRY popped the block
    }

    [TestMethod]
    public void Integration_Throw_ThrownValueAccessibleInCatch()
    {
        // Same as above but we inspect ThrownValue inside the catch (before ENDTRY).
        // We use a flag set by verifying the value is 7 at catch time.
        // Implementation: after THROW lands at catch=11, ENDTRY at 11 pops the block.
        // We capture ThrownValue before the full Execute by stepping.
        byte[] program =
        [
            0x30, 11, 0, 0, 0,   // 0: TRY 11
            0x01, 7, 0, 0, 0,    // 5: PUSH_INT 7
            0x31,                 // 10: THROW â†’ IP jumps to 11
            0x32,                 // 11: ENDTRY
            0xFF                  // 12: HALT
        ];

        var machine = new ControlFlowTestMachine();
        var ctx = new ControlFlowContext(program);

        // Step up to and including THROW (instructions 0, 1, 2)
        machine.ExecuteStep(ctx); // TRY
        machine.ExecuteStep(ctx); // PUSH_INT 7
        machine.ExecuteStep(ctx); // THROW â†’ IP=11, ExceptionBlock still on stack

        var ex = (ExceptionBlock)ctx.ControlFlow.CurrentBlock!;
        Assert.AreEqual(7, ex.ThrownValue);
    }

    // â”€â”€ FindEnclosing<T> â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void FindEnclosing_ReturnsNearestBlockOfType_WithoutModifyingStack()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 50);
        cfs.PushConditional(10, 30);
        var loop = cfs.FindEnclosing<LoopBlock>();
        Assert.IsNotNull(loop);
        Assert.AreEqual(0, loop.StartAddress);
        Assert.AreEqual(2, cfs.Depth); // stack unchanged
    }

    [TestMethod]
    public void FindEnclosing_TypeNotPresent_ReturnsNull()
    {
        var cfs = new ControlFlowStack();
        cfs.PushConditional(0, 20);
        Assert.IsNull(cfs.FindEnclosing<LoopBlock>());
    }

    [TestMethod]
    public void FindEnclosing_ReturnsInnermostMatchWhenMultiplePresent()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 100);   // outer loop
        cfs.PushLoop(10, 50);   // inner loop (top of stack)
        var found = cfs.FindEnclosing<LoopBlock>();
        Assert.AreEqual(10, found!.StartAddress);
    }

    [TestMethod]
    public void FindEnclosing_EmptyStack_ReturnsNull()
    {
        var cfs = new ControlFlowStack();
        Assert.IsNull(cfs.FindEnclosing<LoopBlock>());
    }

    // â”€â”€ ControlFlowContext injectable constructor â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void ControlFlowContext_InjectableStack_IsUsed()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 100);
        var ctx = new ControlFlowContext(new byte[0], cfs);
        Assert.AreSame(cfs, ctx.ControlFlow);
        Assert.AreEqual(1, ctx.ControlFlow.Depth);
    }

    [TestMethod]
    public void ControlFlowContext_NullStack_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new ControlFlowContext(new byte[0], null!));
    }

    // â”€â”€ IsEmpty â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void IsEmpty_TrueWhenNoBlocksOpen()
    {
        var cfs = new ControlFlowStack();
        Assert.IsTrue(cfs.IsEmpty);
    }

    [TestMethod]
    public void IsEmpty_FalseAfterPush()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 10);
        Assert.IsFalse(cfs.IsEmpty);
    }

    [TestMethod]
    public void IsEmpty_TrueAfterPopAll()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 10);
        cfs.Pop();
        Assert.IsTrue(cfs.IsEmpty);
    }

    // â”€â”€ Blocks enumerable â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void Blocks_EmptyWhenNoBlocksOpen()
    {
        var cfs = new ControlFlowStack();
        Assert.AreEqual(0, cfs.Blocks.Count());
    }

    [TestMethod]
    public void Blocks_EnumeratesInnermostFirst()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 50);
        cfs.PushConditional(10, 30);

        var blocks = cfs.Blocks.ToArray();
        Assert.AreEqual(2, blocks.Length);
        Assert.IsInstanceOfType<ConditionalBlock>(blocks[0]); // innermost first
        Assert.IsInstanceOfType<LoopBlock>(blocks[1]);
    }

    [TestMethod]
    public void Blocks_IsLiveView_ReflectsSubsequentPush()
    {
        var cfs = new ControlFlowStack();
        var view = cfs.Blocks;
        cfs.PushLoop(0, 10);
        Assert.AreEqual(1, view.Count());
    }

    // â”€â”€ Context.Terminate â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [TestMethod]
    public void Context_Terminate_SetsInstructionPointerToMinusOne()
    {
        var ctx = new ControlFlowContext(new byte[] { 0x01, 0x02, 0x03 });
        ctx.Terminate();
        Assert.AreEqual(-1, ctx.InstructionPointer);
    }

    [TestMethod]
    public void Context_Terminate_StopsExecution()
    {
        // Terminate() before Execute: the PUSH_INT at byte 0 must never run.
        byte[] program = [0x01, 99, 0, 0, 0]; // PUSH_INT 99
        var ctx = new ControlFlowContext(program);
        ctx.Terminate();
        new ControlFlowTestMachine().Execute(ctx);

        Assert.AreEqual(0, ctx.Stack.Count);
    }
}
