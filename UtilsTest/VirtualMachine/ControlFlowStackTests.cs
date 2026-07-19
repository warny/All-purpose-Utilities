using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine;

// â"€â"€ Minimal test VM (opcodes assigned only here, not in the framework) â"€â"€â"€â"€â"€â"€â"€â"€

public class ControlFlowTestMachine : VirtualProcessor<ControlFlowContext>
{
    [Instruction("PUSH_INT", 0x01)]
    void PushInt(ControlFlowContext ctx, int value) => ctx.Stack.Push(value);

    [Instruction("HALT", 0xFF)]
    void Halt(ControlFlowContext ctx) => ctx.InstructionPointer = ctx.Data.Length;

    // â"€â"€ Conditionals â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    /// <summary>
    /// IF_FALSE endAddr [elseAddr] â€" pops a bool; if false jumps to elseAddr (or endAddr).
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

    // â"€â"€ Loops â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    /// <summary>LOOP_START endAddr â€" marks the loop header; execution falls through into body.</summary>
    [Instruction("LOOP_START", 0x20)]
    void LoopStart(ControlFlowContext ctx, int endAddress)
        => ctx.ControlFlow.PushLoop(ctx.InstructionPointer, endAddress);

    /// <summary>LOOP_END â€" jumps back to loop StartAddress without popping the block.</summary>
    [Instruction("LOOP_END", 0x21)]
    void LoopEnd(ControlFlowContext ctx)
        => ctx.InstructionPointer = ((LoopBlock)ctx.ControlFlow.CurrentBlock!).StartAddress;

    [Instruction("BREAK", 0x22)]
    void Break(ControlFlowContext ctx) => ctx.ControlFlow.Break(ctx);

    [Instruction("CONTINUE", 0x23)]
    void Continue(ControlFlowContext ctx) => ctx.ControlFlow.Continue(ctx);

    // â"€â"€ Exceptions â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    /// <summary>TRY catchAddr — opens an ExceptionBlock with the given catch handler address.</summary>
    [Instruction("TRY", 0x30)]
    void Try(ControlFlowContext ctx, int catchAddress)
        => ctx.ControlFlow.PushException(ctx.InstructionPointer, catchAddress, null);

    /// <summary>THROW — pops a value from the operand stack and throws it.</summary>
    [Instruction("THROW", 0x31)]
    void Throw(ControlFlowContext ctx)
    {
        var value = ctx.Stack.Pop();
        if (!ctx.ControlFlow.Throw(ctx, value))
            throw new InvalidOperationException($"Unhandled throw: {value}");
    }

    /// <summary>ENDTRY — closes the exception block.</summary>
    [Instruction("ENDTRY", 0x32)]
    void EndTry(ControlFlowContext ctx) => ctx.ControlFlow.Pop();

    /// <summary>TRY_FULL catchAddr finallyAddr — opens an ExceptionBlock with both catch and finally addresses.</summary>
    [Instruction("TRY_FULL", 0x33)]
    void TryFull(ControlFlowContext ctx, int catchAddress, int finallyAddress)
        => ctx.ControlFlow.PushException(ctx.InstructionPointer, catchAddress, finallyAddress);

    /// <summary>TRY_FINALLY finallyAddr — opens an ExceptionBlock with only a finally address.</summary>
    [Instruction("TRY_FINALLY", 0x35)]
    void TryFinally(ControlFlowContext ctx, int finallyAddress)
        => ctx.ControlFlow.PushException(ctx.InstructionPointer, null, finallyAddress);

    /// <summary>ENDFINALLY — delegates to ControlFlowStack.EndFinally; jumps to pending catch or pops block.</summary>
    [Instruction("ENDFINALLY", 0x34)]
    void EndFinally(ControlFlowContext ctx) => ctx.ControlFlow.EndFinally(ctx);
}

// â"€â"€ Unit tests â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

[TestClass]
public class ControlFlowStackTests
{
    // â"€â"€ ConditionalBlock â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

