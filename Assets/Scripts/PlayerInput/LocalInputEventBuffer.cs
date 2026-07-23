using UnityEngine;

namespace FrameSyncMoba.PlayerInput
{
    public enum LocalGameplayInputEventKind : byte
    {
        None = 0,
        PrimaryClick = 1,
        SecondaryClick = 2,
        Cancel = 3,
        AbilityKeyPressed = 4,
        AbilityKeyReleased = 5,
    }

    public readonly struct LocalGameplayInputEvent
    {
        public readonly ulong LocalEventSequence;
        public readonly LocalGameplayInputEventKind Kind;
        public readonly byte AbilitySlot;
        public readonly Vector2 ScreenPositionAtEvent;

        internal LocalGameplayInputEvent(
            ulong localEventSequence,
            LocalGameplayInputEventKind kind,
            byte abilitySlot,
            Vector2 screenPositionAtEvent)
        {
            LocalEventSequence = localEventSequence;
            Kind = kind;
            AbilitySlot = abilitySlot;
            ScreenPositionAtEvent = screenPositionAtEvent;
        }
    }

    public sealed class LocalInputEventBuffer
    {
        public const int MaxLocalInputEventsPerUnityFrame = 64;

        private readonly LocalGameplayInputEvent[] events =
            new LocalGameplayInputEvent[MaxLocalInputEventsPerUnityFrame];
        private int head;
        private int count;
        private ulong nextLocalEventSequence = 1;

        public int Count => count;

        public bool Push(
            LocalGameplayInputEventKind kind,
            byte abilitySlot,
            Vector2 screenPositionAtEvent)
        {
            if (kind == LocalGameplayInputEventKind.None) return false;
            if (count >= MaxLocalInputEventsPerUnityFrame) return false;
            if (nextLocalEventSequence == ulong.MaxValue)
            {
                throw new System.InvalidOperationException(
                    "Local gameplay input event sequence overflow.");
            }

            int index = (head + count) % events.Length;
            events[index] = new LocalGameplayInputEvent(
                nextLocalEventSequence++,
                kind,
                abilitySlot,
                screenPositionAtEvent);
            count++;
            return true;
        }

        public bool TryDequeue(out LocalGameplayInputEvent inputEvent)
        {
            if (count == 0)
            {
                inputEvent = default;
                return false;
            }

            inputEvent = events[head];
            events[head] = default;
            head = (head + 1) % events.Length;
            count--;
            return true;
        }

        public void Clear()
        {
            System.Array.Clear(events, 0, events.Length);
            head = 0;
            count = 0;
        }
    }
}
