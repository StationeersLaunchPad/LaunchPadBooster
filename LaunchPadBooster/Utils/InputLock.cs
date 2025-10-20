using Assets.Scripts;
using Assets.Scripts.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaunchPadBooster.Utils
{
    /// <summary>
    /// Creating this object will create an input lock in the game.
    /// This prevents keyboard or mouse input from doing what it normally does.
    /// Cancelling the lock is done by calling Dispose() on the object.
    /// </summary>
    public class InputLock : IDisposable
    {
        [Flags]
        public enum LockType : int
        {
            None = 0,
            Keyboard = 1,
            Mouse = 2,
        }

        /// <summary>
        /// Creates a new input lock.
        /// </summary>
        /// <param name="lockType">What input to lock</param>
        public InputLock(LockType lockType = LockType.Keyboard)
        {
            this.lockType = lockType;
            lockName = $"LaunchPadBoosterLock-{lockCounter++}";

            if (lockType.HasFlag(LockType.Keyboard))
            {
                KeyManager.SetInputState(lockName, KeyInputState.Typing);
                CursorManager.SetCursor(true);
                InputWindow.InputState = InputPanelState.Waiting;
            }

            if (lockType.HasFlag(LockType.Mouse))
            {
                InputMouse.SetMouseControl(true);
            }
        }

        private bool disposedValue;
        private string lockName;
        private LockType lockType;

        private static long lockCounter = 0;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // There is no managed state (managed objects)
                }

                if (lockType.HasFlag(LockType.Keyboard))
                {
                    KeyManager.RemoveInputState(lockName);
                    CursorManager.SetCursor(false);
                    InputWindow.InputState = InputPanelState.None;
                }

                if (lockType.HasFlag(LockType.Mouse))
                {
                    InputMouse.SetMouseControl(false);
                }
                disposedValue = true;
            }
        }

        ~InputLock()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