    // â"€â"€ LoopBlock â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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
    public void Loop_MultipleIterations_ControlFlowDepthRemainsConstant()
    {
        // Verify item 42: StartAddress points into the loop body (not the LOOP instruction),
        // so iterating via CONTINUE does not push additional blocks.
        var cfs = new ControlFlowStack();
        cfs.PushLoop(startAddress: 10, endAddress: 100); // body starts at 10

        int depthAfterPush = cfs.Depth;

        var ctx = new DefaultContext(ReadOnlyMemory<byte>.Empty);
        for (int i = 0; i < 5; i++)
        {
            cfs.Continue(ctx); // jumps to StartAddress, block stays on stack
            Assert.AreEqual(depthAfterPush, cfs.Depth, $"Depth changed on iteration {i}");
        }
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

    // â"€â"€ ExceptionBlock â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

    // â"€â"€ Pop underflow â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    [TestMethod]
    public void Pop_EmptyStack_Throws()
    {
        var cfs = new ControlFlowStack();
        Assert.ThrowsException<InvalidOperationException>(() => cfs.Pop());
    }

    // â"€â"€ Typed Pop<T> â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

    // ── Item 12: Break/Continue are transactional — no stack corruption on failure ──

    [TestMethod]
    public void Break_OutsideLoop_StackUnchanged()
    {
        // The stack must be intact after BREAK throws when no loop is in scope.
        var cfs = new ControlFlowStack();
        cfs.PushConditional(0, 10);
        cfs.PushConditional(5, 8);
        var ctx = new ControlFlowContext(new byte[0]);
        Assert.ThrowsException<InvalidOperationException>(() => cfs.Break(ctx));
        Assert.AreEqual(2, cfs.Depth); // both blocks must still be present
    }

    [TestMethod]
    public void Continue_OutsideLoop_StackUnchanged()
    {
        var cfs = new ControlFlowStack();
        cfs.PushConditional(0, 10);
        var ctx = new ControlFlowContext(new byte[0]);
        Assert.ThrowsException<InvalidOperationException>(() => cfs.Continue(ctx));
        Assert.AreEqual(1, cfs.Depth);
    }

    [TestMethod]
    public void Break_NestedInsideException_PopsBothAndJumps()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 50);
        cfs.PushException(5, catchAddress: 40, finallyAddress: null);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Break(ctx);
        Assert.AreEqual(50, ctx.InstructionPointer);
        Assert.AreEqual(0, cfs.Depth);
    }

    // â"€â"€ FullContext â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

    // â"€â"€ Integration: BREAK exits loop, PUSH_INT 99 is never reached â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

    // â"€â"€ FindEnclosing<T> â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

    // â"€â"€ ControlFlowContext injectable constructor â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

    // â"€â"€ IsEmpty â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

    // â"€â"€ Blocks enumerable â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

    // â"€â"€ Context.Terminate â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

    // ── Finally semantics ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Throw_WithBothCatchAndFinally_JumpsToFinallyFirst()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: 20, finallyAddress: 10);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Throw(ctx, "error");
        Assert.AreEqual(10, ctx.InstructionPointer);
    }

    [TestMethod]
    public void Throw_WithBothCatchAndFinally_SetsPendingCatchAddress()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: 20, finallyAddress: 10);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Throw(ctx, "error");
        var ex = (ExceptionBlock)cfs.CurrentBlock!;
        Assert.AreEqual(20, ex.PendingCatchAddress);
    }

    [TestMethod]
    public void Throw_WithFinallyOnly_JumpsToFinally_NoPendingCatch()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: null, finallyAddress: 10);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Throw(ctx, "error");
        Assert.AreEqual(10, ctx.InstructionPointer);
        var ex = (ExceptionBlock)cfs.CurrentBlock!;
        Assert.IsNull(ex.PendingCatchAddress);
    }

    [TestMethod]
    public void Throw_WithCatchOnly_JumpsToCatchDirectly_NoPendingCatch()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: 42, finallyAddress: null);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Throw(ctx, "error");
        Assert.AreEqual(42, ctx.InstructionPointer);
        var ex = (ExceptionBlock)cfs.CurrentBlock!;
        Assert.IsNull(ex.PendingCatchAddress);
    }

    [TestMethod]
    public void EndFinally_WithPendingCatch_RedirectsToCatch_ReturnsTrue()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: 20, finallyAddress: 10);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Throw(ctx, "error");       // IP → 10, PendingCatch = 20
        bool hasCatch = cfs.EndFinally(ctx); // IP → 20, PendingCatch cleared
        Assert.IsTrue(hasCatch);
        Assert.AreEqual(20, ctx.InstructionPointer);
        Assert.AreEqual(1, cfs.Depth); // block stays on stack until ENDTRY
        Assert.IsNull(((ExceptionBlock)cfs.CurrentBlock!).PendingCatchAddress);
    }

    [TestMethod]
    public void EndFinally_NoPendingCatch_PopsBlock_ReturnsFalse()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: null, finallyAddress: 10);
        var ctx = new ControlFlowContext(new byte[0]);
        cfs.Throw(ctx, "error");        // IP → 10, PendingCatch = null
        bool hasCatch = cfs.EndFinally(ctx); // pops block
        Assert.IsFalse(hasCatch);
        Assert.AreEqual(0, cfs.Depth);
    }

    [TestMethod]
    public void EndFinally_OutsideExceptionBlock_Throws()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 10);
        var ctx = new ControlFlowContext(new byte[0]);
        Assert.ThrowsException<InvalidOperationException>(() => cfs.EndFinally(ctx));
    }

    [TestMethod]
    public void EndFinally_EmptyStack_Throws()
    {
        var cfs = new ControlFlowStack();
        var ctx = new ControlFlowContext(new byte[0]);
        Assert.ThrowsException<InvalidOperationException>(() => cfs.EndFinally(ctx));
    }

    [TestMethod]
    public void Integration_ThrowWithCatchAndFinally_RunsFinallyThenCatch()
    {
        // Layout (all addresses in decimal):
        //  0: TRY_FULL catch=25 finally=17   [0x33, 25,0,0,0, 17,0,0,0]  (9 bytes)
        //  9: PUSH_INT 42                     [0x01, 42,0,0,0]            (5 bytes)
        // 14: THROW                           [0x31]                      (1 byte)
        // 15: PUSH_INT 99 (dead code)         [0x01, 99,0,0,0]
        // 20: HALT (dead code)                [0xFF]
        // --- finally block ---
        // 21: PUSH_INT 1 (finally marker)     [0x01, 1,0,0,0]
        // 26: ENDFINALLY                      [0x34]                      → jumps to catch=25... wait, let me re-count

        // Let me re-layout carefully:
        //  0: TRY_FULL  [0x33] + int catchAddr(4) + int finallyAddr(4) = 9 bytes
        //  9: PUSH_INT  [0x01] + int(4) = 5 bytes  → address 14
        // 14: THROW     [0x31] = 1 byte             → address 15
        // 15: PUSH_INT 99 (dead)  [0x01, 99,0,0,0] = 5 bytes → address 20
        // 20: HALT (dead) [0xFF]  = 1 byte           → address 21
        // 21: PUSH_INT 1 (finally marker) [0x01, 1,0,0,0] = 5 bytes → address 26
        // 26: ENDFINALLY [0x34] = 1 byte             → address 27
        // 27: PUSH_INT 2 (catch marker) [0x01, 2,0,0,0] = 5 bytes → address 32
        // 32: ENDTRY [0x32] = 1 byte                 → address 33
        // 33: HALT [0xFF]

        // catchAddress = 27, finallyAddress = 21
        byte[] program =
        [
            0x33, 27, 0, 0, 0, 21, 0, 0, 0,  //  0: TRY_FULL catch=27 finally=21
            0x01, 42, 0, 0, 0,               //  9: PUSH_INT 42
            0x31,                            // 14: THROW → IP=21 (finally)
            0x01, 99, 0, 0, 0,              // 15: PUSH_INT 99 (dead)
            0xFF,                            // 20: HALT (dead)
            0x01, 1, 0, 0, 0,              // 21: PUSH_INT 1  (finally runs)
            0x34,                            // 26: ENDFINALLY → IP=27 (catch)
            0x01, 2, 0, 0, 0,              // 27: PUSH_INT 2  (catch runs)
            0x32,                            // 32: ENDTRY
            0xFF                             // 33: HALT
        ];

        var ctx = new ControlFlowContext(program);
        new ControlFlowTestMachine().Execute(ctx);

        // THROW pops 42 from the operand stack and stores it in ExceptionBlock.ThrownValue.
        // Stack after: 1 (finally marker), then 2 (catch marker) = [2, 1] in ToArray() order.
        Assert.AreEqual(0, ctx.ControlFlow.Depth);
        var stack = ctx.Stack.ToArray(); // top is index 0
        Assert.AreEqual(2, stack.Length);
        Assert.AreEqual(2, (int)stack[0]); // catch marker (top)
        Assert.AreEqual(1, (int)stack[1]); // finally marker
    }

    [TestMethod]
    public void EndFinally_FinallyOnly_ExceptionInFlight_PropagatesThrowToOuterHandler()
    {
        // Inner try/finally-only sits inside outer try/catch.
        // After THROW lands in the inner finally, ENDFINALLY should propagate to the outer catch.
        var cfs = new ControlFlowStack();
        var ctx = new ControlFlowContext(new byte[50]);
        cfs.PushException(startAddress: 0, catchAddress: 40, finallyAddress: null); // outer try/catch
        cfs.PushException(startAddress: 5, catchAddress: null, finallyAddress: 20); // inner try/finally-only

        bool thrown = cfs.Throw(ctx, "error");
        Assert.IsTrue(thrown);
        Assert.AreEqual(20, ctx.InstructionPointer); // redirected to inner finally

        // ENDFINALLY — should propagate exception to outer catch
        bool result = cfs.EndFinally(ctx);
        Assert.IsTrue(result);
        Assert.AreEqual(40, ctx.InstructionPointer); // redirected to outer catch
        Assert.AreEqual(1, cfs.Depth);               // outer block remains
    }

    [TestMethod]
    public void EndFinally_FinallyOnly_NoExceptionInFlight_PopsBlock()
    {
        // Finally entered normally (no throw) — ENDFINALLY should just pop and return false.
        var cfs = new ControlFlowStack();
        var ctx = new ControlFlowContext(new byte[50]);
        cfs.PushException(startAddress: 0, catchAddress: null, finallyAddress: 20);

        // Manually jump into the finally without going through Throw (no exception in flight).
        ctx.InstructionPointer = 20;

        bool result = cfs.EndFinally(ctx);
        Assert.IsFalse(result);
        Assert.AreEqual(0, cfs.Depth);
        Assert.AreEqual(20, ctx.InstructionPointer); // unchanged
    }

    [TestMethod]
    public void Integration_ThrowInFinallyOnly_PropagatesThrowToOuterCatch()
    {
        // Layout:
        //  0: TRY catch=28       [0x30, 28,0,0,0]              5 bytes
        //  5: TRY_FINALLY f=22   [0x35, 22,0,0,0]              5 bytes → addr 10
        // 10: PUSH_INT 99        [0x01, 99,0,0,0]              5 bytes → addr 15
        // 15: THROW              [0x31]                         1 byte  → addr 16 (dead)
        // 16: PUSH_INT 0 (dead)  [0x01, 0,0,0,0]               5 bytes → addr 21
        // 21: HALT (dead)        [0xFF]                         1 byte  → addr 22
        // 22: PUSH_INT 1 (finally marker) [0x01, 1,0,0,0]      5 bytes → addr 27
        // 27: ENDFINALLY         [0x34]    → propagates to outer catch → IP=28
        // 28: PUSH_INT 2 (catch marker)   [0x01, 2,0,0,0]      5 bytes → addr 33
        // 33: ENDTRY             [0x32]                         1 byte  → addr 34
        // 34: HALT               [0xFF]
        byte[] program =
        [
            0x30, 28, 0, 0, 0,        //  0: TRY catch=28
            0x35, 22, 0, 0, 0,        //  5: TRY_FINALLY finally=22
            0x01, 99, 0, 0, 0,        // 10: PUSH_INT 99
            0x31,                      // 15: THROW → IP=22 (inner finally)
            0x01, 0, 0, 0, 0,         // 16: dead
            0xFF,                      // 21: HALT (dead)
            0x01, 1, 0, 0, 0,         // 22: PUSH_INT 1 (finally marker)
            0x34,                      // 27: ENDFINALLY → propagates to outer catch → IP=28
            0x01, 2, 0, 0, 0,         // 28: PUSH_INT 2 (catch marker)
            0x32,                      // 33: ENDTRY
            0xFF                       // 34: HALT
        ];

        var ctx = new ControlFlowContext(program);
        new ControlFlowTestMachine().Execute(ctx);

        Assert.AreEqual(0, ctx.ControlFlow.Depth);
        var stack = ctx.Stack.ToArray(); // top is index 0
        Assert.AreEqual(2, stack.Length);
        Assert.AreEqual(2, (int)stack[0]); // catch marker (top)
        Assert.AreEqual(1, (int)stack[1]); // finally marker
    }

    [TestMethod]
    public void Integration_ThrowWithFinallyOnly_RunsFinally()
    {
        // Layout:
        //  0: TRY_FINALLY finallyAddr=14  [0x35, 14,0,0,0]  (5 bytes)
        //  5: PUSH_INT 7                  [0x01, 7,0,0,0]   (5 bytes) → addr 10
        // 10: THROW                       [0x31]             (1 byte)  → addr 11
        // 11: PUSH_INT 99 (dead)          [0x01, 99,0,0,0]             → addr 16
        // 16: HALT (dead)                 [0xFF]                        → addr 17 (dead)
        // 17: PUSH_INT 1 (finally marker) [0x01, 1,0,0,0]             (5 bytes) → addr 22
        //     wait, finallyAddr should be 17... but THROW at addr 10 checks PushException with finallyAddr.
        // Let me recalculate with TRY_FINALLY having only one int parameter:
        //  0: TRY_FINALLY [0x35] + int(4) = 5 bytes
        //  5: PUSH_INT 7  [0x01, 7,0,0,0] = 5 bytes → addr 10
        // 10: THROW [0x31] = 1 byte → addr 11 (dead)
        // 11: PUSH_INT 99 (dead) [0x01, 99,0,0,0] → addr 16
        // 16: HALT (dead) [0xFF] → addr 17
        // 17: PUSH_INT 1 (finally marker) [0x01, 1,0,0,0] → addr 22
        // 22: ENDFINALLY [0x34] → pops block (no catch) → addr 23
        // 23: HALT [0xFF]
        byte[] program =
        [
            0x35, 17, 0, 0, 0,           //  0: TRY_FINALLY finally=17
            0x01, 7, 0, 0, 0,            //  5: PUSH_INT 7
            0x31,                         // 10: THROW → IP=17 (finally)
            0x01, 99, 0, 0, 0,           // 11: PUSH_INT 99 (dead)
            0xFF,                         // 16: HALT (dead)
            0x01, 1, 0, 0, 0,           // 17: PUSH_INT 1 (finally runs)
            0x34,                         // 22: ENDFINALLY → pops block
            0xFF                          // 23: HALT
        ];

        var ctx = new ControlFlowContext(program);
        new ControlFlowTestMachine().Execute(ctx);

        // THROW pops 7 from the operand stack; it is stored in ExceptionBlock.ThrownValue,
        // not on the operand stack. Only the finally marker (1) remains.
        Assert.AreEqual(0, ctx.ControlFlow.Depth);
        var stack = ctx.Stack.ToArray();
        Assert.AreEqual(1, stack.Length);
        Assert.AreEqual(1, (int)stack[0]); // finally marker
    }

    // ── Item 28: throw inside catch/finally propagates to outer handler ───────

    [TestMethod]
    public void Throw_InsideCatch_PropagatestoOuterHandler()
    {
        // Inner try/catch, outer try/catch.
        // The inner catch re-throws; the outer catch must receive the new exception.
        var cfs = new ControlFlowStack();
        var ctx = new ControlFlowContext(new byte[100]);

        cfs.PushException(startAddress: 0, catchAddress: 50, finallyAddress: null); // outer
        cfs.PushException(startAddress: 5, catchAddress: 20, finallyAddress: null); // inner

        // First throw: inner block handles it (phase transitions to Catch).
        cfs.Throw(ctx, "error1");
        Assert.AreEqual(20, ctx.InstructionPointer); // inner catch
        var inner = (ExceptionBlock)cfs.CurrentBlock!;
        Assert.AreEqual(ExceptionBlockPhase.Catch, inner.Phase);

        // Second throw from inside the catch: inner block is now in Catch phase → skip it.
        // Must propagate to the outer catch.
        bool handled = cfs.Throw(ctx, "error2");
        Assert.IsTrue(handled);
        Assert.AreEqual(50, ctx.InstructionPointer); // outer catch
    }

    [TestMethod]
    public void Throw_InsideFinally_PropagatestoOuterHandler()
    {
        var cfs = new ControlFlowStack();
        var ctx = new ControlFlowContext(new byte[100]);

        cfs.PushException(startAddress: 0, catchAddress: 60, finallyAddress: null); // outer
        cfs.PushException(startAddress: 5, catchAddress: null, finallyAddress: 30); // inner (finally-only)

        // First throw: inner block's finally handles it (phase → Finally).
        cfs.Throw(ctx, "error1");
        Assert.AreEqual(30, ctx.InstructionPointer); // inner finally
        var inner = (ExceptionBlock)cfs.CurrentBlock!;
        Assert.AreEqual(ExceptionBlockPhase.Finally, inner.Phase);

        // Throw from inside the finally: inner block in Finally phase → skip.
        bool handled = cfs.Throw(ctx, "error2");
        Assert.IsTrue(handled);
        Assert.AreEqual(60, ctx.InstructionPointer); // outer catch
    }

    [TestMethod]
    public void ExceptionBlock_Phase_StartsAsTry()
    {
        var block = new ExceptionBlock(0, catchAddress: 10, finallyAddress: null);
        Assert.AreEqual(ExceptionBlockPhase.Try, block.Phase);
    }

    // ── Item 13: PushException rejects blocks with neither catch nor finally ──

    [TestMethod]
    public void PushException_NoCatchNoFinally_ThrowsArgumentException()
    {
        var cfs = new ControlFlowStack();
        Assert.ThrowsException<ArgumentException>(
            () => cfs.PushException(startAddress: 0, catchAddress: null, finallyAddress: null));
    }

    [TestMethod]
    public void PushException_CatchOnly_DoesNotThrow()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(startAddress: 0, catchAddress: 10, finallyAddress: null);
        Assert.IsNotNull(cfs.CurrentBlock);
    }

    [TestMethod]
    public void PushException_FinallyOnly_DoesNotThrow()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(startAddress: 0, catchAddress: null, finallyAddress: 20);
        Assert.IsNotNull(cfs.CurrentBlock);
    }

    [TestMethod]
    public void Throw_CatchOnlyBlock_JumpsToCatch()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(startAddress: 0, catchAddress: 10, finallyAddress: null);
        var ctx = new DefaultContext(ReadOnlyMemory<byte>.Empty);
        bool handled = cfs.Throw(ctx, "err");
        Assert.IsTrue(handled);
        Assert.AreEqual(10, ctx.InstructionPointer);
    }

    // ── Null context guards (item 37) ────────────────────────────────────────────────────────

    [TestMethod]
    public void Break_NullContext_ThrowsArgumentNullException()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 10);
        Assert.ThrowsException<ArgumentNullException>(() => cfs.Break(null!));
    }

    [TestMethod]
    public void Continue_NullContext_ThrowsArgumentNullException()
    {
        var cfs = new ControlFlowStack();
        cfs.PushLoop(0, 10);
        Assert.ThrowsException<ArgumentNullException>(() => cfs.Continue(null!));
    }

    [TestMethod]
    public void EndFinally_NullContext_ThrowsArgumentNullException()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: null, finallyAddress: 20);
        Assert.ThrowsException<ArgumentNullException>(() => cfs.EndFinally(null!));
    }

    [TestMethod]
    public void Throw_NullContext_ThrowsArgumentNullException()
    {
        var cfs = new ControlFlowStack();
        cfs.PushException(0, catchAddress: 10, finallyAddress: null);
        Assert.ThrowsException<ArgumentNullException>(() => cfs.Throw(null!, "error"));
    }

    // ── MaxDepth (item 14) ────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void MaxDepth_Default_Is1024()
    {
        var cfs = new ControlFlowStack();
        Assert.AreEqual(1024, cfs.MaxDepth);
        Assert.AreEqual(ControlFlowStack.DefaultMaxDepth, cfs.MaxDepth);
    }

    [TestMethod]
    public void Constructor_InvalidMaxDepth_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ControlFlowStack(0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ControlFlowStack(-1));
    }

    [TestMethod]
    public void PushConditional_BeyondMaxDepth_ThrowsInvalidOperationException()
    {
        var cfs = new ControlFlowStack(maxDepth: 2);
        cfs.PushConditional(0, 10);
        cfs.PushConditional(1, 20);
        Assert.ThrowsException<InvalidOperationException>(() => cfs.PushConditional(2, 30));
    }

    [TestMethod]
    public void PushLoop_BeyondMaxDepth_ThrowsInvalidOperationException()
    {
        var cfs = new ControlFlowStack(maxDepth: 1);
        cfs.PushLoop(0, 10);
        Assert.ThrowsException<InvalidOperationException>(() => cfs.PushLoop(1, 20));
    }

    [TestMethod]
    public void PushException_BeyondMaxDepth_ThrowsInvalidOperationException()
    {
        var cfs = new ControlFlowStack(maxDepth: 1);
        cfs.PushException(0, catchAddress: 10, finallyAddress: null);
        Assert.ThrowsException<InvalidOperationException>(
            () => cfs.PushException(1, catchAddress: 20, finallyAddress: null));
    }

    [TestMethod]
    public void CustomMaxDepth_AfterPop_AllowsPushAgain()
    {
        var cfs = new ControlFlowStack(maxDepth: 1);
        cfs.PushLoop(0, 10);
        cfs.Pop();
        cfs.PushLoop(0, 10); // should not throw after pop freed a slot
        Assert.AreEqual(1, cfs.Depth);
    }
}
