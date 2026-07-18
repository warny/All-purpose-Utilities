using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine;

// â"€â"€ Test processor wiring CALL/RET against CallStackContext â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

public class CallMachine : VirtualProcessor<CallStackContext>
{
    /// <summary>Pushes an integer operand onto the operand stack.</summary>
    [Instruction("PUSH_INT", 0x01)]
    void PushInt(CallStackContext ctx, int value) => ctx.Stack.Push(value);

    /// <summary>Jumps to <paramref name="target"/>; saves the post-instruction IP as return address.</summary>
    [Instruction("CALL", 0xC0)]
    void Call(CallStackContext ctx, int target)
    {
        ctx.CallStack.Call(ctx.InstructionPointer);
        ctx.InstructionPointer = target;
    }

    /// <summary>
    /// Returns to the address saved by the most recent CALL.
    /// When the call stack is empty, Return() yields -1, which terminates the Execute loop.
    /// </summary>
    [Instruction("RET", 0xC1)]
    void Ret(CallStackContext ctx) => ctx.InstructionPointer = ctx.CallStack.Return();

    /// <summary>Halts execution by advancing the instruction pointer past the end of the stream.</summary>
    [Instruction("HALT", 0xFF)]
    void Halt(CallStackContext ctx) => ctx.InstructionPointer = ctx.Data.Length;
}

// â"€â"€ Unit tests â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

[TestClass]
public class CallStackTests
{
    // â"€â"€ ICallStack contract: CallStack â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    [TestMethod]
    public void CallStack_Call_IncrementsDepth()
    {
        var cs = new CallStack();
        Assert.AreEqual(0, cs.Depth);
        cs.Call(10);
        Assert.AreEqual(1, cs.Depth);
        cs.Call(20);
        Assert.AreEqual(2, cs.Depth);
    }

    [TestMethod]
    public void CallStack_Return_RestoresAddressLIFO()
    {
        var cs = new CallStack();
        cs.Call(10);
        cs.Call(20);
        Assert.AreEqual(20, cs.Return());
        Assert.AreEqual(10, cs.Return());
    }

    [TestMethod]
    public void CallStack_IsEmpty_TrueWhenEmpty()
    {
        var cs = new CallStack();
        Assert.IsTrue(cs.IsEmpty);
        cs.Call(5);
        Assert.IsFalse(cs.IsEmpty);
        cs.Return();
        Assert.IsTrue(cs.IsEmpty);
    }

    [TestMethod]
    public void CallStack_Return_OnEmpty_ReturnsMinusOne()
    {
        var cs = new CallStack();
        Assert.AreEqual(-1, cs.Return());
    }

    [TestMethod]
    public void CallStack_Return_OnEmpty_DoesNotAlterDepth()
    {
        var cs = new CallStack();
        cs.Return();
        Assert.AreEqual(0, cs.Depth);
    }

    [TestMethod]
    public void CallStack_MaxDepth_Overflow_Throws()
    {
        var cs = new CallStack(maxDepth: 3);
        cs.Call(1);
        cs.Call(2);
        cs.Call(3);
        Assert.ThrowsException<InvalidOperationException>(() => cs.Call(4));
    }

