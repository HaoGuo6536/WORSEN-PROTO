// ============================================================================
// RunFactRelayTestUtility.cs
// ============================================================================
// PURPOSE:
//   Resolves the explicit Run fact groups for routing fixtures that enumerate event
//   names. Reflection still inspects the real publisher and fails on missing fields;
//   it does not create a substitute event stream or change delivery assertions.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 test support) · Editor · Run.
// KEY RESPONSIBILITIES:
//   - Locate an event on the Run Manager or one of its typed fact groups.
//   - Read the actual backing delegate for subscription and routing assertions.
//   - Exercise pure channel delivery, pause gates, observer order and teardown.
// DEPENDENCIES:
//   - Session Run, NUnit and managed reflection only.
// USAGE NOTES:
//   Test-only support for mixed legacy/channel event lists. Non-event fields retain
//   their exact original reflection target; missing members are never ignored.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Worsen.Session.Run;

namespace Worsen.Tests.Run
{
    internal static class RunFactRelayTestUtility
    {
        internal static object Publisher(object target, string name)
        {
            if (target is RunSessionManager run)
            {
                if (typeof(RunHunterFactRelayController).GetEvent(name) != null) return run.HunterFacts;
                if (typeof(RunFloorFactRelayController).GetEvent(name) != null) return run.FloorFacts;
                if (typeof(RunPlayerFactRelayController).GetEvent(name) != null) return run.PlayerFacts;
                if (typeof(RunWorldFactRelayController).GetEvent(name) != null) return run.WorldFacts;
            }
            return target;
        }

        internal static object Read(object target, string name)
        {
            target = Publisher(target, name);
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        internal static void VerifyDelivery(object channel, Action<bool> pause, string method, string eventName, bool gated)
        {
            var input = channel.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            var output = channel.GetType().GetEvent(eventName);
            Assert.That(input, Is.Not.Null, method);
            Assert.That(output, Is.Not.Null, eventName);
            var arguments = input.GetParameters().Select(p => Sample(p.ParameterType)).ToArray();
            var order = new List<int>();
            var first = new Observer(arguments, order, 1);
            var second = new Observer(arguments, order, 2);
            Delegate a = first.Handler(output.EventHandlerType), b = second.Handler(output.EventHandlerType);
            output.AddEventHandler(channel, a); output.AddEventHandler(channel, b);
            input.Invoke(channel, arguments);
            Assert.That(order, Is.EqualTo(new[] { 1, 2 }), "Synchronous once-only delivery in subscription order.");
            input.Invoke(channel, arguments);
            Assert.That(order, Is.EqualTo(new[] { 1, 2, 1, 2 }), "The relay must not deduplicate source facts.");
            order.Clear(); pause(true); input.Invoke(channel, arguments);
            Assert.That(order, Is.EqualTo(gated ? Array.Empty<int>() : new[] { 1, 2 }));
            pause(false); order.Clear(); output.RemoveEventHandler(channel, a); input.Invoke(channel, arguments);
            Assert.That(order, Is.EqualTo(new[] { 2 }));
            output.RemoveEventHandler(channel, b); order.Clear(); input.Invoke(channel, arguments);
            Assert.That(order, Is.Empty);
            output.AddEventHandler(channel, a); output.AddEventHandler(channel, b);
            channel.GetType().GetMethod("Teardown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(channel, null);
            foreach (var fact in channel.GetType().GetEvents()) Assert.That(Read(channel, fact.Name), Is.Null, fact.Name);
            input.Invoke(channel, arguments); Assert.That(order, Is.Empty);
        }

        private static object Sample(Type type)
        {
            if (type == typeof(int)) return 7;
            if (type == typeof(long)) return 17L;
            if (type == typeof(float)) return .75f;
            if (type == typeof(bool)) return true;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
                return Array.CreateInstance(type.GetGenericArguments()[0], 1);
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private sealed class Observer
        {
            private readonly object[] expected;
            private readonly List<int> order;
            private readonly int id;
            internal Observer(object[] expected, List<int> order, int id)
            { this.expected = expected; this.order = order; this.id = id; }
            internal Delegate Handler(Type type)
            {
                Type[] arguments = type.GetGenericArguments();
                var method = GetType().GetMethod("Receive" + arguments.Length).MakeGenericMethod(arguments);
                return Delegate.CreateDelegate(type, this, method);
            }
            private void Record(params object[] values) { Assert.That(values, Is.EqualTo(expected)); order.Add(id); }
            public void Receive1<T>(T a) => Record(a);
            public void Receive2<T, U>(T a, U b) => Record(a, b);
            public void Receive3<T, U, V>(T a, U b, V c) => Record(a, b, c);
            public void Receive4<T, U, V, W>(T a, U b, V c, W d) => Record(a, b, c, d);
            public void Receive5<T, U, V, W, X>(T a, U b, V c, W d, X e) => Record(a, b, c, d, e);
        }
    }
}
