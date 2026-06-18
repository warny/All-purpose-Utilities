using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine;

// ── Test processor wiring CALL/RET against CallStackContext ──────────────────

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

    /// <summary>Returns to the address saved by the most recent CALL.</summary>
    [Instruction("RET", 0xC1)]
    void Ret(CallStackContext ctx) => ctx.InstructionPointer = ctx.CallStack.Return();

    /// <summary>Halts execution by advancing the instruction pointer past the end of the stream.</summary>
    [Instruction("HALT", 0xFF)]
    void Halt(CallStackContext ctx) => ctx.InstructionPointer = ctx.Data.Length;
}

// ── Unit tests ────────────────────────────────────────────────────────────────

[TestClass]
public class CallStackTests
{
    // ── ICallStack contract: CallStack ────────────────────────────────────────

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
    public void CallStack_Return_OnEmpty_Throws()
    {
        var cs = new CallStack();
        Assert.ThrowsException<InvalidOperationException>(() => cs.Return());
    }

    [TestMethod]
    public void CallStack_TryReturn_OnEmpty_ReturnsFalse()
    {
        var cs = new CallStack();
        Assert.IsFalse(cs.TryReturn(out int addr));
        Assert.AreEqual(0, addr);
    }

    [TestMethod]
    public void CallStack_TryReturn_WhenNotEmpty_ReturnsTrueAndAddress()
    {
        var cs = new CallStack();
        cs.Call(42);
        Assert.IsTrue(cs.TryReturn(out int addr));
        Assert.AreEqual(42, addr);
        Assert.IsTrue(cs.IsEmpty);
    }

    [TestMethod]
    public void CallStack_MaxDepth_Overflow_Throws()
    {
        var cs = new CallStack { MaxDepth = 3 };
        cs.Call(1);
        cs.Call(2);
        cs.Call(3);
        Assert.ThrowsException<InvalidOperationException>(() => cs.Call(4));
    }

    [TestMethod]
    public void CallStack_MaxDepth_SetBelowOne_Throws()
    {
        var cs = new CallStack();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => cs.MaxDepth = 0);
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

    // ── CallFrame local variables ─────────────────────────────────────────────

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

    // ── ICallStack contract: SimpleCallStack ──────────────────────────────────

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
    public void SimpleCallStack_Return_OnEmpty_Throws()
    {
        var cs = new SimpleCallStack();
        Assert.ThrowsException<InvalidOperationException>(() => cs.Return());
    }

    [TestMethod]
    public void SimpleCallStack_TryReturn_OnEmpty_ReturnsFalse()
    {
        var cs = new SimpleCallStack();
        Assert.IsFalse(cs.TryReturn(out _));
    }

    [TestMethod]
    public void SimpleCallStack_MaxDepth_Overflow_Throws()
    {
        var cs = new SimpleCallStack { MaxDepth = 2 };
        cs.Call(1);
        cs.Call(2);
        Assert.ThrowsException<InvalidOperationException>(() => cs.Call(3));
    }

    [TestMethod]
    public void SimpleCallStack_MaxDepth_SetBelowOne_Throws()
    {
        var cs = new SimpleCallStack();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => cs.MaxDepth = 0);
    }

    // ── CallStackContext ──────────────────────────────────────────────────────

    [TestMethod]
    public void CallStackContext_DefaultCtor_UsesCallStack()
    {
        var ctx = new CallStackContext([]);
        Assert.IsInstanceOfType<CallStack>(ctx.CallStack);
    }

    [TestMethod]
    public void CallStackContext_CustomCtor_UsesProvidedStack()
    {
        var simple = new SimpleCallStack();
        var ctx = new CallStackContext([], simple);
        Assert.AreSame(simple, ctx.CallStack);
    }

    [TestMethod]
    public void CallStackContext_CustomCtor_NullStack_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new CallStackContext([], null!));
    }

    // ── Integration: CALL / RET in a VirtualProcessor ────────────────────────

    [TestMethod]
    public void Integration_Call_JumpsToSubroutine_ReturnRestoresIP()
    {
        // Layout (little-endian, 14 bytes total):
        //   0: CALL 0xC0
        //   1-4: target = 8 (jump to subroutine)
        //        → after operand read: IP=5, return addr = 5
        //   5: HALT 0xFF
        //   6-7: (padding, never reached)
        //   8: PUSH_INT 0x01
        //   9-12: 99 (int operand)
        //   13: RET 0xC1  → pops 5, sets IP=5 → HALT
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
        //   0: CALL → 10    (return to 5)
        //   5: HALT
        //   6-9: padding
        //  10: CALL → 20   (return to 15)
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
}