    [TestMethod]
    public void CallStack_MaxDepth_SetBelowOne_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CallStack(0));
    }

    [TestMethod]
    public void CallStack_MaxDepth_IsImmutable()
    {
        var cs = new CallStack(maxDepth: 10);
        Assert.AreEqual(10, cs.MaxDepth);
    }

    [TestMethod]
    public void CallStack_CurrentFrame_IsNullWhenEmpty()
    {
        var cs = new CallStack();
        Assert.IsNull(cs.CurrentFrame);
    }

    [TestMethod]
    public void CallStack_CurrentFrame_ReflectsTopFrame()
    {
        var cs = new CallStack();
        cs.Call(99);
        Assert.IsNotNull(cs.CurrentFrame);
        Assert.AreEqual(99, cs.CurrentFrame!.ReturnAddress);
    }

    // â"€â"€ CallFrame local variables â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    [TestMethod]
    public void CallFrame_SetLocal_CanBeRetrievedTyped()
    {
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("x", 42);
        Assert.IsTrue(cs.CurrentFrame.TryGetLocal<int>("x", out int v));
        Assert.AreEqual(42, v);
    }

    [TestMethod]
    public void CallFrame_TryGetLocal_WrongType_ReturnsFalse()
    {
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("x", 42);
        Assert.IsFalse(cs.CurrentFrame.TryGetLocal<string>("x", out _));
    }

    [TestMethod]
    public void CallFrame_TryGetLocal_Missing_ReturnsFalse()
    {
        var cs = new CallStack();
        cs.Call(0);
        Assert.IsFalse(cs.CurrentFrame!.TryGetLocal<int>("y", out _));
    }

    [TestMethod]
    public void CallFrame_Locals_ReflectsAllStoredValues()
    {
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("a", 1);
        cs.CurrentFrame.SetLocal("b", "hello");
        Assert.AreEqual(2, cs.CurrentFrame.Locals.Count);
    }

    [TestMethod]
    public void CallFrame_Locals_ArePerFrame()
    {
        var cs = new CallStack();
        cs.Call(10);
        cs.CurrentFrame!.SetLocal("x", 1);
        cs.Call(20);
        cs.CurrentFrame!.SetLocal("x", 2);

        Assert.IsTrue(cs.CurrentFrame.TryGetLocal<int>("x", out int top));
        Assert.AreEqual(2, top);

        cs.Return();
        Assert.IsTrue(cs.CurrentFrame!.TryGetLocal<int>("x", out int prev));
        Assert.AreEqual(1, prev);
    }

    // â"€â"€ ICallStack.CurrentFrame via SimpleCallStack â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    [TestMethod]
    public void SimpleCallStack_CurrentFrame_AlwaysNull()
    {
        ICallStack cs = new SimpleCallStack();
        cs.Call(42);
        Assert.IsNull(cs.CurrentFrame);
    }

    // â"€â"€ CallFrame.GetLocal<T> throwing variant â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    [TestMethod]
    public void CallFrame_GetLocal_ReturnsTypedValue()
    {
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("x", 99);
        Assert.AreEqual(99, cs.CurrentFrame.GetLocal<int>("x"));
    }

    [TestMethod]
    public void CallFrame_GetLocal_MissingKey_ThrowsKeyNotFoundException()
    {
        var cs = new CallStack();
        cs.Call(0);
        Assert.ThrowsException<KeyNotFoundException>(() => cs.CurrentFrame!.GetLocal<int>("missing"));
    }

    [TestMethod]
    public void CallFrame_GetLocal_WrongType_ThrowsInvalidCastException_Legacy()
    {
        // Previously threw KeyNotFoundException; now throws InvalidCastException to
        // distinguish a type mismatch from a missing key.
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("x", 42);
        Assert.ThrowsException<InvalidCastException>(() => cs.CurrentFrame.GetLocal<string>("x"));
    }

    // â"€â"€ ICallStack contract: SimpleCallStack â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    [TestMethod]
    public void SimpleCallStack_Call_IncrementsDepth()
    {
        var cs = new SimpleCallStack();
        cs.Call(10);
        Assert.AreEqual(1, cs.Depth);
    }

    [TestMethod]
    public void SimpleCallStack_Return_RestoresAddressLIFO()
    {
        var cs = new SimpleCallStack();
        cs.Call(10);
        cs.Call(20);
        Assert.AreEqual(20, cs.Return());
        Assert.AreEqual(10, cs.Return());
    }

    [TestMethod]
    public void SimpleCallStack_Return_OnEmpty_ReturnsMinusOne()
    {
        var cs = new SimpleCallStack();
        Assert.AreEqual(-1, cs.Return());
    }

    [TestMethod]
    public void SimpleCallStack_MaxDepth_Overflow_Throws()
    {
        var cs = new SimpleCallStack(maxDepth: 2);
        cs.Call(1);
        cs.Call(2);
        Assert.ThrowsException<InvalidOperationException>(() => cs.Call(3));
    }

    [TestMethod]
    public void SimpleCallStack_MaxDepth_SetBelowOne_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SimpleCallStack(0));
    }

    [TestMethod]
    public void SimpleCallStack_MaxDepth_IsImmutable()
    {
        var cs = new SimpleCallStack(maxDepth: 5);
        Assert.AreEqual(5, cs.MaxDepth);
    }

    // â"€â"€ CallStackContext â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    [TestMethod]
    public void CallStackContext_DefaultCtor_UsesCallStack()
    {
        var ctx = new CallStackContext(new byte[0]);
        Assert.IsInstanceOfType<CallStack>(ctx.CallStack);
    }

    [TestMethod]
    public void CallStackContext_CustomCtor_UsesProvidedStack()
    {
        var simple = new SimpleCallStack();
        var ctx = new CallStackContext(new byte[0], simple);
        Assert.AreSame(simple, ctx.CallStack);
    }

    [TestMethod]
    public void CallStackContext_CustomCtor_NullStack_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new CallStackContext(new byte[0], null!));
    }

    // â"€â"€ Integration: CALL / RET in a VirtualProcessor â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

    [TestMethod]
    public void Integration_Call_JumpsToSubroutine_ReturnRestoresIP()
    {
        // Layout (little-endian, 14 bytes total):
        //   0: CALL 0xC0
        //   1-4: target = 8 (jump to subroutine)
        //        â†’ after operand read: IP=5, return addr = 5
        //   5: HALT 0xFF
        //   6-7: (padding, never reached)
        //   8: PUSH_INT 0x01
        //   9-12: 99 (int operand)
        //   13: RET 0xC1  â†’ pops 5, sets IP=5 â†’ HALT
        byte[] program =
        [
            0xC0, 8, 0, 0, 0,   // CALL 8  (bytes 0-4)
            0xFF,                // HALT    (byte 5)
            0, 0,                // padding (bytes 6-7)
            0x01, 99, 0, 0, 0,  // PUSH_INT 99 (bytes 8-12)
            0xC1                 // RET (byte 13)
        ];

        var ctx = new CallStackContext(program);
        new CallMachine().Execute(ctx);

        Assert.AreEqual(0, ctx.CallStack.Depth);
        Assert.AreEqual(99, (int)ctx.Stack.Peek());
    }

    [TestMethod]
    public void Integration_NestedCalls_UnwindCorrectly()
    {
        // Layout:
        //   0: CALL â†’ 10    (return to 5)
        //   5: HALT
        //   6-9: padding
        //  10: CALL â†’ 20   (return to 15)
        //  15: RET          (return to 5)
        //  16-19: padding
        //  20: PUSH_INT 7
        //  25: RET          (return to 15)
        byte[] program =
        [
            0xC0, 10, 0, 0, 0,  // 0:  CALL 10
            0xFF,                // 5:  HALT
            0, 0, 0, 0,          // 6-9: padding
            0xC0, 20, 0, 0, 0,  // 10: CALL 20
            0xC1,                // 15: RET
            0, 0, 0, 0,          // 16-19: padding
            0x01, 7, 0, 0, 0,   // 20: PUSH_INT 7
            0xC1                 // 25: RET
        ];

        var ctx = new CallStackContext(program);
        new CallMachine().Execute(ctx);

        Assert.AreEqual(0, ctx.CallStack.Depth);
        Assert.AreEqual(7, (int)ctx.Stack.Peek());
    }

    [TestMethod]
    public void Integration_SimpleCallStack_WorksViaCtor()
    {
        byte[] program =
        [
            0xC0, 8, 0, 0, 0,
            0xFF,
            0, 0,
            0x01, 55, 0, 0, 0,
            0xC1
        ];

        var ctx = new CallStackContext(program, new SimpleCallStack());
        new CallMachine().Execute(ctx);

        Assert.AreEqual(55, (int)ctx.Stack.Peek());
    }

    [TestMethod]
    public void Integration_Ret_OnEmptyStack_TerminatesExecution()
    {
        // RET with an empty call stack yields IP = -1; the Execute loop stops.
        // Program: RET (0xC1) â€" no prior CALL.
        byte[] program = [0xC1];
        var ctx = new CallStackContext(program);
        new CallMachine().Execute(ctx);

        Assert.AreEqual(-1, ctx.InstructionPointer);
        Assert.AreEqual(0, ctx.CallStack.Depth);
    }

    // ── Item 35: TryGetLocal handles null values correctly ────────────────

    [TestMethod]
    public void CallFrame_TryGetLocal_NullValue_ReferenceType_ReturnsTrue()
    {
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("s", (string?)null);
        Assert.IsTrue(cs.CurrentFrame.TryGetLocal<string>("s", out string? v));
        Assert.IsNull(v);
    }

    [TestMethod]
    public void CallFrame_TryGetLocal_NullValue_NullableInt_ReturnsTrue()
    {
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("n", (int?)null);
        Assert.IsTrue(cs.CurrentFrame.TryGetLocal<int?>("n", out int? v));
        Assert.IsNull(v);
    }

    [TestMethod]
    public void CallFrame_TryGetLocal_NullValue_NonNullableInt_ReturnsFalse()
    {
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("n", null);
        Assert.IsFalse(cs.CurrentFrame.TryGetLocal<int>("n", out _));
    }

    [TestMethod]
    public void CallFrame_ContainsLocal_PresentKey_ReturnsTrue()
    {
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("x", 1);
        Assert.IsTrue(cs.CurrentFrame.ContainsLocal("x"));
    }

    [TestMethod]
    public void CallFrame_ContainsLocal_AbsentKey_ReturnsFalse()
    {
        var cs = new CallStack();
        cs.Call(0);
        Assert.IsFalse(cs.CurrentFrame!.ContainsLocal("missing"));
    }

    [TestMethod]
    public void CallFrame_GetLocal_NullStoredValue_ThrowsInvalidCastForNonNullableValueType()
    {
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("n", null);
        Assert.ThrowsException<InvalidCastException>(
            () => cs.CurrentFrame.GetLocal<int>("n"));
    }

    [TestMethod]
    public void CallFrame_GetLocal_AbsentKey_ThrowsKeyNotFoundException()
    {
        var cs = new CallStack();
        cs.Call(0);
        Assert.ThrowsException<KeyNotFoundException>(
            () => cs.CurrentFrame!.GetLocal<int>("missing"));
    }

    [TestMethod]
    public void CallFrame_GetLocal_WrongType_ThrowsInvalidCastException()
    {
        var cs = new CallStack();
        cs.Call(0);
        cs.CurrentFrame!.SetLocal("x", 42);
        Assert.ThrowsException<InvalidCastException>(
            () => cs.CurrentFrame.GetLocal<string>("x"));
    }
}
